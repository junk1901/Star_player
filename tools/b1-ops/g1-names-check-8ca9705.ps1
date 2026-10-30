<#
.SYNOPSIS
  G1-a static consistency check of the B-4 normative file-name / path table against the production source (read-only).

.DESCRIPTION
  G1-a proposal (docs/tasks/B1_G1_G2_CONDITIONAL_DECISION_PROPOSAL.md, part B): the official names stay as the code has them and the
  normative table in B-4 (docs/tasks/B1_B4_BUILD_PROCEDURE_DECISION_DRAFT.md 6.1) is the single definition. Production source = src/,
  tools/StarPlayer.SimHarness/, tools/StarPlayer.EvidenceVerifier/, *.cs. Four checks, each against values pinned from 8ca9705:
    1. names       every occurrence of each governed name literal, by file and count (unchanged from the first version);
    2. definitions each B-4 6.1 row's path-forming definition is exactly the pinned source line, once, in its file
                   (run/<run_id>/ composition, Family/Unit PREFLIGHT names and extension, result root, run indexes, units/, cells/);
    3. lines       the multiset of every governed line (non-comment line holding a governed literal or a reference to a governed
                   path symbol: RunPath, UnitPreflightName, PreflightFile, PreflightDirectory, RootRelative, ResultFile, UnitDirectory,
                   CellDirectory) per file; any added, removed or edited line, or a governed line in an unlisted file, fails. This is
                   what catches a changed path with an unchanged count (codex G1-POLICY-01);
    4. files       the SHA-256 of every file that holds a governed line, after CRLF -> LF (= the git blob for these files), so a change
                   of the data that flows into a path inside those files (no governed token on the changed line) also fails.
  Limits (static): a name assembled from pieces outside the governed files (e.g. "manifest" + ".json", or a copied literal under a new
  identifier) is not seen; values that reach the governed files from other files through ungoverned identifiers are not traced. Test
  code is only counted (informational); producer -> archive -> verifier agreement at run time is the TEST_ONLY end-to-end tests.
  Version for product source 8ca9705539bc941919f5290de149c4678f5147dd (tree fd87de669127228dd15cbe9cbc85272326a65ff7, G5-TEMP-01 C-3): the
  rules, definitions and self-test are those of g1-names-check.ps1 (d3945b5e, pinned to c77ed1a, kept unchanged); only the two governed
  lines and three file SHA-256 values that C-3 changed are re-pinned. A PASS is evidence for a tree whose Commit and Tree were checked
  outside this tool, never an approval of another commit.
  -SelfTest copies the production files into a scratch directory, applies counterexample mutations and checks each verdict
  (it never touches -Root). Exit: 0 PASS, 1 FAIL, 2 usage.
#>
[CmdletBinding()]
param(
    [string]$Root,
    [switch]$SelfTest
)
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
if (-not $Root) { $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }   # $PSScriptRoot is empty in a PS 5.1 param default

