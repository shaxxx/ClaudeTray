using System.Diagnostics;
using System.Text;
using ClaudeTray.Core;
using Xunit.Abstractions;

namespace ClaudeTray.Tests;

/// <summary>Pokreće objavljeni AOT hook (artifacts\publish). Prije testova pokrenuti build.cmd.</summary>
public class HookIntegrationTests
{
    static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    internal sealed record Run(int Exit, string Stdout, string Stderr, long ElapsedMs);

    internal static Run RunHook(string stdin, string home, IDictionary<string, string>? extraEnv = null, bool closeStdinOnly = false)
    {
        string exe = Repo.Publish(TrayPaths.HookExeName);
        if (!File.Exists(exe)) Assert.Fail("Nema " + exe + ". Pokreni build.cmd.");

        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var name in psi.Environment.Keys.ToList())
            if (!ChildEnvironment.IsInherited(name)) psi.Environment.Remove(name);   // bez CLAUDE_* ove sesije
        psi.Environment["CLAUDE_TRAY_HOME"] = home;
        psi.Environment["CLAUDE_TRAY_NO_LAUNCH"] = "1";
        foreach (var (k, v) in extraEnv ?? new Dictionary<string, string>()) psi.Environment[k] = v;

        var sw = Stopwatch.StartNew();
        using var p = Process.Start(psi)!;
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!closeStdinOnly) p.StandardInput.Write(stdin);
        p.StandardInput.Close();
        Assert.True(p.WaitForExit(10_000), "hook nije završio u 10 s");
        sw.Stop();
        return new Run(p.ExitCode, outTask.Result, errTask.Result, sw.ElapsedMilliseconds);
    }

    internal static string Fixture(string name) => File.ReadAllText(Path.Combine(Fixtures, name + ".json"));
    static string SessionIdOf(string json) => System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("session_id").GetString()!;

    [Fact]
    public void PreToolUseWrite_WritesWorkingFile()
    {
        using var home = new TempDir();
        string json = Fixture("PreToolUse-Write");
        var run = RunHook(json, home.Path);

        Assert.Equal(0, run.Exit);
        Assert.Equal("", run.Stdout);
        Assert.Equal("", run.Stderr);
        var rec = StateStore.TryRead(StateStore.PathFor(Path.Combine(home.Path, "state"), SessionIdOf(json)));
        Assert.NotNull(rec);
        Assert.Equal(SessionStatus.Working, rec!.State);
        Assert.Equal("Piše", rec.Detail);
    }

    [Fact]
    public void SessionEnd_DeletesFile()
    {
        using var home = new TempDir();
        string pre = Fixture("PreToolUse-Write");
        string id = SessionIdOf(pre);
        RunHook(pre, home.Path);
        string path = StateStore.PathFor(Path.Combine(home.Path, "state"), id);
        Assert.True(File.Exists(path));

        RunHook($$"""{"session_id":"{{id}}","hook_event_name":"SessionEnd","reason":"other"}""", home.Path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Garbage_Exit0_NoFiles()
    {
        using var home = new TempDir();
        var run = RunHook("not json", home.Path);
        Assert.Equal(0, run.Exit);
        Assert.Equal("", run.Stdout + run.Stderr);
        Assert.False(Directory.Exists(Path.Combine(home.Path, "state")) && Directory.EnumerateFileSystemEntries(Path.Combine(home.Path, "state")).Any());
    }

    [Fact]
    public void EmptyStdin_ExitsWithin1500ms()
    {
        using var home = new TempDir();
        var run = RunHook("", home.Path, closeStdinOnly: true);
        Assert.Equal(0, run.Exit);
        Assert.True(run.ElapsedMs < 1500, $"trajalo {run.ElapsedMs} ms");
    }

    [Fact]
    public void HugeStdin5MB_WritesFile()
    {
        using var home = new TempDir();
        string json = $$$"""{"session_id":"big","hook_event_name":"PreToolUse","tool_name":"Write","tool_input":{"content":"{{{new string('x', 5 * 1024 * 1024)}}}"}}""";
        var run = RunHook(json, home.Path);
        Assert.Equal(0, run.Exit);
        Assert.True(run.ElapsedMs < 2000, $"trajalo {run.ElapsedMs} ms");
        Assert.True(File.Exists(StateStore.PathFor(Path.Combine(home.Path, "state"), "big")));
    }

    [Fact]
    public void SessionStart_RemovesQuitIntent()
    {
        using var home = new TempDir();
        string quit = Path.Combine(home.Path, "quit-intent");
        File.WriteAllText(quit, "");
        RunHook(Fixture("SessionStart"), home.Path);
        Assert.False(File.Exists(quit));
    }

    [Fact]
    public void SessionStartCompact_KeepsQuitIntent()
    {
        // Sažimanje nije nova sesija: "Izlaz" mora vrijediti i nakon njega.
        using var home = new TempDir();
        string quit = Path.Combine(home.Path, "quit-intent");
        File.WriteAllText(quit, "");
        var run = RunHook("""{"session_id":"q1","hook_event_name":"SessionStart","source":"compact","cwd":"C:\\x"}""", home.Path);
        Assert.Equal(0, run.Exit);
        Assert.True(File.Exists(quit));
    }

    [Fact]
    public void UsesClaudePidEnv()
    {
        using var home = new TempDir();
        string json = Fixture("UserPromptSubmit");
        RunHook(json, home.Path, new Dictionary<string, string> { ["CLAUDE_PID"] = Environment.ProcessId.ToString() });
        var rec = StateStore.TryRead(StateStore.PathFor(Path.Combine(home.Path, "state"), SessionIdOf(json)))!;
        Assert.Equal(Environment.ProcessId, rec.Pid);
        Assert.Equal(NativeProcess.GetStartTimeMs(Environment.ProcessId), rec.PidStartedAt);
    }

    [Fact]
    public void NoClaudePid_FallsBackToClaudeAncestor()
    {
        // Hook je dijete test hosta, pa vidi istog pretka claude.exe (ako se testovi izvode iz Claude sesije) ili nijednog.
        int expected = NativeProcess.FindAncestor(Environment.ProcessId, "claude.exe")?.Pid ?? 0;
        using var home = new TempDir();
        string json = Fixture("UserPromptSubmit");
        RunHook(json, home.Path);
        Assert.Equal(expected, StateStore.TryRead(StateStore.PathFor(Path.Combine(home.Path, "state"), SessionIdOf(json)))!.Pid);
    }

    [Fact]
    public void EnvironmentFieldsRecorded()
    {
        using var home = new TempDir();
        string json = Fixture("UserPromptSubmit");
        RunHook(json, home.Path, new Dictionary<string, string>
        {
            ["CLAUDE_CODE_ENTRYPOINT"] = "cli", ["TERM_PROGRAM"] = "vscode", ["CLAUDE_PROJECT_DIR"] = "c:/projekti/Novi modul",
        });
        var rec = StateStore.TryRead(StateStore.PathFor(Path.Combine(home.Path, "state"), SessionIdOf(json)))!;
        Assert.Equal("cli", rec.Entrypoint);
        Assert.Equal("vscode", rec.TermProgram);
        Assert.Equal(@"c:\projekti\Novi modul", rec.ProjectDir);
    }

}

/// <summary>Mjerenje brzine izvodi se bez paralelnih testova, jer bi ga njihovo opterećenje iskrivilo.</summary>
[Collection("Perf")]
public class HookLatencyTests(ITestOutputHelper output)
{
    /// <summary>
    /// Trošak hooka iznad praznog procesa, mjeren naizmjence u istom okviru. Sam okvir (.NET Process.Start s
    /// preusmjerenim cijevima) troši ~17 ms i za prazan proces, pa apsolutni prag ne bi mjerio hook.
    /// </summary>
    [Fact]
    [Trait("Category", "Perf")]
    public void Latency_OverheadOverEmptyProcessUnder10ms()
    {
        using var home = new TempDir();
        string json = HookIntegrationTests.Fixture("PreToolUse-Write");
        HookIntegrationTests.RunHook(json, home.Path);   // zagrijavanje
        Baseline();
        var hook = new List<long>();
        var empty = new List<long>();
        for (int i = 0; i < 20; i++)
        {
            hook.Add(HookIntegrationTests.RunHook(json, home.Path).ElapsedMs);
            empty.Add(Baseline());
        }
        long hookMedian = hook.Order().ElementAt(10), emptyMedian = empty.Order().ElementAt(10);
        string report = $"hook medijan {hookMedian} ms ({string.Join(",", hook.Order())}); prazan proces medijan {emptyMedian} ms";
        output.WriteLine(report);
        Assert.True(hookMedian - emptyMedian < 10, report);
    }

    /// <summary>Isti mjerni okvir (preusmjereni stdin/stdout/stderr) s praznim procesom.</summary>
    static long Baseline()
    {
        var psi = new ProcessStartInfo("cmd.exe", "/c exit")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        var sw = Stopwatch.StartNew();
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        p.StandardInput.Close();
        p.WaitForExit();
        Task.WaitAll(o, e);
        return sw.ElapsedMilliseconds;
    }
}
