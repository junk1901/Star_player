using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using StarPlayer.Domain.Common;

namespace StarPlayer.SimHarness;

/// <summary>
/// F-C2-A path safety beyond string prefixes: a junction or symlink makes a path that is textually inside a root really live elsewhere. The official seed and output paths are refused when any
/// EXISTING component from the volume root down (every ancestor of the root, the root itself, then each component below it) is a reparse point (link, junction, mount point); a component that does
/// not exist yet is new and cannot be a link. Anything that cannot be inspected is refused (fail-closed). This is a check at the moment of use, not a lock: the official WRITE path uses <see cref="PinnedDirectory"/> (handle-based, atomic creation), which closes the check-to-use window.
/// </summary>
internal static class PathSafety
{
    /// <summary>The first path (the volume root, each ancestor of the root down to the root, then each segment in turn) that is a reparse point or cannot be inspected; null when none is.</summary>
    public static string? FirstReparsePoint(string root, IEnumerable<string> segments)
    {
        var current = Path.GetFullPath(root);
        var ancestors = new List<string>();
        for (var d = new DirectoryInfo(current); d is not null; d = d.Parent)
            ancestors.Insert(0, d.FullName);
        foreach (var path in ancestors)
            switch (IsLinkOrUnreadable(path))
            {
                case null: return null;                                                       // the root does not exist: nothing below it can be read
                case true: return path;
            }

        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            switch (IsLinkOrUnreadable(current))
            {
                case null: return null;                                                       // not there yet: everything below it is new
                case true: return current;
            }
        }

        return null;
    }

    // true: a link (or unreadable, which is refused alike); false: a plain file or directory; null: does not exist.
    private static bool? IsLinkOrUnreadable(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);   // reads the entry itself, so a dangling link is still seen as a link
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return true;
        }
    }
}

/// <summary>
/// F-C2 official output pinned by OS handles (FIX1, OI-01). A path check alone leaves a window between the check and the write in which a parent can be swapped for a junction; this type closes
/// it by never resolving an output path twice:
/// <list type="bullet">
/// <item>Every directory from the volume root down is opened by handle, each one RELATIVE to its parent's handle (<c>NtCreateFile</c> with <c>RootDirectory</c>), with
/// <c>FILE_OPEN_REPARSE_POINT</c>: a junction, symlink or mount point is opened as itself and refused, never followed.</item>
/// <item>Each handle is kept open for the whole run WITHOUT <c>FILE_SHARE_DELETE</c>: while it is held, nobody can open that directory for delete, so it cannot be renamed, moved, deleted or
/// replaced by a link. A directory already open by someone else for delete makes the open fail (fail-closed).</item>
/// <item>The run directory and every file are created relative to their pinned parent handle with <c>FILE_CREATE</c>: creation and the existence check are one atomic operation, an existing
/// name is never reused or overwritten, and of two runs with the same run id exactly one obtains the directory.</item>
/// <item>Each opened or created object is checked through its own handle (not a reparse point, the expected kind, its final path a direct child of its pinned parent's final path) before any
/// byte is written; data is written through the creation handle only.</item>
/// </list>
/// Anything that cannot be established is refused. Only Windows drive-letter roots are supported; elsewhere the official output is refused as unpinnable.
/// </summary>
internal sealed class PinnedDirectory : IDisposable
{
    private const uint FileListDirectory = 0x0001, FileAddFile = 0x0002, FileAddSubdirectory = 0x0004, FileTraverse = 0x0020, FileReadAttributes = 0x0080, Synchronize = 0x0010_0000,
        FileWriteData = 0x0002, FileAppendData = 0x0004, FileWriteAttributes = 0x0100;
    private const uint ShareRead = 1, ShareWrite = 2;                                        // never FILE_SHARE_DELETE (4)
    private const uint FileOpen = 1, FileCreate = 2, FileOpenIf = 3;
    private const uint DirectoryFile = 0x0001, SynchronousIoNonAlert = 0x0020, NonDirectoryFile = 0x0040, OpenReparsePoint = 0x0020_0000;
    private const uint ObjCaseInsensitive = 0x40;
    private const uint AttributeDirectory = 0x10, AttributeReparsePoint = 0x400;
    private const int StatusObjectNameCollision = unchecked((int)0xC0000035), StatusReparsePointEncountered = unchecked((int)0xC0000280),
        StatusObjectNameNotFound = unchecked((int)0xC0000034), StatusObjectPathNotFound = unchecked((int)0xC000003A);
    private const uint FileReadData = 0x0001;

    private readonly SafeFileHandle _handle;
    private readonly IReadOnlyList<SafeFileHandle> _ancestors;

