using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using StarPlayer.SimHarness;

namespace StarPlayer.SimHarness.Tests;

/// <summary>
/// FIX1 (OI-01): the handle-pinned official output against attacks a share mode cannot stop. The run directory is EMPTY right after its exclusive creation, so an attacker who can write
/// attributes can turn that very directory into a mount point (<c>FSCTL_SET_REPARSE_POINT</c> needs no delete access, so the pin's missing <c>FILE_SHARE_DELETE</c> does not prevent it). Observed
/// on NTFS: a handle-relative create under the converted directory fails with <c>STATUS_REPARSE_POINT_ENCOUNTERED</c> (the pin opens with <c>FILE_OPEN_REPARSE_POINT</c>), so the attack can only
/// make the run fail closed; nothing reaches the mount point's target. Everything happens in a TEMP directory that is removed afterwards.
/// </summary>
public sealed class PinnedDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "simharness-tests", "pinned-" + Guid.NewGuid().ToString("N"));

    public PinnedDirectoryTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        for (var attempt = 0; ; attempt++)
            try
            {
                if (Directory.Exists(_root))
                    Directory.Delete(_root, true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 10)
            {
                Thread.Sleep(200);
            }
    }

    [Fact]
    public void A_held_output_directory_turned_into_a_mount_point_fails_closed_and_never_writes_to_the_target()
    {
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Path.Combine(_root, "repo"));
        using var run = H.Ok(PinnedDirectory.CreateNew(Path.Combine(_root, "repo"), ["out", "run"]));
        var path = Path.Combine(_root, "repo", "out", "run");
        Assert.Equal(path, run.FullPath, ignoreCase: true);

        var converted = MountPoint(path, outside);                                            // the attacker converts the held, still empty run directory
        Assert.True(converted is null, $"the attack could not be staged: {converted}");
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint));

        var written = run.WriteNewFile("manifest.json", Encoding.UTF8.GetBytes("{}"));
        var sub = run.CreateSubdirectory("cells");

        Assert.Empty(Directory.GetFileSystemEntries(outside));                               // nothing reached the mount point's target
        Assert.Equal("Official.output_reparse_point", written.Error?.Code);                   // refused, never followed
        Assert.Equal("Official.output_reparse_point", sub.Error?.Code);
    }

    [Fact]
    public void Creation_is_exclusive_and_a_link_in_the_chain_is_refused()
    {
        Directory.CreateDirectory(Path.Combine(_root, "repo"));
        using (var first = H.Ok(PinnedDirectory.CreateNew(Path.Combine(_root, "repo"), ["out", "run"])))
        {
            Assert.Equal("Official.output_exists", PinnedDirectory.CreateNew(Path.Combine(_root, "repo"), ["out", "run"]).Error!.Value.Code);   // exclusive while held ...
            Assert.True(first.WriteNewFile("a.json", [1]).IsSuccess);
            Assert.Equal("Official.output_exists", first.WriteNewFile("a.json", [2]).Error!.Value.Code);                               // ... and files are never overwritten
        }

        Assert.Equal("Official.output_exists", PinnedDirectory.CreateNew(Path.Combine(_root, "repo"), ["out", "run"]).Error!.Value.Code);       // ... and after release
        Assert.Equal([1], File.ReadAllBytes(Path.Combine(_root, "repo", "out", "run", "a.json")));
        Assert.Equal("Official.output_invalid", PinnedDirectory.CreateNew(Path.Combine(_root, "repo"), ["out", ".."]).Error!.Value.Code);

        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        Directory.CreateDirectory(Path.Combine(_root, "repo2"));
        Assert.Null(MountPoint(Path.Combine(_root, "repo2", "out"), outside, create: true));   // an existing junction in the chain
        Assert.Equal("Official.output_reparse_point", PinnedDirectory.CreateNew(Path.Combine(_root, "repo2"), ["out", "run"]).Error!.Value.Code);
        Assert.Empty(Directory.GetFileSystemEntries(outside));
        Directory.Delete(Path.Combine(_root, "repo2", "out"));                                 // removes the junction only
    }

    // ---------------------------------------------------------------- staging a mount point (test only)

    private const uint FileWriteAttributes = 0x0100, FileReadAttributes = 0x0080, ShareAll = 7, OpenExisting = 3, BackupSemantics = 0x0200_0000, OpenReparsePointFlag = 0x0020_0000;
    private const uint FsctlSetReparsePoint = 0x000900A4, MountPointTag = 0xA0000003;

    /// <summary>Sets a mount-point (junction) reparse point on <paramref name="directory"/> (creating it first when asked) through a handle opened with write-attribute access only. Null on success.</summary>
    internal static string? MountPoint(string directory, string target, bool create = false)
    {
        if (create)
            Directory.CreateDirectory(directory);
        var substitute = @"\??\" + Path.GetFullPath(target);
        var print = Path.GetFullPath(target);
        var path = Encoding.Unicode.GetBytes(substitute + "\0" + print + "\0");
        var data = new byte[8 + path.Length];                                                 // SubstituteNameOffset/Length, PrintNameOffset/Length + PathBuffer
        BitConverter.GetBytes((ushort)0).CopyTo(data, 0);
        BitConverter.GetBytes((ushort)(substitute.Length * 2)).CopyTo(data, 2);
        BitConverter.GetBytes((ushort)((substitute.Length + 1) * 2)).CopyTo(data, 4);
        BitConverter.GetBytes((ushort)(print.Length * 2)).CopyTo(data, 6);
        path.CopyTo(data, 8);
        var buffer = new byte[8 + data.Length];                                               // ReparseTag, ReparseDataLength, Reserved + data
        BitConverter.GetBytes(MountPointTag).CopyTo(buffer, 0);
        BitConverter.GetBytes((ushort)data.Length).CopyTo(buffer, 4);
        data.CopyTo(buffer, 8);

        using var handle = CreateFileW(directory, FileWriteAttributes | FileReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePointFlag, IntPtr.Zero);
        if (handle.IsInvalid)
            return $"open failed (Win32 {Marshal.GetLastWin32Error()})";
        return DeviceIoControl(handle, FsctlSetReparsePoint, buffer, (uint)buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero) ? null : $"FSCTL_SET_REPARSE_POINT failed (Win32 {Marshal.GetLastWin32Error()})";
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, byte[] input, uint inputSize, IntPtr output, uint outputSize, out uint returned, IntPtr overlapped);
}
