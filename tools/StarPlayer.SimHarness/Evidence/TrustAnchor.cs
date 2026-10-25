using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace StarPlayer.SimHarness.Evidence;

/// <summary>Which part of the anchor path a descriptor belongs to (NSC-4 as amended by OWNER decision 2, 2026-10-24).</summary>
public enum AclTarget
{
    /// <summary>The anchor file: no change of any kind outside the TCB.</summary>
    AnchorFile,

    /// <summary>The directory that holds the anchor: no change of any kind outside the TCB (adding a file or subdirectory included).</summary>
    AnchorDirectory,

    /// <summary>A directory above it, up to the drive root: nothing outside the TCB may delete, rename, replace or re-permission a path component (DELETE, FILE_DELETE_CHILD, WRITE_DAC, WRITE_OWNER).</summary>
    Ancestor,
}

/// <summary>
/// NSC-4 for §16 T2 — option B (OWNER decision 2026-10-24) with the minimal correction of OWNER decision 2 (2026-10-24, <c>B1-T00_OWNER_DECISION_2.md</c>). The TCB is SYSTEM, BUILTIN\Administrators and
/// NT SERVICE\TrustedInstaller. For every component of the anchor path (drive root → anchor file): its owner is in the TCB; a NULL DACL or an ACE type other than plain allow / deny is refused; an allow ACE
/// that APPLIES to the component (not INHERIT_ONLY; inherited ACEs included) must not give a SID outside the TCB a right forbidden for that component's <see cref="AclTarget"/> (generic rights mapped first);
/// such an ACE naming CREATOR OWNER / CREATOR GROUP / OWNER RIGHTS is refused as ambiguous. An INHERIT_ONLY ACE is not ignored: every inheritable allow ACE of a component is also judged as it would apply to the
/// next component of the path (unless that component's DACL is protected), with CREATOR OWNER / OWNER RIGHTS replaced by that component's owner and CREATOR GROUP by its group. Deny ACEs never relax
/// anything; a descriptor that cannot be read is a violation (fail-closed).
/// </summary>
[SupportedOSPlatform("windows")]
public static class AnchorAcl
{
    public static readonly IReadOnlySet<string> Tcb = new HashSet<string>(StringComparer.Ordinal)
    {
        "S-1-5-18", "S-1-5-32-544", "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464",
    };

    private const string CreatorOwner = "S-1-3-0", CreatorGroup = "S-1-3-1", OwnerRights = "S-1-3-4";
    private const int GenericRead = unchecked((int)0x80000000), GenericWrite = 0x40000000, GenericExecute = 0x20000000, GenericAll = 0x10000000, MaximumAllowed = 0x02000000;
    private const int FileRead = 0x00120089, FileWrite = 0x00120116, FileExecute = 0x001200A0, FileAll = 0x001F01FF;
    private const int WriteData = 0x2, AppendData = 0x4, WriteEa = 0x10, DeleteChild = 0x40, WriteAttributes = 0x100, Delete = 0x10000, WriteDac = 0x40000, WriteOwner = 0x80000;

    /// <summary>
    /// The rights that count as a change of the component. File: data, append, EA, attributes, delete, DACL, owner. Anchor directory: the same (add file / add subdirectory are the same bits) plus delete
    /// child. Ancestor: only what can delete, rename, replace or re-permission an existing component — DELETE, FILE_DELETE_CHILD, WRITE_DAC, WRITE_OWNER (adding a new name cannot displace an existing one;
    /// reparse points are refused separately on every read).
    /// </summary>
    public static int Forbidden(AclTarget target) => target switch
    {
        AclTarget.AnchorFile => WriteData | AppendData | WriteEa | WriteAttributes | Delete | WriteDac | WriteOwner,
        AclTarget.AnchorDirectory => WriteData | AppendData | WriteEa | WriteAttributes | Delete | WriteDac | WriteOwner | DeleteChild,
        _ => Delete | DeleteChild | WriteDac | WriteOwner,
    };

    public static int Map(int mask)
    {
        var m = mask & ~(GenericRead | GenericWrite | GenericExecute | GenericAll | MaximumAllowed);
        if ((mask & GenericRead) != 0) m |= FileRead;
        if ((mask & GenericWrite) != 0) m |= FileWrite;
        if ((mask & GenericExecute) != 0) m |= FileExecute;
        if ((mask & (GenericAll | MaximumAllowed)) != 0) m |= FileAll;
        return m;
    }