    private PinnedDirectory(SafeFileHandle handle, IReadOnlyList<SafeFileHandle> ancestors, string fullPath) => (_handle, _ancestors, FullPath) = (handle, ancestors, fullPath);

    /// <summary>The real (final) path of the pinned directory.</summary>
    public string FullPath { get; }

    public void Dispose()
    {
        _handle.Dispose();
        foreach (var h in _ancestors.Reverse())
            h.Dispose();
    }

    private static Result<T> Fail<T>(string code, string message) => Result<T>.Failure(new Error(code, message));

    private static Result<T> Fail<T>(Error error) => Result<T>.Failure(error);

    private static bool BadName(string name) => name is "" or "." or ".." || name.IndexOfAny(['\\', '/', ':', '\0']) >= 0;

    /// <summary>
    /// Pins every existing directory from the volume root to <paramref name="root"/>, opens or creates (relative, link-refusing) each of <paramref name="segments"/> but the last, and creates the
    /// last one exclusively. The result holds the whole chain until it is disposed.
    /// </summary>
    public static Result<PinnedDirectory> CreateNew(string root, IReadOnlyList<string> segments) =>
        segments.Count == 0 ? Fail<PinnedDirectory>("Official.output_invalid", "고정할 경로 구성 요소가 올바르지 않습니다.") : Pin(root, segments, FileCreate, true, "Official.output_unpinnable");

    /// <summary>
    /// B1-T11-04 (evidence archive): pins every directory from the volume root through <paramref name="root"/> and <paramref name="segments"/> by handle exactly as <see cref="CreateNew"/> does — relative
    /// opens that refuse links, no FILE_SHARE_DELETE — but the last directory is opened (or, with <paramref name="create"/>, created when absent) instead of created exclusively. While the result is held no
    /// component can be renamed, deleted or swapped for a junction. A missing component without <paramref name="create"/> is <c>Official.output_missing</c>. B1 T11 R4: with <paramref name="createRoot"/>
    /// the missing components of <paramref name="root"/> itself are created the same way: the chain is pinned up to the deepest existing ancestor and every missing component is created relative to its
    /// pinned parent (never a path-based create before the check; the existence probe only chooses where creating starts, every component is still opened and checked by handle).
    /// </summary>
    public static Result<PinnedDirectory> Open(string root, IReadOnlyList<string> segments, bool create, bool createRoot = false)
    {
        if (!create || !createRoot)
            return Pin(root, segments, create ? FileOpenIf : FileOpen, create, "Official.output_missing");
        var existing = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var missing = new List<string>();
        while (!Directory.Exists(existing) && Path.GetDirectoryName(existing) is { } parent)
        {
            missing.Insert(0, Path.GetFileName(existing));
            existing = parent;
        }

        return Pin(existing, [.. missing, .. segments], FileOpenIf, true, "Official.output_missing");
    }

