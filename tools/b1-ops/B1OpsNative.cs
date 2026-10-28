// Handle-bound file-system helpers for tools/b1-ops (loaded with Add-Type -Path by g5-acl.ps1 and b4-candidates.ps1).
// Every open uses FILE_FLAG_OPEN_REPARSE_POINT (a reparse point is opened, never followed) and never shares FILE_SHARE_DELETE, so while a
// handle is held its object cannot be renamed, deleted or replaced; checks and changes go through the same handle (no path re-resolution).
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
        [DllImport("advapi32.dll")]
        private static extern uint SetSecurityInfo(SafeFileHandle h, int type, uint info, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);
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
        /// Sets the owner and/or DACL of the object behind the handle from SDDL. The DACL is written protected when the SDDL says so (D:P...),
        /// otherwise unprotected, in which case Windows recomputes the inherited ACEs from the parent. Windows propagates inheritable ACEs to
        /// existing children; a protected child is not changed.
        /// </summary>
        public static void SetSddl(SafeFileHandle h, string sddl, bool owner, bool dacl)
        {
            IntPtr sd; uint len;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out sd, out len)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                IntPtr o = IntPtr.Zero, d = IntPtr.Zero; bool present, defaulted; ushort control; uint rev;
                uint info = 0;
                if (owner) { GetSecurityDescriptorOwner(sd, out o, out defaulted); if (o == IntPtr.Zero) throw new ArgumentException("SDDL has no owner"); info |= 0x1; }
                if (dacl)
                {
                    GetSecurityDescriptorDacl(sd, out present, out d, out defaulted);
                    if (!present || d == IntPtr.Zero) throw new ArgumentException("SDDL has no DACL");
                    GetSecurityDescriptorControl(sd, out control, out rev);
                    info |= 0x4 | ((control & 0x1000) != 0 ? 0x80000000u : 0x20000000u);
                }
                uint rc = SetSecurityInfo(h, 1, info, o, IntPtr.Zero, d, IntPtr.Zero);
                if (rc != 0) throw new Win32Exception((int)rc);
            }
            finally { LocalFree(sd); }
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
}