    private static string? Ace(string sid, int mask, AclTarget target, string where)
    {
        var bad = Map(mask) & Forbidden(target);
        return !Tcb.Contains(sid) && bad != 0 ? $"TCB 밖 주체 {sid}에게 변경 권한 0x{bad:x}을(를) 주는 ACE가 있습니다({where})." : null;
    }

    /// <summary>The first reason this descriptor lets someone outside the TCB change the component, or null. Only ACEs that apply to the component itself are judged here.</summary>
    public static string? Violation(RawSecurityDescriptor sd, AclTarget target)
    {
        if (sd.Owner is null || !Tcb.Contains(sd.Owner.Value))
            return $"소유자 {sd.Owner?.Value ?? "(없음)"}가 TCB(SYSTEM·Administrators·TrustedInstaller)가 아닙니다.";
        if (sd.DiscretionaryAcl is null || (sd.ControlFlags & ControlFlags.DiscretionaryAclPresent) == 0)
            return "NULL DACL(모든 접근 허용)입니다.";
        foreach (GenericAce ace in sd.DiscretionaryAcl)
        {
            if (ace is not CommonAce c || ace.AceType is not (AceType.AccessAllowed or AceType.AccessDenied))
                return $"해석하지 않는 ACE 형식 {ace.AceType}이 있습니다.";
            if (c.AceType == AceType.AccessDenied || (c.AceFlags & AceFlags.InheritOnly) != 0)
                continue;                                                                              // a deny never relaxes; an inherit-only ACE is judged on the child (Inherits)
            var sid = c.SecurityIdentifier.Value;
            if (sid is CreatorOwner or CreatorGroup or OwnerRights)
                return $"이 객체에 적용되는 ACE가 {sid}(CREATOR OWNER·CREATOR GROUP·OWNER RIGHTS)를 가리켜 해석이 모호합니다.";
            if (Ace(sid, c.AccessMask, target, (c.AceFlags & AceFlags.Inherited) != 0 ? "상속 ACE" : "명시 ACE") is { } v)
                return v;
        }

        return null;
    }

    /// <summary>
    /// The first reason an inheritable allow ACE of <paramref name="parent"/> would, once inherited, let someone outside the TCB change <paramref name="child"/> (the next path component), or null. Nothing is
    /// inherited into a protected DACL; a file inherits object-inherit ACEs, a directory container-inherit ACEs; CREATOR OWNER / OWNER RIGHTS become the child's owner, CREATOR GROUP its group.
    /// </summary>
    public static string? Inherits(RawSecurityDescriptor parent, RawSecurityDescriptor child, bool childIsDirectory, AclTarget childTarget)
    {
        if ((child.ControlFlags & ControlFlags.DiscretionaryAclProtected) != 0 || parent.DiscretionaryAcl is null)
            return null;
        foreach (GenericAce ace in parent.DiscretionaryAcl)
        {
            if (ace is not CommonAce { AceType: AceType.AccessAllowed } c)
                continue;                                                                              // other types are already refused by Violation(parent)
            if ((c.AceFlags & (childIsDirectory ? AceFlags.ContainerInherit : AceFlags.ObjectInherit)) == 0)
                continue;
            var sid = c.SecurityIdentifier.Value switch
            {
                CreatorOwner or OwnerRights => child.Owner?.Value ?? "(없음)",
                CreatorGroup => child.Group?.Value ?? "(없음)",
                var s => s,
            };
            if (Ace(sid, c.AccessMask, childTarget, $"상위에서 상속될 ACE {c.SecurityIdentifier.Value}") is { } v)
                return v;
        }

        return null;
    }

    /// <summary>The descriptor of a file or directory as stored (owner, group, DACL).</summary>
    [SupportedOSPlatform("windows")]
    public static RawSecurityDescriptor Read(string path, bool directory)
    {
        const AccessControlSections sections = AccessControlSections.Owner | AccessControlSections.Group | AccessControlSections.Access;
        FileSystemSecurity s = directory ? new DirectoryInfo(path).GetAccessControl(sections) : new FileInfo(path).GetAccessControl(sections);
        return new RawSecurityDescriptor(s.GetSecurityDescriptorBinaryForm(), 0);
    }

