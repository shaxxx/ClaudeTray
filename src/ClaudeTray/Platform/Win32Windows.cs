using System.Runtime.InteropServices;

namespace ClaudeTray.Platform;

public readonly record struct TopWindow(IntPtr Handle, int Pid, string Title);

/// <summary>Prozori najviše razine i prvi plan (spec 7).</summary>
public static unsafe partial class Win32Windows
{
    const uint ASFW_ANY = unchecked((uint)-1);
    const uint GW_OWNER = 4;
    const int SW_RESTORE = 9;
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>Svaki proces smije preuzeti prvi plan; tray to smije dati jer je upravo primio klik.</summary>
    public static void AllowAnyForeground() => AllowSetForegroundWindow(ASFW_ANY);

    public static IntPtr Foreground() => GetForegroundWindow();

    /// <summary>Vidljivi prozori najviše razine bez vlasnika i s naslovom, redom od vrha prema dnu.</summary>
    public static List<TopWindow> TopLevel()
    {
        var handles = new List<IntPtr>();
        var gc = GCHandle.Alloc(handles);
        try { EnumWindows(&Collect, GCHandle.ToIntPtr(gc)); }
        finally { gc.Free(); }

        var result = new List<TopWindow>();
        char* buf = stackalloc char[512];
        foreach (var h in handles)
        {
            if (!IsWindowVisible(h) || GetWindow(h, GW_OWNER) != IntPtr.Zero) continue;
            int len = GetWindowTextW(h, buf, 512);
            if (len <= 0) continue;
            GetWindowThreadProcessId(h, out uint pid);
            result.Add(new TopWindow(h, (int)pid, new string(buf, 0, len)));
        }
        return result;
    }

    /// <summary>Ime exe-a procesa (npr. "Code.exe"), ili null ako mu se ne može pristupiti.</summary>
    public static string? ExeName(int pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return null;
        try
        {
            char* buf = stackalloc char[1024];
            int size = 1024;
            return QueryFullProcessImageNameW(h, 0, buf, ref size) ? Path.GetFileName(new string(buf, 0, size)) : null;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Vraća minimizirani prozor i dovodi ga u prvi plan.</summary>
    public static bool Activate(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        return SetForegroundWindow(hwnd);
    }

    [UnmanagedCallersOnly]
    static int Collect(IntPtr hwnd, IntPtr state)
    {
        ((List<IntPtr>)GCHandle.FromIntPtr(state).Target!).Add(hwnd);
        return 1;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(delegate* unmanaged<IntPtr, IntPtr, int> callback, IntPtr state);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetWindow(IntPtr hwnd, uint cmd);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextW(IntPtr hwnd, char* text, int max);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hwnd, int cmd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hwnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(uint pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool QueryFullProcessImageNameW(IntPtr process, uint flags, char* name, ref int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
