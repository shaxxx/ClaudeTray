using System.Diagnostics;
using ClaudeTray.Core;

namespace ClaudeTray.Tests;

public class NativeProcessTests
{
    static readonly string SelfExe = Path.GetFileName(Environment.ProcessPath!);

    // Ručna provjera 16. 11.: Claude sesija u sesiji 0 (servis, zakazani zadatak) pokrenula je nevidljivi tray.
    [Fact]
    public void ServiceSession_OnlyZero()
    {
        Assert.True(NativeProcess.IsServiceSession(0));
        Assert.False(NativeProcess.IsServiceSession(1));
        Assert.False(NativeProcess.IsServiceSession(NativeProcess.CurrentSessionId()));   // testovi rade na korisnikovoj radnoj površini
    }

    static Process StartChild(string args)
    {
        var p = Process.Start(new ProcessStartInfo("cmd.exe", args) { CreateNoWindow = true, UseShellExecute = false })!;
        return p;
    }

    [Fact]
    public void IsAlive_Self_True()
    {
        long start = NativeProcess.GetStartTimeMs(Environment.ProcessId)!.Value;
        Assert.True(NativeProcess.IsAlive(Environment.ProcessId, start));
    }

    [Fact]
    public void IsAlive_WrongStartTime_False()
    {
        long start = NativeProcess.GetStartTimeMs(Environment.ProcessId)!.Value;
        Assert.False(NativeProcess.IsAlive(Environment.ProcessId, start + 1));
    }

    [Fact]
    public void IsAlive_ExitedChild_False()
    {
        using var child = StartChild("/c exit");
        child.WaitForExit();
        long? start = NativeProcess.GetStartTimeMs(child.Id);   // Process još drži ručku pa PID nije ponovno upotrijebljen
        Assert.NotNull(start);
        Assert.False(NativeProcess.IsAlive(child.Id, start!.Value));
    }

    [Fact]
    public void IsAlive_NonexistentPid_False() => Assert.False(NativeProcess.IsAlive(0x7FFFFFF0, 0));

    [Fact]
    public void GetExeName_Self() =>
        Assert.Equal(SelfExe, NativeProcess.GetExeName(Environment.ProcessId), ignoreCase: true);

    [Fact]
    public void FindAncestor_FindsTestHost()
    {
        using var child = StartChild("/c ping -n 3 127.0.0.1 >nul");
        try
        {
            Assert.Equal(Environment.ProcessId, NativeProcess.GetParentPid(child.Id));
            var found = NativeProcess.FindAncestor(child.Id, SelfExe.ToUpperInvariant());
            Assert.NotNull(found);
            Assert.Equal(Environment.ProcessId, found!.Value.Pid);
            Assert.Equal(NativeProcess.GetStartTimeMs(Environment.ProcessId), found.Value.StartedAtMs);
        }
        finally { child.Kill(); }
    }

    [Fact]
    public void FindAncestor_Missing_Null() => Assert.Null(NativeProcess.FindAncestor(Environment.ProcessId, "nepostoji.exe"));

    [Fact]
    public void AncestorChain_StartsWithParent()
    {
        using var child = StartChild("/c ping -n 3 127.0.0.1 >nul");
        try
        {
            var chain = NativeProcess.AncestorChain(child.Id);
            Assert.NotEmpty(chain);
            Assert.Equal(Environment.ProcessId, chain[0]);
            Assert.DoesNotContain(child.Id, chain);
        }
        finally { child.Kill(); }
    }
}
