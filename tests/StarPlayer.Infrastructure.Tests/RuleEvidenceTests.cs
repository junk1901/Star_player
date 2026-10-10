using System.Text.Json;
using System.Text.RegularExpressions;
using StarPlayer.Domain.Baseball.Game;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Infrastructure.Tests;

/// <summary>
/// Guards the rule evidence table (content/rules/game_rules_evidence.json, policy "KBO first, 2026 OBR fallback"): official evidence and test success are
/// different things, so this checks the BOOKKEEPING only: every rule has one of the four approved statuses, no OBR fallback or simplification is dressed as
/// KBO verification, every test id really exists in the test sources, and the Gate blockers are exactly the required rules that are still unresolved.
/// Since the 2026 OBR text was read (docs/2026-official-baseball-rules.pdf) it also guards the OBR comparison table: every row has a class, every OPEN_DEVIATION
/// carries Issue / Reason / Recommended Option / Alternative / Impact, and an approval is never claimed for a difference nobody approved.
/// </summary>
public class RuleEvidenceTests
{
    private static readonly string[] Statuses = ["KBO_VERIFIED", "OBR_FALLBACK_APPROVED", "GAME_SIMPLIFICATION_APPROVED", "UNRESOLVED"];

    private static string Root
    {
        get
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "StarPlayer.sln")))
                    return d.FullName;
            throw new InvalidOperationException("StarPlayer.sln을 찾지 못했습니다.");
        }
    }

    private static JsonElement Evidence() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "content", "rules", "game_rules_evidence.json"))).RootElement;

    private static HashSet<string> TestMethodNames()
    {
        var names = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "tests"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"public void (\w+)\("))
                names.Add(m.Groups[1].Value);
        return names;
    }

    [Fact]
    public void Every_rule_has_an_approved_status_a_reason_and_unique_id_and_nothing_non_kbo_claims_kbo_verification()
    {
        var rules = Evidence().GetProperty("rules").EnumerateArray().ToList();
        Assert.True(rules.Count >= 20);
        Assert.Equal(rules.Count, rules.Select(r => r.GetProperty("rule_id").GetString()).Distinct().Count());

        foreach (var rule in rules)
        {
            var id = rule.GetProperty("rule_id").GetString()!;
            var status = rule.GetProperty("status").GetString()!;
            var urls = rule.GetProperty("source_urls").EnumerateArray().Select(u => u.GetString()!).ToList();
            Assert.Contains(status, Statuses);
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("reason").GetString()), id);
            Assert.False(string.IsNullOrWhiteSpace(rule.GetProperty("what_was_read").GetString()), id);
            if (status == "KBO_VERIFIED")
                Assert.True(urls.Count > 0 && urls.All(u => u.StartsWith("https://www.koreabaseball.com/", StringComparison.Ordinal)), $"{id}: KBO 인증은 KBO 공식 URL만 근거로 가능");
            if (status == "OBR_FALLBACK_APPROVED")
                Assert.Contains(urls, u => u.StartsWith("https://www.mlb.com/", StringComparison.Ordinal));
            if (status == "GAME_SIMPLIFICATION_APPROVED")
                Assert.DoesNotContain("KBO_VERIFIED", rule.GetRawText());
        }
    }

    [Fact]
    public void The_policy_says_the_obr_text_was_read_names_the_exact_file_and_the_pdf_matches_when_it_is_present()
    {
        var policy = Evidence().GetProperty("policy");
        Assert.True(policy.GetProperty("obr_text_read").GetBoolean());
        Assert.Equal("2026 Official Baseball Rules", policy.GetProperty("obr_edition").GetString());
        var pdf = policy.GetProperty("obr_pdf");
        var path = Path.Combine(Root, pdf.GetProperty("local_path").GetString()!.Replace('/', Path.DirectorySeparatorChar));
        Assert.Matches("^[0-9a-f]{64}$", pdf.GetProperty("sha256").GetString()!);
        if (File.Exists(path))   // the PDF is a supplied reference file; when it is there it must be exactly the file that was read
        {
            Assert.Equal(pdf.GetProperty("bytes").GetInt64(), new FileInfo(path).Length);
            Assert.Equal(pdf.GetProperty("sha256").GetString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant());
        }
    }

    [Fact]
    public void Every_obr_fallback_rule_has_compared_clauses_and_no_glossary_sentence_is_dressed_as_a_clause()
    {
        var evidence = Evidence();
        var compared = evidence.GetProperty("obr_comparison").EnumerateArray().Select(r => r.GetProperty("rule_id").GetString()!).ToHashSet();
        foreach (var rule in evidence.GetProperty("rules").EnumerateArray().Where(r => r.GetProperty("status").GetString() == "OBR_FALLBACK_APPROVED"))
        {
            var id = rule.GetProperty("rule_id").GetString()!;
            Assert.Contains(id, compared);   // an OBR fallback without a clause-level comparison would only be a glossary reading
            Assert.Contains(rule.GetProperty("source_urls").EnumerateArray().Select(u => u.GetString()!),
                u => u.StartsWith("https://mktg.mlbstatic.com/", StringComparison.Ordinal) || u.StartsWith("https://www.mlb.com/", StringComparison.Ordinal));
            var text = rule.GetProperty("what_was_read").GetString()! + rule.GetProperty("reason").GetString()!;
            Assert.DoesNotMatch("조항 미열람|원문 미열람|조항 번호 미인용|원문 PDF는", text);   // the old "OBR clause unread" wording must not survive the original-text reading
        }
    }

    [Fact]
    public void The_obr_comparison_table_has_valid_classes_known_rules_and_a_full_issue_record_for_every_open_deviation()
    {
        var evidence = Evidence();
        string[] classes = ["MATCH", "KBO_PRECEDENCE", "APPROVED_SIMPLIFICATION", "PROPOSED_SIMPLIFICATION", "OPEN_DEVIATION", "NOT_MODELLED"];
        var ruleIds = evidence.GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("rule_id").GetString()!).ToHashSet();
        var rows = evidence.GetProperty("obr_comparison").EnumerateArray().ToList();
        Assert.Equal(rows.Count, rows.Select(r => r.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(classes.OrderBy(c => c), evidence.GetProperty("obr_comparison_legend").EnumerateObject().Select(o => o.Name).OrderBy(c => c));

        foreach (var row in rows)
        {
            var id = row.GetProperty("id").GetString()!;
            var result = row.GetProperty("result").GetString()!;
            Assert.Contains(result, classes);
            Assert.Contains(row.GetProperty("rule_id").GetString()!, ruleIds);
            Assert.NotEmpty(row.GetProperty("clauses").EnumerateArray());
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("verified_by").GetString()), id);

            if (result is "APPROVED_SIMPLIFICATION" or "KBO_PRECEDENCE" or "NOT_MODELLED")
                Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("basis").GetString()), $"{id}: 승인/우선/미구현에는 근거(basis)가 필요");
            if (result == "PROPOSED_SIMPLIFICATION")
            {
                Assert.Matches("승인 전|승인 보류", row.GetProperty("basis").GetString()!);   // never worded as approved
                foreach (var field in new[] { "original_rule", "difference", "impact", "recommendation", "gate_note" })
                    Assert.False(string.IsNullOrWhiteSpace(row.GetProperty(field).GetString()), $"{id}: {field}");
                Assert.True(row.TryGetProperty("gate_required", out var gate) && gate.ValueKind is JsonValueKind.True or JsonValueKind.False, $"{id}: gate_required");
            }
            if (result == "OPEN_DEVIATION")
                foreach (var field in new[] { "issue", "reason", "recommended_option", "alternative", "impact", "gate_effect" })
                    Assert.False(string.IsNullOrWhiteSpace(row.GetProperty(field).GetString()), $"{id}: {field}");
        }

        var open = rows.Where(r => r.GetProperty("result").GetString() == "OPEN_DEVIATION").Select(r => r.GetProperty("id").GetString()!).Order().ToList();
        Assert.Equal(open, evidence.GetProperty("obr_open_deviation_ids").EnumerateArray().Select(b => b.GetString()!).Order().ToList());

        // pending owner decisions are exactly the proposed simplifications; a pending row of a Gate-required rule is tracked as a possible implementation defect,
        // kept apart from the rule-source blockers (gate_blockers)
        var pending = rows.Where(r => r.GetProperty("result").GetString() == "PROPOSED_SIMPLIFICATION").ToList();
        Assert.Equal(pending.Select(r => r.GetProperty("id").GetString()!).Order(), evidence.GetProperty("pending_owner_decisions").EnumerateArray().Select(b => b.GetString()!).Order());
        var tracked = evidence.GetProperty("implementation_defects_open").GetProperty("gate_required_rules").EnumerateArray().Select(b => b.GetString()!).Order().ToList();
        Assert.Equal(pending.Where(r => r.GetProperty("gate_required").GetBoolean()).Select(r => r.GetProperty("id").GetString()!).Order(), tracked);
    }

    [Fact]
    public void Every_fixed_deviation_names_hand_tests_that_exist_and_says_what_was_wrong_before()
    {
        var names = TestMethodNames();
        var fixedRows = Evidence().GetProperty("obr_comparison").EnumerateArray().Where(r => r.TryGetProperty("fixed_on", out _)).ToList();
        Assert.True(fixedRows.Count >= 7);   // C12 C14 C19 C24 C29 C32 C34
        foreach (var row in fixedRows)
        {
            var id = row.GetProperty("id").GetString()!;
            Assert.Equal("MATCH", row.GetProperty("result").GetString());
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("before_fix").GetString()), id);
            var tests = row.GetProperty("tests").EnumerateArray().Select(x => x.GetString()!).ToList();
            Assert.NotEmpty(tests);
            Assert.All(tests, x => Assert.True(names.Contains(x), $"{id}: 테스트 '{x}'가 없음"));
        }
    }

    [Fact]
    public void R02_is_resolved_by_an_owner_approved_application_with_operating_evidence_and_the_clause_is_not_claimed_as_certified()
    {
        var r02 = Evidence().GetProperty("rules").EnumerateArray().Single(r => r.GetProperty("rule_id").GetString() == "R02-tie-when-limit-reached");
        Assert.Equal("GAME_SIMPLIFICATION_APPROVED", r02.GetProperty("status").GetString());
        Assert.False(r02.GetProperty("clause_certified").GetBoolean());
        var urls = r02.GetProperty("source_urls").EnumerateArray().Select(u => u.GetString()!).ToList();
        Assert.Contains(urls, u => u.EndsWith("bdSe=11353", StringComparison.Ordinal));
        Assert.Contains(urls, u => u.EndsWith("bdSe=61318", StringComparison.Ordinal));
        Assert.All(urls, u => Assert.StartsWith("https://www.koreabaseball.com/", u));   // press reports live in press_urls, never among the official sources
        Assert.Contains("퓨처스", r02.GetProperty("open_risk").GetString()!);   // the Futures-league 승부치기 sentence is kept out of the first-team evidence
    }

    [Fact]
    public void Every_listed_test_exists_and_every_required_rule_that_is_not_a_pure_policy_statement_names_at_least_one()
    {
        var names = TestMethodNames();
        foreach (var rule in Evidence().GetProperty("rules").EnumerateArray())
        {
            var id = rule.GetProperty("rule_id").GetString()!;
            var tests = rule.GetProperty("tests").EnumerateArray().Select(t => t.GetString()!).ToList();
            Assert.All(tests, t => Assert.True(names.Contains(t), $"{id}: 테스트 '{t}'가 없음"));
            var status = rule.GetProperty("status").GetString();
            if (rule.GetProperty("gate_required").GetBoolean() && status != "UNRESOLVED" && id != "R24-pitch-clock-abs-not-modelled")
                Assert.NotEmpty(tests);
        }
    }

    [Fact]
    public void The_gate_blockers_are_exactly_the_required_rules_that_are_still_unresolved_and_none_is_hidden_by_a_passing_test()
    {
        var evidence = Evidence();
        var computed = evidence.GetProperty("rules").EnumerateArray()
            .Where(r => r.GetProperty("gate_required").GetBoolean() && r.GetProperty("status").GetString() == "UNRESOLVED")
            .Select(r => r.GetProperty("rule_id").GetString()!).Order().ToList();

        Assert.Equal(computed, evidence.GetProperty("gate_blockers").EnumerateArray().Select(b => b.GetString()!).Order().ToList());
        Assert.DoesNotContain("R02-tie-when-limit-reached", computed);   // R02 was resolved by the owner's application approval (2026-10-11); open OBR deviations are tracked separately
    }

    [Fact]
    public void The_profiles_name_this_policy_and_only_the_kbo_oriented_profile_is_kbo_oriented_while_fictional_profiles_say_so()
    {
        var policy = Evidence().GetProperty("policy_id").GetString();
        var profiles = GameRulesJson.Read(ScriptedGameSource.ReadResource("game_rules_profiles.json")).Value;

        var kbo = Assert.Single(profiles, p => p.Status == GameRulesStatus.KboOrientedUnverified);
        Assert.Equal(policy, kbo.RulePolicyId);
        Assert.True(kbo.DhMayBeForfeited);
        Assert.True(kbo.FielderToMoundForfeitsDh);           // OBR 5.11(a)(14) (rule R27), read in the original text on 2026-10-11, is on in the KBO-oriented profile
        Assert.All(profiles.Where(p => p != kbo), p => Assert.Equal("fictional-test-v1", p.RulePolicyId));
        Assert.All(profiles.Where(p => p != kbo && p.FielderToMoundForfeitsDh), p => Assert.Equal(GameRulesStatus.FictionalTest, p.Status));
        Assert.True(kbo.TieAllowed && kbo.ExtraInningsLimit == 11);   // R01 + R02
        var forfeit = Assert.Single(profiles, p => p.ProfileId == "test-dh-forfeit");   // the profile DhStateTests runs the transitions on has the KBO-oriented DH switches
        Assert.Equal((forfeit.DesignatedHitter, forfeit.TwoWayPlayerRule, forfeit.DhMayBeForfeited, forfeit.FielderToMoundForfeitsDh),
            (kbo.DesignatedHitter, kbo.TwoWayPlayerRule, kbo.DhMayBeForfeited, kbo.FielderToMoundForfeitsDh));
    }
}
