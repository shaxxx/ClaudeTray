using System.Runtime.InteropServices;
using ClaudeTray.Core;

namespace ClaudeTray.Hook;

/// <summary>Pokreće tray ako ne radi (spec 5.4, dodatak A (c)).</summary>
static partial class TrayLauncher
{
    const uint DETACHED_PROCESS = 0x00000008, CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    public static void EnsureRunning(string trayExe)
    {
        if (Environment.GetEnvironmentVariable("CLAUDE_TRAY_NO_LAUNCH") == "1") return;
        if (NativeProcess.IsServiceSession(NativeProcess.CurrentSessionId())) return;   // nema radne površine za ikonu
        if (Mutex.TryOpenExisting(TrayPaths.MutexName, out var running)) { running.Dispose(); return; }
        if (File.Exists(TrayPaths.QuitIntentFile)) return;
        if (!File.Exists(trayExe)) return;
        Start(trayExe);
    }

    /// <summary>
    /// Bez nasljeđivanja ručki (tray ne smije držati otvoren stdout cijev hooka) i s očišćenim okruženjem.
    /// Breakaway iz joba nije potreban: job hooka ima SILENT_BREAKAWAY_OK.
    /// </summary>
    static unsafe void Start(string exe)
    {
        string block = ChildEnvironment.BuildBlock(ChildEnvironment.Current());
        IntPtr env = Marshal.StringToHGlobalUni(block);
        IntPtr cmd = Marshal.StringToHGlobalUni("\"" + exe + "\"");
        try
        {
            var si = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
            if (CreateProcessW(null, cmd, IntPtr.Zero, IntPtr.Zero, false, DETACHED_PROCESS | CREATE_UNICODE_ENVIRONMENT,
                               env, Path.GetDirectoryName(exe), ref si, out var pi))
            {
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(cmd);
            Marshal.FreeHGlobal(env);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct STARTUPINFOW
    {
        public uint cb;
        public IntPtr lpReserved, lpDesktop, lpTitle;
        public uint dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public ushort wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public uint dwProcessId, dwThreadId;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateProcessW(string? application, IntPtr commandLine, IntPtr processAttributes, IntPtr threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment, string? currentDirectory,
        ref STARTUPINFOW startupInfo, out PROCESS_INFORMATION processInformation);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