    private static Result<PinnedDirectory> Pin(string root, IReadOnlyList<string> segments, uint lastDisposition, bool createIntermediate, string missingCode)
    {
        if (!OperatingSystem.IsWindows())
            return Fail<PinnedDirectory>("Official.output_unpinnable", "공식 출력 경로를 OS 핸들로 고정할 수 없는 플랫폼입니다(Windows 전용).");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var volume = Path.GetPathRoot(full);
        if (volume is null || volume.Length != 3 || !char.IsAsciiLetter(volume[0]) || volume[1] != ':' || volume[2] != '\\')
            return Fail<PinnedDirectory>("Official.output_unpinnable", $"드라이브 문자 경로가 아닌 루트('{full}')는 고정할 수 없습니다.");
        var rootNames = full[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var names = rootNames.Concat(segments).ToList();
        if (names.Any(BadName))
            return Fail<PinnedDirectory>("Official.output_invalid", "고정할 경로 구성 요소가 올바르지 않습니다.");

        var chain = new List<SafeFileHandle>();
        var ok = false;
        try
        {
            var volumeHandle = Open(null, @"\??\" + volume, FileOpen, true, Access(rootNames.Length == 0));
            if (!volumeHandle.IsSuccess)
                return Fail<PinnedDirectory>(volumeHandle.Error!.Value);
            chain.Add(volumeHandle.Value);
            var volumeFinal = FinalPath(volumeHandle.Value);
            if (volumeFinal is null || !string.Equals(volumeFinal, volume, StringComparison.OrdinalIgnoreCase) || KindProblem(volumeHandle.Value, true) is not null)
                return Fail<PinnedDirectory>("Official.output_reparse_point", $"볼륨 루트 '{volume}'가 실제 볼륨 루트로 확인되지 않습니다(SUBST·링크 등; 실제 '{volumeFinal}').");
            var parentFinal = volumeFinal;
            for (var i = 0; i < names.Count; i++)
            {
                var last = i == names.Count - 1;
                var child = Open(chain[^1], names[i], last ? lastDisposition : i >= rootNames.Length && createIntermediate ? FileOpenIf : FileOpen, true, Access(i >= rootNames.Length - 1), missingCode);
                if (!child.IsSuccess)
                    return Fail<PinnedDirectory>(child.Error!.Value);
                chain.Add(child.Value);
                var checkedChild = CheckChild(child.Value, parentFinal, true);
                if (!checkedChild.IsSuccess)
                    return Fail<PinnedDirectory>(checkedChild.Error!.Value);
                parentFinal = checkedChild.Value;
            }

            var expected = Path.Combine([full, .. segments]);
            if (!string.Equals(parentFinal, expected, StringComparison.OrdinalIgnoreCase))
                return Fail<PinnedDirectory>("Official.output_unpinnable", $"고정한 출력 디렉터리의 실제 경로 '{parentFinal}'가 요청 경로 '{expected}'와 다릅니다.");
            var self = chain[^1];
            chain.RemoveAt(chain.Count - 1);
            ok = true;
            return Result<PinnedDirectory>.Success(new PinnedDirectory(self, chain, parentFinal));
        }
        finally
        {
            if (!ok)
                foreach (var h in chain)
                    h.Dispose();
        }
    }

    /// <summary>Creates a NEW subdirectory relative to this pinned directory (never an existing one, never through a link). The caller disposes it before this directory.</summary>
    public Result<PinnedDirectory> CreateSubdirectory(string name)
    {
        if (BadName(name))
            return Fail<PinnedDirectory>("Official.output_invalid", $"디렉터리 이름 '{name}'가 올바르지 않습니다.");
        var child = Open(_handle, name, FileCreate, true, Access(true));
        if (!child.IsSuccess)
            return Fail<PinnedDirectory>(child.Error!.Value);
        var checkedChild = CheckChild(child.Value, FullPath, true);
        if (checkedChild.IsSuccess)
            return Result<PinnedDirectory>.Success(new PinnedDirectory(child.Value, [], checkedChild.Value));
        child.Value.Dispose();
        return Fail<PinnedDirectory>(checkedChild.Error!.Value);
    }

    /// <summary>Creates a NEW file relative to this pinned directory, verifies it through its handle, then writes <paramref name="bytes"/> through that handle; returns their SHA-256.</summary>
    public Result<string> WriteNewFile(string name, byte[] bytes)
    {
        if (BadName(name))
            return Fail<string>("Official.output_invalid", $"파일 이름 '{name}'가 올바르지 않습니다.");
        var created = Open(_handle, name, FileCreate, false, FileWriteData | FileAppendData | FileWriteAttributes | FileReadAttributes | Synchronize);
        if (!created.IsSuccess)
            return Fail<string>(created.Error!.Value);
        using var stream = new FileStream(created.Value, FileAccess.Write, 1, false);           // owns the handle from here on
        var checkedFile = CheckChild(created.Value, FullPath, false);
        if (!checkedFile.IsSuccess)
            return Fail<string>(checkedFile.Error!.Value);
        stream.Write(bytes);
        stream.Flush(true);
        return Result<string>.Success(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    /// <summary>B1-T11-04: opens an EXISTING file relative to this pinned directory (a link is opened as itself and refused), checks it through its handle and reads it through that handle with no write or
    /// delete sharing. A missing file is <c>Official.output_missing</c>.</summary>
    public Result<byte[]> ReadFile(string name)
    {
        if (BadName(name))
            return Fail<byte[]>("Official.output_invalid", $"파일 이름 '{name}'가 올바르지 않습니다.");
        var opened = Open(_handle, name, FileOpen, false, FileReadData | FileReadAttributes | Synchronize, "Official.output_missing", ShareRead);
        if (!opened.IsSuccess)
            return Fail<byte[]>(opened.Error!.Value);
        using var stream = new FileStream(opened.Value, FileAccess.Read, 1, false);             // owns the handle from here on
        var checkedFile = CheckChild(opened.Value, FullPath, false);
        if (!checkedFile.IsSuccess)
            return Fail<byte[]>(checkedFile.Error!.Value);
        using var m = new MemoryStream();
        stream.CopyTo(m);
        return Result<byte[]>.Success(m.ToArray());
    }

    private static uint Access(bool addChildren) => FileListDirectory | FileTraverse | FileReadAttributes | Synchronize | (addChildren ? FileAddFile | FileAddSubdirectory : 0);

    /// <summary>The handle is a plain object of the expected kind and a direct child of the pinned parent; returns its final path.</summary>
    private static Result<string> CheckChild(SafeFileHandle handle, string parentFinal, bool directory)
    {
        if (KindProblem(handle, directory) is { } problem)
            return Fail<string>(problem);
        var final = FinalPath(handle);
        var prefix = parentFinal.EndsWith('\\') ? parentFinal : parentFinal + "\\";             // a volume root already ends with its separator
        return final is not null && final.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && final.Length > prefix.Length && final.IndexOf('\\', prefix.Length) < 0
            ? Result<string>.Success(final)
            : Fail<string>("Official.output_unpinnable", $"열거나 만든 객체의 실제 경로 '{final}'가 고정된 상위 '{parentFinal}'의 직접 하위가 아닙니다.");
    }

    private static Error? KindProblem(SafeFileHandle handle, bool directory)
    {
        if (!GetFileInformationByHandleEx(handle, 9 /* FileAttributeTagInfo */, out var info, (uint)Marshal.SizeOf<FileAttributeTagInfo>()))
            return new Error("Official.output_unpinnable", $"핸들 속성을 읽을 수 없습니다(Win32 {Marshal.GetLastWin32Error()}).");
        if ((info.FileAttributes & AttributeReparsePoint) != 0)
            return new Error("Official.output_reparse_point", $"공식 출력 경로에 링크/Junction(reparse point, tag 0x{info.ReparseTag:X8})이 있어 거부합니다.");
        return ((info.FileAttributes & AttributeDirectory) != 0) == directory ? null : new Error("Official.output_unpinnable", directory ? "디렉터리가 아닌 항목입니다." : "파일이 아닌 항목입니다.");
    }

    /// <summary>The handle's normalized DOS path without the <c>\\?\</c> prefix; null if it cannot be read or is not a drive-letter path.</summary>
    private static string? FinalPath(SafeFileHandle handle)
    {
        var buffer = new char[1024];
        var n = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (n == 0 || n >= buffer.Length)
            return null;
        var path = new string(buffer, 0, (int)n);
        return path.StartsWith(@"\\?\", StringComparison.Ordinal) && path.Length >= 7 && path[5] == ':' ? path[4..] : null;
    }

    /// <summary>NtCreateFile relative to <paramref name="parent"/> (absolute when null), refusing to follow a reparse point, sharing read / write but never delete.</summary>
    private static Result<SafeFileHandle> Open(SafeFileHandle? parent, string name, uint disposition, bool directory, uint access, string missingCode = "Official.output_unpinnable", uint share = ShareRead | ShareWrite)
    {
        var buffer = Marshal.StringToHGlobalUni(name);
        var unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        var added = false;
        try
        {
            parent?.DangerousAddRef(ref added);
            Marshal.StructureToPtr(new UnicodeString { Length = checked((ushort)(name.Length * 2)), MaximumLength = checked((ushort)(name.Length * 2 + 2)), Buffer = buffer }, unicode, false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent?.DangerousGetHandle() ?? IntPtr.Zero, ObjectName = unicode, Attributes = ObjCaseInsensitive,
            };
            var status = NtCreateFile(out var handle, access, ref attributes, out _, IntPtr.Zero, 0, share, disposition,
                (directory ? DirectoryFile : NonDirectoryFile) | SynchronousIoNonAlert | OpenReparsePoint, IntPtr.Zero, 0);
            if (status >= 0)
                return Result<SafeFileHandle>.Success(handle);
            handle.Dispose();
            return status == StatusObjectNameCollision ? Fail<SafeFileHandle>("Official.output_exists", $"'{name}'가 이미 있습니다(덮어쓰거나 재사용하지 않음).")
                : status == StatusReparsePointEncountered ? Fail<SafeFileHandle>("Official.output_reparse_point", $"'{name}'의 상위 디렉터리가 고정 후 링크/마운트 지점으로 바뀌어 거부합니다(NTSTATUS 0x{status:X8}).")
                : status is StatusObjectNameNotFound or StatusObjectPathNotFound ? Fail<SafeFileHandle>(missingCode, $"'{name}'가 없습니다(NTSTATUS 0x{status:X8}).")
                : Fail<SafeFileHandle>("Official.output_unpinnable", $"'{name}'를 고정해 열거나 만들 수 없습니다(NTSTATUS 0x{status:X8}).");
        }
        finally
        {
            if (added)
                parent!.DangerousRelease();
            Marshal.FreeHGlobal(unicode);
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint desiredAccess, ref ObjectAttributes objectAttributes, out IoStatusBlock ioStatus, IntPtr allocationSize,
        uint fileAttributes, uint shareAccess, uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int infoClass, out FileAttributeTagInfo info, uint size);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, [Out] char[] buffer, uint size, uint flags);
}
