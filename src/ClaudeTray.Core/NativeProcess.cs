using System.Runtime.InteropServices;

namespace ClaudeTray.Core;

public readonly record struct ProcessRef(int Pid, long StartedAtMs);

/// <summary>Procesi preko Win32 API-ja, kompatibilno s Native AOT-om.</summary>
public static partial class NativeProcess
{
    const uint TH32CS_SNAPPROCESS = 0x2;
    const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, SYNCHRONIZE = 0x00100000;
    const uint WAIT_TIMEOUT = 0x102;
    const int ERROR_ACCESS_DENIED = 5;
    const long FileTimeUnixEpoch = 116444736000000000;
    const int MaxSteps = 32;

    public static int? GetParentPid(int pid) => Snapshot().TryGetValue(pid, out var e) ? e.Parent : null;

    public static string? GetExeName(int pid) => Snapshot().TryGetValue(pid, out var e) ? e.Exe : null;

    public static long? GetStartTimeMs(int pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return null;
        try { return StartTimeOf(h); }
        finally { CloseHandle(h); }
    }

    /// <summary>
    /// Proces postoji, nije završio i ima isto vrijeme pokretanja (zaštita od ponovne upotrebe PID-a).
    /// Ako proces postoji, ali mu ne smijemo pristupiti, smatra se živim.
    /// </summary>
    public static bool IsAlive(int pid, long startedAtMs)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, false, (uint)pid);
        if (h == IntPtr.Zero) return Marshal.GetLastPInvokeError() == ERROR_ACCESS_DENIED;
        try
        {
            if (WaitForSingleObject(h, 0) != WAIT_TIMEOUT) return false;
            return StartTimeOf(h) == startedAtMs;
        }
        finally { CloseHandle(h); }
    }

    public static ProcessRef? FindAncestor(int startPid, string exeName)
    {
        var map = Snapshot();
        foreach (int pid in Walk(startPid, map))
        {
            if (string.Equals(map[pid].Exe, exeName, StringComparison.OrdinalIgnoreCase) && GetStartTimeMs(pid) is { } start)
                return new ProcessRef(pid, start);
        }
        return null;
    }

    public static IReadOnlyList<int> AncestorChain(int startPid) => Walk(startPid, Snapshot()).ToList();

    /// <summary>Windows sesija trenutnog procesa (0 je sesija servisa, bez radne površine).</summary>
    public static uint CurrentSessionId() =>
        ProcessIdToSessionId((uint)Environment.ProcessId, out uint session) ? session : uint.MaxValue;

    /// <summary>U sesiji 0 nema radne površine, pa ikona u traci ne bi bila vidljiva.</summary>
    public static bool IsServiceSession(uint sessionId) => sessionId == 0;

    /// <summary>Roditelji redom od startPid prema gore. Staje kod petlje ili kad je roditelj mlađi od djeteta.</summary>
    static IEnumerable<int> Walk(int startPid, Dictionary<int, (int Parent, string Exe)> map)
    {
        var seen = new HashSet<int> { startPid };
        int child = startPid;
        long? childStart = GetStartTimeMs(child);
        for (int step = 0; step < MaxSteps && map.TryGetValue(child, out var entry); step++)
        {
            int parent = entry.Parent;
            if (parent == 0 || !seen.Add(parent) || !map.ContainsKey(parent)) yield break;
            long? parentStart = GetStartTimeMs(parent);
            if (childStart is { } cs && parentStart is { } ps && ps > cs) yield break; // PID roditelja je ponovno upotrijebljen
            yield return parent;
            child = parent;
            childStart = parentStart;
        }
    }

    static unsafe Dictionary<int, (int Parent, string Exe)> Snapshot()
    {
        var map = new Dictionary<int, (int, string)>();
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return map;
        try
        {
            var pe = new PROCESSENTRY32W { dwSize = (uint)sizeof(PROCESSENTRY32W) };
            if (!Process32FirstW(snap, ref pe)) return map;
            do
            {
                string exe = new string(pe.szExeFile, 0, IndexOfNul(pe.szExeFile, 260));
                map[(int)pe.th32ProcessID] = ((int)pe.th32ParentProcessID, exe);
            }
            while (Process32NextW(snap, ref pe));
        }
        finally { CloseHandle(snap); }
        return map;
    }

    static unsafe int IndexOfNul(char* s, int max)
    {
        for (int i = 0; i < max; i++) if (s[i] == '\0') return i;
        return max;
    }

    static long? StartTimeOf(IntPtr h) =>
        GetProcessTimes(h, out long creation, out _, out _, out _) ? (creation - FileTimeUnixEpoch) / 10_000 : null;

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct PROCESSENTRY32W
    {
        public uint dwSize, cntUsage, th32ProcessID;
        public UIntPtr th32DefaultHeapID;
        public uint th32ModuleID, cntThreads, th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        public fixed char szExeFile[260];
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32FirstW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32NextW(IntPtr snapshot, ref PROCESSENTRY32W entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ProcessIdToSessionId(uint processId, out uint sessionId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
