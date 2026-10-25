using StarPlayer.SimHarness.Evidence;
using static StarPlayer.SimHarness.Evidence.Schema;

namespace StarPlayer.EvidenceVerifier;

public enum ApprovalResolution { ResolvedTestOnly, ResolvedOfficial }

/// <summary>What the bootstrap established from the trusted registry: the approval record and its three profiles (each read once, bytes kept), the execution authorization and the resolution state (§14.2).</summary>
public sealed record ResolvedApproval(
    ApprovalRecordV1 Approval, (JObject Doc, string Sha) Build, (JObject Doc, string Sha) Environment, (JObject Doc, string Sha) TestProfile, JObject Authorization, ApprovalResolution Resolution);

/// <summary>
/// T06a §16 ③-⑧ after the anchor (T1/T2) and the connection (T3): the registry instance id against the anchor (R03-4 step 7), the approval record by id (L2), its registrant among the anchor's owners and its
/// SHA recomputed from the returned bytes (L3), the strict §13 read of those bytes (L4), no other record for the validation set (L5), the three profile entries with the same checks plus PATCH-04 (L6), the
/// TRUST_CONFIG post-check (T6), the execution authorization (L7) and an identical re-query (L8). Every failure is <c>Evidence.approval_mismatch</c> with its <c>Trust.*</c> cause, except a changed
/// re-query (<c>registration_conflict</c>). A local, untrusted registry can resolve only a TEST_ONLY record.
/// </summary>
public static class TrustBootstrap
{
    private static EvidenceException Mismatch(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, cause, "", "trust_bootstrap", message);

    private static (RegistryEntry Entry, JObject Doc) Owned(IEvidenceRegistry registry, JObject anchor, string kind, string id, string? expectedSha, Shape shape)
    {
        var e = registry.Get(kind, id) ?? throw Mismatch("Trust.entry_not_found", $"{kind} '{id}'가 신뢰 기록부에 없습니다(L2).");
        var owners = Items(anchor["owner_principal_sids"]).Select(Text).ToHashSet(StringComparer.Ordinal);
        if (!owners.Contains(e.RegistrantPrincipalId))
            throw Mismatch("Trust.registrant_not_owner", $"{kind} '{id}'의 등록 주체 {e.RegistrantPrincipalId}가 앵커의 OWNER가 아닙니다(L3).");
        var sha = Archive.Sha(e.RawBytes);
        if (sha != e.RawSha256 || (expectedSha is not null && sha != expectedSha))
            throw Mismatch("Trust.raw_sha_mismatch", $"{kind} '{id}' 원문의 SHA-256이 기록부·승인 참조 값과 다릅니다(L3).");
        return (e, ApprovalFormats.Read(shape, e.RawBytes));
    }

    public static ResolvedApproval Resolve(TrustAnchorV1 anchor, IEvidenceRegistry registry, string approvalRecordId, string approvalRecordSha256, string authorizationId, string? repositoryRoot)
    {
        var a = anchor.Anchor;
        if (registry.InstanceId != Text(a["registry_instance_id"]))
            throw Mismatch("Trust.registry_mismatch", $"기록부 instance id '{registry.InstanceId}'가 앵커 값과 다릅니다(R03-4 7단계, N27).");

        var (entry, _) = Owned(registry, a, "APPROVAL_RECORD", approvalRecordId, approvalRecordSha256, ApprovalFormats.Approval);
        var approval = ApprovalFormats.ReadApproval(entry.RawBytes, repositoryRoot);                              // L4: the same bytes are parsed
        var scope = registry.GetByScope("APPROVAL_RECORD", approval.ValidationSetId);
        if (scope.Count != 1 || scope[0].Id != approvalRecordId || !scope[0].RawBytes.AsSpan().SequenceEqual(entry.RawBytes))
            throw Mismatch("Trust.validation_set_conflict", $"검증 집합 '{approval.ValidationSetId}'의 승인 기록이 정확히 이것 하나가 아닙니다(L5).");

        (JObject, string) Profile(string field, Shape shape)
        {
            var r = approval.Ref(field);
            var (e, doc) = Owned(registry, a, field switch { "build_provenance" => "BUILD_PROVENANCE", "environment_profile" => "ENVIRONMENT_PROFILE", _ => "PZL_TEST_PROFILE" },
                Text(r["id"]), Text(r["raw_sha256"]), shape);
            return (doc, e.RawSha256);
        }

        var build = Profile("build_provenance", ApprovalFormats.BuildProvenance);
        var environment = Profile("environment_profile", ApprovalFormats.EnvironmentProfile);
        var testProfile = Profile("pzl_test_profile", ApprovalFormats.TestProfile);
        ApprovalFormats.CheckProfiles(approval, build, environment, testProfile);                                  // L6 / PATCH-04

        var trust = Items(build.Item1["artifacts"]).Select(Obj).Where(x => Text(x["role"]) == "TRUST_CONFIG").ToList();
        if (trust.Count != 1 || Text(trust[0]["raw_sha256"]) != anchor.RawSha256)
            throw EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, "Trust.anchor_mismatch", "/artifacts", "trust_bootstrap",
                "Build Provenance의 TRUST_CONFIG raw_sha256이 앵커 바이트의 SHA와 다릅니다(T6 사후 대조, N26).");

        var (authEntry, _) = Owned(registry, a, "EXECUTION_AUTHORIZATION", authorizationId, null, ApprovalFormats.Authorization);
        var authorization = ApprovalFormats.ReadAuthorization(authEntry.RawBytes, approval);                    // L7

        if (registry.Get("APPROVAL_RECORD", approvalRecordId) is not { } again || !again.RawBytes.AsSpan().SequenceEqual(entry.RawBytes))
            throw EvidenceCodes.Fail(EvidenceCodes.RegistrationConflict, "Trust.requery_differs", "", "trust_bootstrap", "승인 기록 재조회 결과가 다릅니다(L8).");

        ApprovalResolution resolution;
        if (approval.TestOnly)
            resolution = ApprovalResolution.ResolvedTestOnly;
        else if (registry.Trusted)
            resolution = ApprovalResolution.ResolvedOfficial;
        else
            throw Mismatch("Trust.registry_untrusted", "인증된 Pipe 기록부가 아닌 로컬 기록부로는 공식(test_only=false) 승인을 확정하지 않습니다(A17).");
        return new ResolvedApproval(approval, build, environment, testProfile, authorization, resolution);
    }
}
