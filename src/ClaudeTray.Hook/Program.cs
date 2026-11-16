using System.Text.Json;
using ClaudeTray.Core;

namespace ClaudeTray.Hook;

/// <summary>
/// Hook Claude Codea (spec 5.4). Čita događaj sa stdina, zapisuje stanje sesije i po potrebi pokreće tray.
/// Nikad ne piše na stdout ni stderr i uvijek izlazi s kodom 0, da ne utječe na Claudea.
/// </summary>
static class Program
{
    static int Main()
    {
        try { Run(); }
        catch (Exception) { /* hook nikad ne smeta Claudeu */ }
        return 0;
    }

    static void Run()
    {
        string raw = StdinReader.ReadAll(TimeSpan.FromSeconds(1));
        HookInput? input = null;
        try { input = JsonSerializer.Deserialize(raw, CoreJsonContext.Default.HookInput); }
        catch (JsonException) { }
        HookLog.Write(raw.Length, input);
        if (input is null || string.IsNullOrEmpty(input.SessionId)) return;

        string evt = input.HookEventName ?? "";
        string stateDir = TrayPaths.StateDir;
        var previous = StateStore.TryRead(StateStore.PathFor(stateDir, input.SessionId));
        var env = new HookEnvironment(Env("CLAUDE_CODE_ENTRYPOINT"), Env("TERM_PROGRAM"), Env("CLAUDE_PROJECT_DIR"));
        var result = EventMapper.Apply(previous, input, env, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        switch (result.Action)
        {
            case MapAction.Write:
                var record = result.Record!;
                if (record.Pid == 0 || evt == "SessionStart") record = WithClaudeProcess(record);
                StateStore.WriteAtomic(stateDir, record);
                break;
            case MapAction.Delete:
                StateStore.Delete(stateDir, input.SessionId);
                break;
            default:
                return;
        }

        if (evt == "SessionStart" && !EventMapper.IsCompact(input)) TryDelete(TrayPaths.QuitIntentFile);
        if (evt != "SessionEnd") TrayLauncher.EnsureRunning(Path.Combine(AppContext.BaseDirectory, TrayPaths.TrayExeName));
    }

    /// <summary>PID sesije: CLAUDE_PID (dodatak A (g)), inače najbliži predak claude.exe.</summary>
    static SessionRecord WithClaudeProcess(SessionRecord r)
    {
        if (int.TryParse(Env("CLAUDE_PID"), out int pid) && NativeProcess.GetStartTimeMs(pid) is { } start)
            return r with { Pid = pid, PidStartedAt = start };
        if (NativeProcess.FindAncestor(Environment.ProcessId, "claude.exe") is { } claude)
            return r with { Pid = claude.Pid, PidStartedAt = claude.StartedAtMs };
        return r;
    }

    static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
