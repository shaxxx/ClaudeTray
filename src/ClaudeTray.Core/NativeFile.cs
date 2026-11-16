using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ClaudeTray.Core;

/// <summary>
/// Zamjena datoteke s POSIX semantikom: uspijeva i dok drugi proces drži odredište otvorenim
/// (uz FILE_SHARE_DELETE), što obični MoveFileEx ne može. Windows 10 1709+ na NTFS-u.
/// </summary>
internal static partial class NativeFile
{
    const uint DELETE = 0x00010000, SYNCHRONIZE = 0x00100000;
    const uint FILE_SHARE_ALL = 0x1 | 0x2 | 0x4;
    const uint OPEN_EXISTING = 3;
    const int FileRenameInfoEx = 22;
    const uint FILE_RENAME_FLAG_REPLACE_IF_EXISTS = 0x1, FILE_RENAME_FLAG_POSIX_SEMANTICS = 0x2;

    public static unsafe bool TryPosixReplace(string source, string target)
    {
        using SafeFileHandle h = CreateFileW(source, DELETE | SYNCHRONIZE, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid) return false;

        string full = Path.GetFullPath(target);
        // FILE_RENAME_INFO: ULONG Flags; HANDLE RootDirectory; DWORD FileNameLength; WCHAR FileName[]
        int rootOffset = IntPtr.Size;              // Flags (4) + poravnanje na veličinu pokazivača
        int lengthOffset = rootOffset + IntPtr.Size;
        int nameOffset = lengthOffset + sizeof(uint);
        int nameBytes = full.Length * sizeof(char);
        int size = nameOffset + nameBytes + sizeof(char);

        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            new Span<byte>((void*)buf, size).Clear();
            Marshal.WriteInt32(buf, 0, unchecked((int)(FILE_RENAME_FLAG_REPLACE_IF_EXISTS | FILE_RENAME_FLAG_POSIX_SEMANTICS)));
            Marshal.WriteIntPtr(buf, rootOffset, IntPtr.Zero);
            Marshal.WriteInt32(buf, lengthOffset, nameBytes);
            full.AsSpan().CopyTo(new Span<char>((void*)(buf + nameOffset), full.Length));
            return SetFileInformationByHandle(h, FileRenameInfoEx, buf, (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, IntPtr info, uint size);
}