# name -> regex and the exact production occurrences (repository-relative path -> count) of the B-4 6.1 table
$Inventory = [ordered]@{
    'manifest.json (UNIT_MANIFEST file)'          = @('(?<=["/])manifest\.json"', [ordered]@{ 'tools/StarPlayer.EvidenceVerifier/Verifier.cs' = 1; 'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = 3; 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = 2; 'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = 2; 'tools/StarPlayer.SimHarness/RunReport.cs' = 1 })
    'preflight.json (Family PREFLIGHT)'           = @('"preflight\.json"', [ordered]@{ 'tools/StarPlayer.SimHarness/Official.cs' = 1 })
    'preflight/ (Unit PREFLIGHT directory)'       = @('PreflightDirectory = "preflight"', [ordered]@{ 'tools/StarPlayer.SimHarness/Official.cs' = 1 })
    'run/<run_id>/ (run archive prefix)'          = @('"run/|\$"run/', [ordered]@{ 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = 1 })
    'build/simharness/official (result root)'     = @('"build/simharness/official"', [ordered]@{ 'tools/StarPlayer.SimHarness/Official.cs' = 1 })
    'family-result.json (Family run index)'       = @('"family-result\.json"', [ordered]@{ 'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = 1 })
    'dominance-result.json (Dominance run index)' = @('"dominance-result\.json"', [ordered]@{ 'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = 1 })
    'units/ (unit directory)'                     = @('"units/"|\$"units/\{', [ordered]@{ 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = 1; 'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = 1 })
    'cells/ (Dominance cell directory)'           = @('"cells/"', [ordered]@{ 'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = 1 })
}

# B-4 6.1 row -> the file and the exact (trimmed) line that forms that name or path; each must occur exactly once in that file
$Definitions = [ordered]@{
    'build/simharness/official/<run_id> (result root)'  = @('tools/StarPlayer.SimHarness/Official.cs', 'public const string RootRelative = "build/simharness/official";')
    'run/<run_id>/<path in result folder>'              = @('tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs', 'public static string RunPath(string runId, string pathInResultDirectory) => $"run/{runId}/{pathInResultDirectory}";')
    'preflight.json and preflight/ (PREFLIGHT)'         = @('tools/StarPlayer.SimHarness/Official.cs', 'public const string PreflightFile = "preflight.json", PreflightDirectory = "preflight";')
    'preflight/<unit lower>.json (Unit PREFLIGHT)'      = @('tools/StarPlayer.SimHarness/Official.cs', 'public static string UnitPreflightName(string unitId) => unitId.ToLowerInvariant() + ".json";')
    'units/<unit lower>/ (Family result folder)'        = @('tools/StarPlayer.SimHarness/FamilyOfficial.cs', 'public static string UnitDirectory(FamilyUnit unit) => "units/" + unit.Id.ToLowerInvariant();')
    'units/<unit lower>/ (archive unit evidence)'       = @('tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs', 'var dir = $"units/{u.UnitId.ToLowerInvariant()}";')
    'cells/<cell lower>/ (Dominance result folder)'     = @('tools/StarPlayer.SimHarness/DominanceOfficial.cs', 'public static string CellDirectory(DominanceCell cell) => "cells/" + cell.Id.ToLowerInvariant();')
    'family-result.json (Family run index)'             = @('tools/StarPlayer.SimHarness/FamilyOfficial.cs', 'public const string ResultFile = "family-result.json";')
    'dominance-result.json (Dominance run index)'       = @('tools/StarPlayer.SimHarness/DominanceOfficial.cs', 'public const string ResultFile = "dominance-result.json";')
    'run/<run_id>/<unit dir>/manifest.json (archive)'   = @('tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs', 'list.Add(Archive.WriteNew(archiveRoot, RunPath(runId, $"{unitDirectory}/manifest.json"), Read($"{unitDirectory}/manifest.json"), $"manifest-{unitId.ToLowerInvariant()}",')
    'run/<run_id>/<unit dir>/manifest.json (verifier)'  = @('tools/StarPlayer.EvidenceVerifier/Verifier.cs', 'var manifests = unitIdsInPlan.Select((id, i) => RunFile(snapshot, "UNIT_MANIFEST", id, EvidenceWriter.RunPath(runId, $"{unitDirectories[i]}/manifest.json"), family)).ToList();')
    'units/<unit>/manifest.json (Family index path)'    = @('tools/StarPlayer.SimHarness/FamilyOfficial.cs', 'var rel = OfficialFamilyPlan.UnitDirectory(trusted.Unit) + "/manifest.json";')
    'cells/<cell>/manifest.json (Dominance index path)' = @('tools/StarPlayer.SimHarness/DominanceOfficial.cs', 'var rel = DominancePreflight.CellDirectory(cell) + "/manifest.json";')
}

# a governed line: not a // comment, and holds a governed literal or a governed path symbol
$GovernedToken = 'manifest\.json|preflight\.json|"preflight"|"run/|\$"run/|build/simharness/official|family-result\.json|dominance-result\.json|"units/|\$"units/|"cells/|\bRunPath\b|\bUnitPreflightName\b|\bPreflightFile\b|\bPreflightDirectory\b|\bRootRelative\b|\bResultFile\b|\bUnitDirectory\b|\bCellDirectory\b'

# 8ca9705: file -> SHA-256 (UTF-8) of each trimmed governed line (a multiset)
$GovernedLines = [ordered]@{
    'tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs' = @(
        '9364f28a5790e99f783f273a2fb0aa59262975fdb60b8b4ad67efa076f02883a', 'dd0a073742f0a042f8630d2c29bfcb83541bf16a31b11bf29493fc7a5cbc33c5',
        'fae2a3aaec2e5e93b4a67ea15e830a026295fdba961c489521c86b42ed747ddc'
    )
    'tools/StarPlayer.EvidenceVerifier/Verifier.cs' = @(
        '05538d6ec4341c56681b05f88fe347bfd449a095f20d2af31a2fb588766f3a59', '0641b091a3160e6b9a40557e0c188a616b19d7c4197ab488ae1e926a0917aaff',
        '58e0941500cacba64b9f47354f8a22c7aac57d4e680f4f919658ca2b41639fd2', '94366b4ee59c42a43a8ca313c6a36d3ea3e8e8dc189d250acccd611bd8c1d721',
        '950327cc738aa68993e745e16bbd8d1031034df70823a06c3767c7b66f3b6890', 'd197871340e4d505fd7d74d8f7957f0b57da22558e71c52a1e400fd9da9307aa'
    )
    'tools/StarPlayer.SimHarness/DominanceOfficial.cs' = @(
        '0d84bd8a83b88ca0d19c1f9e4ab93790f51dd0a739cf39f6b44335838afad168', '26c74414ffa03f98a7a225f82dfbc7c826d88d4c35ab834af8b239141064aa2d',
        '36e8dde3868f85ebef4c8cb72d10090e42a0400dc1432e9a8184eac300940057', '38f2d8cefd6a4f169ce7e870c1550b197d05b67bb793f35ff55abaa018ece4a7',
        '634a48cc7817cc8d07bd00b0fc4131c7323c7412aedf60cd355184955ecda684', '815f7f67fc8a6deeebd33a751bb1bacb8d0ca967b1d9c4f40c98f69da4048792',
        '87291a4853fe0a254c10c199e82e6fcfcb21bfcbdcf24301834b1d564d8f4652', '97daa4f0a540cd91f705d631226d2c0f485cfb81ffa0ef4fe78fb12896b46226',
        'ad55ad4c819fa16b7eb8b8d9cf64f5af3c1d51fcaad5b00146f350c07da3224d', 'cde5bcc5287ad8b8941166fc1a4caaa4a7779e51218b5ae621fbd1e53d004444',
        '4a4084648aa062a77c1e55d57d57de006819beb2b824c5b9f6bc8efabe97284b', 'ee4eeaee7b0d801271c76693ec0a3b89296825fd3d896aea53945b9337db16a2',
        'faaccfb7b06a74cfe15c13f0179c68f03681f8450a0eb33988036649360945f1'
    )
    'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = @(
        '37d9e2921a5490c4d044855f7a610274530ecf642f266b021d56b7d58c7bec72', '3b22fa1670e5f5693448d1ce2511b823e467fc3d4a6e3044cdded0ac793968e8',
        '3fb6c8a9a46fa8140b87a0c64727c3983074f1e9b4babee490fa416cd0bf4ee9', '639de9d2bd3c2b8eb992d78f740d65942d7f5e572e92038813d5bd4cd739ea5d',
        '7a8f4c7d556474b9599f74240c4b12837693fbd51f1a9bfbf442af9c708e88e8', 'ac763274b8b89ab234ba5e00fefd5fa4f0d1278583f387e5faefcd33e49fdfcc',
        'c1eafc435eacc7527011c49b5d51bff6182a31f597fb41f71da6d8092ec0c6c2', 'cc64e8ac2d3b28f0fe0163a0f6d78acdda072f382eeb22de629ea8afcb1ac648'
    )
    'tools/StarPlayer.SimHarness/FamilyOfficial.cs' = @(
        '127f25ef187099d8c6b204bf4e9adc5c2c0543826aba44f2d8b75dde68a1b2aa', '36e8dde3868f85ebef4c8cb72d10090e42a0400dc1432e9a8184eac300940057',
        '3cb273adf9aac43fda929014ad0157543adf04111772d877d04793c78499be59', '583bac7507372d68767bd10818adf73bbb2d301645dc3ebdc79a447d701fa297',
        '329b017a90ba414fb0aa0ea737d7bd96a93cda215f2b1c428ff2d762195c1bb7', '830e54e949166d7801c7d025147a61ac5667cec74f9964d384be3121f77c3682',
        '9079411fbc2db706afb7b05b7329e8801c691fc7fbc7be5e0a65e4f68f62f802', '9f18737ecdd577d15e4878e2e4338794078487fa108169eb7073a4b2c3872dc2',
        'aca88b365c189b0dbb06144b1ec6105d7363af5928048332ca2aa0396a99f783'
    )
    'tools/StarPlayer.SimHarness/Official.cs' = @(
        '135205acee57fbe4b02c9cf2eaaad057952f9a8941ac3698b9f0b58ccc32e4df', '36fc48bd3a7e4cd9a1a137cb8d6ab1800615ba30323e6d205bef94eaddc5b8c0',
        '5da1fcafc4e3735cb22ead642945419feafe07c735bb636d9d7751e8da5189d8', 'a63302b120fdce44085a76aa0c8e13bfdc2192783a1f0040d7b970ec1c173615',
        'dbb8a6ef876ad444052899b3f0bb7a9e988516a46372b37818f80de5d7db94dd', 'dd7a3e9a7072973f73fbdcc213c84c9956b9ee0c956b4eb8fc4c522c239d7ed3',
        'eaff1c0cddde49d2377745fe2c13d3d362ed08b7bf2dcd8f55c05c5a02d11a01'
    )
    'tools/StarPlayer.SimHarness/Program.cs' = @(
        '2695f38ae960c0750c0560f9b23737da6a77ed3735d1a7680813998d7701a616', 'ecd55e94687c44191442669713f67014044e520d65ae27bcb4425cdc68ee3820',
        'f4af22b8c01a7c9c5f954e8a2186ef96be8e2ee88344fe9c80cc28d281be456b'
    )
    'tools/StarPlayer.SimHarness/RunReport.cs' = @(
        '3a7cfa9d190c72a0b64ee8ef72f390578a6b88a062ef3a9c8a1231f9b716c126'
    )
}

# 8ca9705: SHA-256 of each file holding a governed line, CRLF -> LF (equal to `git show 8ca9705:<file>` for all eight)
$GovernedFiles = [ordered]@{
    'tools/StarPlayer.EvidenceVerifier/RequiredOriginals.cs' = '54167d54354a2513e1ce1aa8f31d8301f992c0bceccbe25cc3cf145f3181a73a'
    'tools/StarPlayer.EvidenceVerifier/Verifier.cs'          = 'eb6ca5d677cfcc97211bee3b28a71de6932bb1c0f68cc5f93682bf814173ec8e'
    'tools/StarPlayer.SimHarness/DominanceOfficial.cs'       = '36e2220290eaf581aa31bc36955cba76b1fd9c76cc15707fc820e00331f981d8'
    'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs' = '4ab7d62d9acd7eaac45143b424434f4d74fdb76c0f8dad3b0e1e45ff0a6d8b87'
    'tools/StarPlayer.SimHarness/FamilyOfficial.cs'          = '4dcddd1d37ba9b3509ed3183c3ff398ed288345b5c43e23108605aecc651a7a9'
    'tools/StarPlayer.SimHarness/Official.cs'                = 'ff211a11fe0588bec4354bfb3d8ca47b17bef496cd8d3d71aa7e89d8787547dd'
    'tools/StarPlayer.SimHarness/Program.cs'                 = '46add7f42169e65a1d1a42b2799f11238c8a96a78c850021969ae43c7f0776cd'
    'tools/StarPlayer.SimHarness/RunReport.cs'               = '1e507fc7ef7bae6cadd050b33653733974416025ec648732d68e778bc476f984'
}
$Scopes = @('src', 'tools/StarPlayer.SimHarness', 'tools/StarPlayer.EvidenceVerifier')

function Get-Sources([string]$root) {
    $list = foreach ($s in $Scopes) {
        $d = Join-Path $root $s
        if (Test-Path -LiteralPath $d) {
            Get-ChildItem -LiteralPath $d -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
                ForEach-Object { $_.FullName.Substring($root.TrimEnd('\').Length + 1).Replace('\', '/') }
        }
    }
    @($list | Sort-Object -CaseSensitive)
}

function Get-Sha([byte[]]$bytes) { -join ([Security.Cryptography.SHA256]::Create().ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) }
$Latin1 = [Text.Encoding]::GetEncoding(28591)   # byte <-> char 1:1, so CRLF -> LF is exact on any encoding

function Invoke-Check([string]$root) {
    $sources = Get-Sources $root
    $text = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
    foreach ($f in $sources) { $text[$f] = [IO.File]::ReadAllText((Join-Path $root $f)) }
    $bad = New-Object System.Collections.Generic.List[string]
    $rows = New-Object System.Collections.Generic.List[string]

    # 1. names
    foreach ($name in $Inventory.Keys) {
        $rx = $Inventory[$name][0]; $want = $Inventory[$name][1]
        $have = New-Object 'System.Collections.Generic.Dictionary[string,int]' ([StringComparer]::Ordinal)
        foreach ($f in $sources) { $n = [regex]::Matches($text[$f], $rx).Count; if ($n) { $have[$f] = $n } }
        foreach ($f in $want.Keys) {
            $got = if ($have.ContainsKey($f)) { $have[$f] } else { 0 }
            if ($got -ne $want[$f]) { $bad.Add("names: $name : $f has $got, table says $($want[$f])") }
        }
        foreach ($f in $have.Keys) { if (-not $want.Contains($f)) { $bad.Add("names: $name : unlisted occurrence in $f ($($have[$f]))") } }
        $rows.Add(("{0,-46} {1}" -f $name, (@($have.Keys | Sort-Object -CaseSensitive | ForEach-Object { "$_=$($have[$_])" }) -join ', ')))
    }

    # 2. definitions and 3. lines, on the same trimmed lines
    $lines = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
    $trimmed = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::Ordinal)
    foreach ($f in $sources) {
        $all = @($text[$f] -split '\r?\n' | ForEach-Object { $_.Trim() })
        $trimmed[$f] = $all
        $gov = @($all | Where-Object { -not $_.StartsWith('//') -and $_ -cmatch $GovernedToken })
        if ($gov.Count) { $lines[$f] = $gov }
    }
    foreach ($d in $Definitions.Keys) {
        $file = $Definitions[$d][0]; $line = $Definitions[$d][1]
        $n = if ($trimmed.ContainsKey($file)) { @($trimmed[$file] | Where-Object { $_ -ceq $line }).Count } else { 0 }
        if ($n -ne 1) { $bad.Add("definitions: $d : the pinned line occurs $n times in $file (want 1): $line") }
    }
    $lineCount = 0
    foreach ($f in $lines.Keys) {
        $lineCount += $lines[$f].Count
        if (-not $GovernedLines.Contains($f)) { foreach ($l in $lines[$f]) { $bad.Add("lines: unlisted governed line in $f : $l") }; continue }
        $want = New-Object System.Collections.Generic.List[string]; foreach ($h in $GovernedLines[$f]) { $want.Add($h) }
        foreach ($l in $lines[$f]) {
            if (-not $want.Remove((Get-Sha ([Text.Encoding]::UTF8.GetBytes($l))))) { $bad.Add("lines: unpinned governed line in $f : $l") }
        }
        foreach ($h in $want) { $bad.Add("lines: pinned governed line missing or changed in $f : sha256 $h") }
    }
    foreach ($f in $GovernedLines.Keys) { if (-not $lines.ContainsKey($f)) { $bad.Add("lines: $f has no governed line (pinned $($GovernedLines[$f].Count))") } }

    # 4. files
    foreach ($f in $GovernedFiles.Keys) {
        $p = Join-Path $root $f
        if (-not (Test-Path -LiteralPath $p)) { $bad.Add("files: $f is missing"); continue }
        $h = Get-Sha ($Latin1.GetBytes($Latin1.GetString([IO.File]::ReadAllBytes($p)).Replace("`r`n", "`n")))
        if ($h -ne $GovernedFiles[$f]) { $bad.Add("files: $f sha256 (CRLF->LF) $h, pinned $($GovernedFiles[$f])") }
    }
    foreach ($f in $lines.Keys) { if (-not $GovernedFiles.Contains($f)) { $bad.Add("files: $f holds governed lines but is not a pinned file") } }

    [pscustomobject]@{ Bad = $bad.ToArray(); Rows = $rows.ToArray(); Files = $sources.Count; Lines = $lineCount; LineFiles = $lines.Count }
}

if ($SelfTest) {
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('g1-names-selftest-' + [guid]::NewGuid().ToString('N'))
    $results = New-Object System.Collections.Generic.List[string]
    try {
        function Fresh {
            if (Test-Path $scratch) { Remove-Item -Recurse -Force $scratch }
            foreach ($f in Get-Sources $Root) { $dst = Join-Path $scratch $f; [void][IO.Directory]::CreateDirectory((Split-Path $dst)); [IO.File]::Copy((Join-Path $Root $f), $dst) }
        }
        function Mutate([string]$rel, [string]$from, [string]$to) {
            $p = Join-Path $scratch $rel; $t = [IO.File]::ReadAllText($p)
            if (-not $t.Contains($from)) { throw "selftest setup: '$from' not in $rel" }
            [IO.File]::WriteAllText($p, ([regex]::new([regex]::Escape($from))).Replace($t, $to, 1))
        }
        function AddFile([string]$rel, [string]$content) { [IO.File]::WriteAllText((Join-Path $scratch $rel), $content) }
        function FlipEol([string]$rel) {
            $p = Join-Path $scratch $rel; $s = $Latin1.GetString([IO.File]::ReadAllBytes($p))
            $s = if ($s.Contains("`r`n")) { $s.Replace("`r`n", "`n") } else { $s.Replace("`n", "`r`n") }
            [IO.File]::WriteAllBytes($p, $Latin1.GetBytes($s))
        }
        $ev = 'tools/StarPlayer.SimHarness/Evidence/EvidenceWriter.cs'; $of = 'tools/StarPlayer.SimHarness/Official.cs'; $vf = 'tools/StarPlayer.EvidenceVerifier/Verifier.cs'
        # name, expected verdict (FAIL = must be caught; PASS = must not be flagged; LIMIT = documented blind spot, stays unflagged), mutation
        $cases = @(
            @('baseline_copy', 'PASS', { }),
            @('verifier_renames_manifest', 'FAIL', { Mutate $vf 'manifest.json"' 'manifest2.json"' }),
            @('producer_renames_manifest', 'FAIL', { Mutate 'tools/StarPlayer.SimHarness/RunReport.cs' 'manifest.json"' 'unit-manifest.json"' }),
            @('verifier_hardcodes_run_prefix', 'FAIL', { Mutate $vf 'EvidenceWriter.RunPath(runId, "")' '$"run/{runId}/"' }),
            @('preflight_renamed', 'FAIL', { Mutate $of '"preflight.json"' '"family-preflight.json"' }),
            @('result_root_moved', 'FAIL', { Mutate $of '"build/simharness/official"' '"build/simharness/official2"' }),
            @('second_dominance_index_literal', 'FAIL', { Mutate $vf 'DominanceOfficial.ResultFile' '"dominance-result.json"' }),
            # codex G1-POLICY-01 counterexamples, then path changes that keep every count
            @('runpath_wrong_segment', 'FAIL', { Mutate $ev '$"run/{runId}/{pathInResultDirectory}"' '$"run/wrong/{runId}/{pathInResultDirectory}"' }),
            @('unit_preflight_extension_txt', 'FAIL', { Mutate $of 'unitId.ToLowerInvariant() + ".json"' 'unitId.ToLowerInvariant() + ".txt"' }),
            @('verifier_manifest_subdir_same_count', 'FAIL', { Mutate $vf '$"{unitDirectories[i]}/manifest.json"' '$"{unitDirectories[i]}/x/manifest.json"' }),
            @('producer_archive_manifest_moved_same_count', 'FAIL', { Mutate $ev 'RunPath(runId, $"{unitDirectory}/manifest.json")' 'RunPath(runId, $"{unitDirectory}/../manifest.json")' }),
            @('unit_preflight_dir_swapped_same_count', 'FAIL', { Mutate $ev '$"{OfficialOutput.PreflightDirectory}/{OfficialOutput.UnitPreflightName(unitId)}"' '$"{OfficialOutput.UnitPreflightName(unitId)}/{OfficialOutput.PreflightDirectory}"' }),
            @('new_file_composes_run_path', 'FAIL', { AddFile 'tools/StarPlayer.EvidenceVerifier/G1Extra.cs' "namespace X;`nstatic class G1Extra { static string P(string id) => StarPlayer.SimHarness.Evidence.EvidenceWriter.RunPath(id, `"x`"); }`n" }),
            @('ungoverned_dataflow_in_governed_file', 'FAIL', { Mutate $vf 'var runId = Text(a.Family(family)["run_id"]);' 'var runId = Text(a.Family(family)["run_id"]) + "-x";' }),
            # no false FAIL: line endings only, and an unrelated file
            @('line_endings_flipped', 'PASS', { foreach ($f in $GovernedFiles.Keys) { FlipEol $f } }),
            @('unrelated_file_edit', 'PASS', { AddFile 'src/StarPlayer.Domain/G1Unrelated.cs' "namespace X;`nstatic class G1Unrelated { const string S = `"summary.json`"; }`n" }),
            # documented blind spot: a name assembled from pieces in a new, ungoverned file
            @('assembled_name_in_new_file', 'LIMIT', { AddFile 'tools/StarPlayer.EvidenceVerifier/G1Assembled.cs' "namespace X;`nstatic class G1Assembled { static string M(string id) => `"ru`" + `"n/`" + id + `"/manifest`" + `".json`"; }`n" })
        )
        foreach ($c in $cases) {
            Fresh; & $c[2]; $r = Invoke-Check $scratch
            $got = if ($r.Bad.Count) { 'FAIL' } else { 'PASS' }
            $ok = ($c[1] -eq 'FAIL' -and $got -eq 'FAIL') -or ($c[1] -ne 'FAIL' -and $got -eq 'PASS')
            $layers = @($r.Bad | ForEach-Object { $_.Split(':')[0] } | Sort-Object -Unique) -join '+'
            $results.Add("$($c[0]) expect=$($c[1]) got=$got layers=$(if ($layers) { $layers } else { '-' }) $(if ($ok) { 'OK' } else { 'WRONG' }) :: $(($r.Bad | Select-Object -First 2) -join ' | ')")
        }
    }
    finally { if (Test-Path $scratch) { Remove-Item -Recurse -Force $scratch } }
    $results | ForEach-Object { Write-Output $_ }
    $wrong = @($results | Where-Object { $_ -match ' WRONG ::' }).Count
    Write-Output "SELFTEST cases=$($results.Count) wrong=$wrong"
    exit $(if ($wrong) { 1 } else { 0 })
}

if (-not (Test-Path -LiteralPath (Join-Path $Root 'StarPlayer.sln'))) { [Console]::Error.WriteLine("usage: '$Root' is not the repository root."); exit 2 }
Write-Output "tool=$PSCommandPath sha256=$(Get-Sha ([IO.File]::ReadAllBytes($PSCommandPath))) powershell=$($PSVersionTable.PSVersion) edition=$($PSVersionTable.PSEdition)"
$head = try { & git -C $Root rev-parse HEAD 2>$null } catch { $null }
Write-Output "target=$Root head=$(if ($head) { $head } else { 'unknown' })"
$r = Invoke-Check $Root
$r.Rows | ForEach-Object { Write-Output $_ }
Write-Output "definitions=$($Definitions.Count) governed_lines=$($r.Lines) in $($r.LineFiles) files, pinned_files=$($GovernedFiles.Count)"
foreach ($b in $r.Bad) { Write-Output "    - $b" }
$testCounts = foreach ($name in $Inventory.Keys) {
    $n = 0; foreach ($f in Get-ChildItem (Join-Path $Root 'tests') -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) { $n += [regex]::Matches([IO.File]::ReadAllText($f.FullName), $Inventory[$name][0]).Count }
    "$($name.Split(' ')[0])=$n"
}
Write-Output "tests (informational, dynamic agreement is the TEST_ONLY end-to-end tests): $($testCounts -join ', ')"
Write-Output "RESULT=$(if ($r.Bad.Count) { 'FAIL' } else { 'PASS' }) production_files=$($r.Files) names=$($Inventory.Count) mismatches=$($r.Bad.Count)"
exit $(if ($r.Bad.Count) { 1 } else { 0 })
