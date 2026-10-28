// Handle-bound helpers for tools/b1-ops (loaded with Add-Type -Path by g5-acl.ps1, b4-candidates.ps1 and g5-pins.ps1).
// Every file open uses FILE_FLAG_OPEN_REPARSE_POINT (a reparse point is opened, never followed) and never shares FILE_SHARE_DELETE, so while
// a handle is held its object cannot be renamed, deleted or replaced; checks and changes go through the same handle (no path re-resolution).
// ModuleReader does the same for processes: identity check and module list through one process handle.
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace B1Ops
{
    public sealed class Info
    {
        public uint Attributes; public uint VolumeSerial; public ulong FileId; public uint Links; public long Size;
        public bool IsDirectory { get { return (Attributes & 0x10) != 0; } }
        public bool IsReparse { get { return (Attributes & 0x400) != 0; } }
    }

    public static class Native
    {
        public const uint ReadControl = 0x00020000, WriteDac = 0x00040000, WriteOwner = 0x00080000, ReadAttributes = 0x80, ReadData = 0x1;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]                // FILETIME fields are 4-byte aligned in BY_HANDLE_FILE_INFORMATION
        private struct ByHandleInfo
        {
            public uint Attributes; public long Created, Accessed, Written; public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes { public int Length; public IntPtr Descriptor; public int Inherit; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle h, out ByHandleInfo info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle h, [Out] char[] buffer, uint size, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateDirectoryW(string name, ref SecurityAttributes security);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr sd, out uint length);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertSecurityDescriptorToStringSecurityDescriptorW(IntPtr sd, uint revision, uint info, out IntPtr sddl, out uint length);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetSecurityDescriptorDacl(IntPtr sd, out bool present, out IntPtr dacl, out bool defaulted);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetSecurityDescriptorOwner(IntPtr sd, out IntPtr owner, out bool defaulted);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetSecurityDescriptorControl(IntPtr sd, out ushort control, out uint revision);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetSecurityDescriptorControl(IntPtr sd, ushort mask, ushort bits);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetKernelObjectSecurity(SafeFileHandle h, uint info, IntPtr sd);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformationByHandleW(SafeFileHandle h, char[] name, uint nameSize, out uint serial, out uint maxComponent, out uint flags, [Out] char[] fsName, uint fsNameSize);
        [DllImport("advapi32.dll")]
        private static extern uint GetSecurityInfo(SafeFileHandle h, int type, uint info, out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr sd);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr p);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool LookupPrivilegeValueW(string system, string name, out long luid);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivilege state, uint length, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr h);

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct TokenPrivilege { public uint Count; public long Luid; public uint Attributes; }

        private static string Long(string path) { return path.StartsWith(@"\\?\") ? path : @"\\?\" + path; }

        /// <summary>
        /// Opens a file or directory (reparse point not followed, no delete sharing). FILE_READ_DATA / FILE_LIST_DIRECTORY is always requested:
        /// a handle with only metadata rights (READ_CONTROL, WRITE_DAC, attributes) takes no part in share-access checks, so it would not stop a
        /// rename or delete of the object; with data access the missing FILE_SHARE_DELETE does. Throws Win32Exception with the error code.
        /// </summary>
        public static SafeFileHandle Open(string path, uint access)
        {
            SafeFileHandle h = CreateFileW(Long(path), access | ReadAttributes | ReadData, 0x1 | 0x2, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero);
            if (h.IsInvalid)
            {
                int e = Marshal.GetLastWin32Error();
                throw new Win32Exception(e, "open '" + path + "': " + new Win32Exception(e).Message + " (" + e + ")");
            }
            return h;
        }

        public static Info Query(SafeFileHandle h)
        {
            ByHandleInfo i;
            if (!GetFileInformationByHandle(h, out i)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new Info { Attributes = i.Attributes, VolumeSerial = i.VolumeSerial, FileId = ((ulong)i.IndexHigh << 32) | i.IndexLow, Links = i.Links, Size = ((long)i.SizeHigh << 32) | i.SizeLow };
        }

        /// <summary>The path the object behind the handle really has (DOS form, no \\?\ prefix).</summary>
        public static string FinalPath(SafeFileHandle h)
        {
            char[] b = new char[32768];
            uint n = GetFinalPathNameByHandleW(h, b, (uint)b.Length, 0);
            if (n == 0 || n >= b.Length) throw new Win32Exception(Marshal.GetLastWin32Error());
            string s = new string(b, 0, (int)n);
            if (s.StartsWith(@"\\?\UNC\")) return @"\\" + s.Substring(8);
            return s.StartsWith(@"\\?\") ? s.Substring(4) : s;
        }

        /// <summary>Owner and DACL of the object behind the handle as SDDL.</summary>
        public static string GetSddl(SafeFileHandle h)
        {
            IntPtr o, g, d, s, sd;
            uint rc = GetSecurityInfo(h, 1, 0x1 | 0x4, out o, out g, out d, out s, out sd);
            if (rc != 0) throw new Win32Exception((int)rc);
            try
            {
                IntPtr str; uint len;
                if (!ConvertSecurityDescriptorToStringSecurityDescriptorW(sd, 1, 0x1 | 0x4, out str, out len)) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { return Marshal.PtrToStringUni(str); } finally { LocalFree(str); }
            }
            finally { LocalFree(sd); }
        }

        /// <summary>
        /// Writes the DACL (and the owner when asked) of the SDDL to the object behind the handle exactly as given (SetKernelObjectSecurity):
        /// no inherited ACE is recomputed from a parent and nothing is propagated to children, so only this one, already checked object
        /// changes (G5-FIX-01: SetSecurityInfo walks the children by name and would reach an object that has another link elsewhere).
        /// The caller supplies inherited ACEs itself (ID flag) and the protected / auto-inherited flags come from the SDDL.
        /// </summary>
        public static void SetSddlExact(SafeFileHandle h, string sddl, bool owner)
        {
            IntPtr sd; uint len;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out sd, out len)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                IntPtr o, d; bool present, defaulted; ushort control; uint rev;
                if (owner) { GetSecurityDescriptorOwner(sd, out o, out defaulted); if (o == IntPtr.Zero) throw new ArgumentException("SDDL has no owner"); }
                GetSecurityDescriptorDacl(sd, out present, out d, out defaulted);
                if (!present || d == IntPtr.Zero) throw new ArgumentException("SDDL has no DACL");
                GetSecurityDescriptorControl(sd, out control, out rev);
                // SE_DACL_AUTO_INHERITED is only kept by the object manager when SE_DACL_AUTO_INHERIT_REQ accompanies it
                ushort ai = (ushort)((control & 0x0400) != 0 ? 0x0500 : 0);
                if (!SetSecurityDescriptorControl(sd, 0x0500, ai)) throw new Win32Exception(Marshal.GetLastWin32Error());
                uint info = (owner ? 0x1u : 0u) | 0x4 | ((control & 0x1000) != 0 ? 0x80000000u : 0x20000000u);
                if (!SetKernelObjectSecurity(h, info, sd)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { LocalFree(sd); }
        }

        /// <summary>File system name of the volume behind the handle (e.g. NTFS).</summary>
        public static string FileSystem(SafeFileHandle h)
        {
            char[] fs = new char[261]; uint serial, maxLen, flags;
            if (!GetVolumeInformationByHandleW(h, null, 0, out serial, out maxLen, out flags, fs, (uint)fs.Length)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return new string(fs).TrimEnd('\0');
        }

        /// <summary>Creates one directory that is born with the given security descriptor. False if the name already existed (nothing created).</summary>
        public static bool CreateDirectory(string path, string sddl)
        {
            IntPtr sd; uint len;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out sd, out len)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                SecurityAttributes sa = new SecurityAttributes { Length = Marshal.SizeOf(typeof(SecurityAttributes)), Descriptor = sd, Inherit = 0 };
                if (CreateDirectoryW(Long(path), ref sa)) return true;
                int e = Marshal.GetLastWin32Error();
                if (e == 183) return false;
                throw new Win32Exception(e);
            }
            finally { LocalFree(sd); }
        }

        /// <summary>Enables a privilege the token already holds (e.g. SeRestorePrivilege for setting another owner). False if it is not held.</summary>
        public static bool EnablePrivilege(string name)
        {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), 0x20 | 0x8, out token)) return false;
            try
            {
                long luid;
                if (!LookupPrivilegeValueW(null, name, out luid)) return false;
                TokenPrivilege tp = new TokenPrivilege { Count = 1, Luid = luid, Attributes = 0x2 };
                if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)) return false;
                return Marshal.GetLastWin32Error() == 0;
            }
            finally { CloseHandle(token); }
        }

        /// <summary>SHA-256 and byte count of the data read through the handle (the handle stays open).</summary>
        public static string Sha256(SafeFileHandle h, out long read)
        {
            using (FileStream s = new FileStream(new SafeFileHandle(h.DangerousGetHandle(), false), FileAccess.Read))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] buf = new byte[1 << 16]; long total = 0; int n;
                while ((n = s.Read(buf, 0, buf.Length)) > 0) { sha.TransformBlock(buf, 0, n, null, 0); total += n; }
                sha.TransformFinalBlock(buf, 0, 0);
                read = total;
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    /// <summary>The process calls ModuleReader needs; Win32ProcessApi is the real one, the self-test supplies a scripted stand-in.</summary>
    public interface IProcessApi
    {
        /// <summary>A process handle for the pid, or IntPtr.Zero (error in lastError).</summary>
        IntPtr Open(int pid, out int lastError);
        /// <summary>Creation time read from the handle as UTC ticks, or -1.</summary>
        long CreationTicks(IntPtr h);
        /// <summary>True while the process behind the handle has not exited.</summary>
        bool IsRunning(IntPtr h);
        /// <summary>Module file names read through the handle; null on failure, complete=false when the list may be partial.</summary>
        string[] Modules(IntPtr h, out bool complete, out string error);
        void Close(IntPtr h);
    }

    public sealed class ModuleRead
    {
        public string Status;           // OK | NOT_RUNNING | OPEN_FAILED | TIME_UNREADABLE | OTHER_INSTANCE | EXITED_BEFORE_READ | READ_FAILED | INCOMPLETE | EXITED_DURING_READ
        public int Pid; public long ExpectedTicks; public long HandleTicks = -1; public string Reason = ""; public string[] Modules = new string[0];
        public string Identity { get { return "pid " + Pid + " + creation time read from the same process handle the modules were read through"; } }
    }

    /// <summary>
    /// G5-R05: one OpenProcess per read. The creation time is checked and the modules are listed through that one handle, so the pid is
    /// never resolved a second time (an open handle also keeps the process object, and so its pid, from being reused). Modules count only
    /// when the handle's creation time equals the expected instance, the list is complete and the process is still running afterwards;
    /// otherwise the read is a failure or a different instance and no module is returned. The handle is always closed.
    /// </summary>
    public static class ModuleReader
    {
        public static long Micro(long ticks) { return ticks - (ticks % 10); }

        public static ModuleRead Read(IProcessApi api, int pid, long expectedTicks)
        {
            ModuleRead r = new ModuleRead { Pid = pid, ExpectedTicks = expectedTicks };
            int err;
            IntPtr h = api.Open(pid, out err);
            if (h == IntPtr.Zero)
            {
                r.Status = err == 87 ? "NOT_RUNNING" : "OPEN_FAILED";             // ERROR_INVALID_PARAMETER: no process has this pid any more
                r.Reason = "OpenProcess failed (" + err + ")"; return r;
            }
            try
            {
                long t = api.CreationTicks(h);
                if (t < 0) { r.Status = "TIME_UNREADABLE"; r.Reason = "creation time unreadable from the handle"; return r; }
                r.HandleTicks = Micro(t);
                if (r.HandleTicks != expectedTicks) { r.Status = "OTHER_INSTANCE"; r.Reason = "the pid belongs to another process instance"; return r; }
                if (!api.IsRunning(h)) { r.Status = "EXITED_BEFORE_READ"; r.Reason = "the instance exited before its modules were read"; return r; }
                bool complete; string error;
                string[] mods = api.Modules(h, out complete, out error);
                if (mods == null) { r.Status = "READ_FAILED"; r.Reason = error ?? "module list unreadable"; return r; }
                if (!complete) { r.Status = "INCOMPLETE"; r.Reason = error ?? "module list partial"; return r; }
                if (!api.IsRunning(h)) { r.Status = "EXITED_DURING_READ"; r.Reason = "the instance exited while its modules were read; the list is not used"; return r; }
                if (Micro(api.CreationTicks(h)) != expectedTicks) { r.Status = "READ_FAILED"; r.Reason = "creation time changed on the same handle"; return r; }
                r.Status = "OK"; r.Modules = mods;
                return r;
            }
            finally { api.Close(h); }
        }
    }

    public sealed class Win32ProcessApi : IProcessApi
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessTimes(IntPtr h, out long creation, out long exit, out long kernel, out long user);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr h, uint ms);
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModulesEx(IntPtr h, [Out] IntPtr[] modules, uint size, out uint needed, uint filter);
        [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileNameExW(IntPtr h, IntPtr module, [Out] char[] name, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr h);

        public IntPtr Open(int pid, out int lastError)
        {
            IntPtr h = OpenProcess(0x0400 | 0x0010 | 0x00100000, false, pid);    // QUERY_INFORMATION | VM_READ | SYNCHRONIZE
            lastError = h == IntPtr.Zero ? Marshal.GetLastWin32Error() : 0;
            return h;
        }

        public long CreationTicks(IntPtr h)
        {
            long c, e, k, u;
            return GetProcessTimes(h, out c, out e, out k, out u) ? DateTime.FromFileTimeUtc(c).Ticks : -1;
        }

        public bool IsRunning(IntPtr h) { return WaitForSingleObject(h, 0) == 0x102; }    // WAIT_TIMEOUT

        public string[] Modules(IntPtr h, out bool complete, out string error)
        {
            complete = false; error = null;
            IntPtr[] buf = new IntPtr[256];
            for (int attempt = 0; attempt < 5; attempt++)
            {
                uint needed;
                if (!EnumProcessModulesEx(h, buf, (uint)(buf.Length * IntPtr.Size), out needed, 0x03))    // LIST_MODULES_ALL
                {
                    error = "EnumProcessModulesEx failed (" + Marshal.GetLastWin32Error() + ")"; return null;
                }
                int count = (int)(needed / IntPtr.Size);
                if (count > buf.Length) { buf = new IntPtr[count + 64]; continue; }             // the list grew: read it again, whole
                string[] names = new string[count];
                char[] name = new char[32768];
                for (int i = 0; i < count; i++)
                {
                    uint n = GetModuleFileNameExW(h, buf[i], name, (uint)name.Length);
                    if (n == 0) { error = "GetModuleFileNameExW failed (" + Marshal.GetLastWin32Error() + ") for module " + i + " of " + count; return names; }
                    names[i] = new string(name, 0, (int)n);
                }
                complete = true;
                return names;
            }
            error = "the module list kept growing";
            return null;
        }

        public void Close(IntPtr h) { CloseHandle(h); }
    }
}