    /// <summary>The judgement of one path given its components' descriptors from the drive root down to the anchor file (the last entry); the first violation with its path, or null.</summary>
    public static string? CheckChain(IReadOnlyList<(string Path, RawSecurityDescriptor Sd)> chain)
    {
        for (var i = 0; i < chain.Count; i++)
        {
            var target = i == chain.Count - 1 ? AclTarget.AnchorFile : i == chain.Count - 2 ? AclTarget.AnchorDirectory : AclTarget.Ancestor;
            if (Violation(chain[i].Sd, target) is { } v)
                return $"{chain[i].Path}: {v}";
            if (i > 0 && Inherits(chain[i - 1].Sd, chain[i].Sd, childIsDirectory: i < chain.Count - 1, target) is { } iv)
                return $"{chain[i].Path}: {iv}";
        }

        return null;
    }

    /// <summary>The real descriptors of the file and every parent directory up to and including the drive root; any read failure is a violation (fail-closed).</summary>
    [SupportedOSPlatform("windows")]
    public static string? CheckPath(string file)
    {
        var full = Path.GetFullPath(file);
        var paths = new List<string> { full };
        for (var d = new DirectoryInfo(full).Parent; d is not null; d = d.Parent)
            paths.Add(d.FullName);
        paths.Reverse();
        var chain = new List<(string, RawSecurityDescriptor)>();
        for (var i = 0; i < paths.Count; i++)
        {
            try
            {
                chain.Add((paths[i], Read(paths[i], directory: i < paths.Count - 1)));
            }
            catch (Exception e)
            {
                return $"{paths[i]}: 보안 설명자를 읽을 수 없어 신뢰를 입증할 수 없습니다({e.GetType().Name}).";
            }
        }

        return CheckChain(chain);
    }

    /// <summary>True when this process runs with BUILTIN\Administrators enabled (elevated): the verifier then refuses to start (NSC-4 6).</summary>
    [SupportedOSPlatform("windows")]
    public static bool Elevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

/// <summary>A trust anchor read once from the owner-controlled location: its parsed content, the exact bytes and their SHA-256 (§16 T1/T2, R03-1).</summary>
public sealed record TrustAnchorV1(JObject Anchor, byte[] Raw, string RawSha256);

[SupportedOSPlatform("windows")]
public static class TrustAnchorReader
{
    private static EvidenceException Untrusted(string cause, string message) => EvidenceCodes.Fail(EvidenceCodes.ApprovalMismatch, cause, "", "trust_bootstrap", message);

    /// <summary>
    /// §16 T1/T2: the anchor path is outside the repository and evidence roots, has no reparse point, is TCB-only (NSC-4 B) and the process is not elevated; the file is opened ONCE without write sharing,
    /// and the same bytes are hashed and parsed. A missing anchor is <c>Trust.anchor_missing</c>, every other failure <c>Trust.anchor_untrusted</c> (both <c>Evidence.approval_mismatch</c>).
    /// <paramref name="acl"/> / <paramref name="elevated"/> are seams for tests; production uses the real Windows checks.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static TrustAnchorV1 Read(string anchorPath, string repositoryRoot, string evidenceRoot, Func<string, string?>? acl = null, Func<bool>? elevated = null)
    {
        var full = Path.GetFullPath(anchorPath);
        if (!File.Exists(full))
            throw Untrusted("Trust.anchor_missing", $"신뢰 앵커가 없습니다: '{full}'.");
        foreach (var root in new[] { repositoryRoot, evidenceRoot })
            if (full.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                throw Untrusted("Trust.anchor_untrusted", $"앵커가 저장소·증거 루트 안에 있습니다: '{root}' (N25).");
        var drive = Path.GetPathRoot(full)!;
        if (PathSafety.FirstReparsePoint(drive, full[drive.Length..].Split('\\')) is { } link)
            throw Untrusted("Trust.anchor_untrusted", $"앵커 경로에 Reparse Point가 있습니다: '{link}'.");
        if ((elevated ?? AnchorAcl.Elevated)())
            throw Untrusted("Trust.anchor_untrusted", "검증기가 관리자 권한(Elevated)으로 실행되었습니다(NSC-4: 관리자 권한 우회 운영 금지).");
        if ((acl ?? AnchorAcl.CheckPath)(full) is { } why)
            throw Untrusted("Trust.anchor_untrusted", $"앵커 ACL이 TCB 전용이 아닙니다(NSC-4 B): {why}");
        byte[] raw;
        using (var s = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            using var m = new MemoryStream();
            s.CopyTo(m);
            raw = m.ToArray();
        }

        // a missing evidence_reader_sids stays Evidence.schema_invalid, no [] default (AT-14n)
        return new TrustAnchorV1(ApprovalFormats.ReadAnchor(raw), raw, Archive.Sha(raw));
    }
}
