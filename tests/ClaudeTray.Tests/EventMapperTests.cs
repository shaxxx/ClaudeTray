using System.Text.Json;
using ClaudeTray.Core;

namespace ClaudeTray.Tests;

public class EventMapperTests
{
    const long Now = 1_000_000;

    static HookInput In(string evt, string? tool = null, string? ntype = null, string? error = null,
                        string? prompt = null, string sid = "s1", string cwd = @"C:\proj", string? title = null,
                        string? transcript = @"C:\t.jsonl", string? source = null) =>
        new()
        {
            SessionId = sid, Cwd = cwd, TranscriptPath = transcript, HookEventName = evt, ToolName = tool,
            NotificationType = ntype, Error = error, Prompt = prompt, SessionTitle = title, Source = source,
        };

    // Završni pregled: SessionStart nakon sažimanja (i automatskog usred poteza) ne smije sakriti sesiju koja radi.
    [Theory]
    [InlineData(SessionStatus.Working, "Pokreće naredbu")]
    [InlineData(SessionStatus.Waiting, "Pita te")]
    [InlineData(SessionStatus.Error, "Ograničenje upotrebe")]
    [InlineData(SessionStatus.Done, "Gotovo")]
    public void SessionStart_Compact_KeepsState(SessionStatus state, string detail)
    {
        var prev = new SessionRecord
        {
            SessionId = "s1", State = state, Detail = detail, Tool = "Bash", Started = true, TurnStartedAt = 500,
            UpdatedAt = 900, ProjectDir = @"C:\proj", Title = "naslov",
        };
        var r = Written(EventMapper.Apply(prev, In("SessionStart", source: "compact", transcript: @"C:\novi.jsonl"), Env(), Now));
        Assert.Equal(state, r.State);
        Assert.Equal(detail, r.Detail);
        Assert.Equal("Bash", r.Tool);
        Assert.True(r.Started);
        Assert.Equal(500, r.TurnStartedAt);
        Assert.Equal(900, r.UpdatedAt);   // potvrda greške vezana je uz UpdatedAt
        Assert.Equal("naslov", r.Title);
        Assert.Equal(@"C:\novi.jsonl", r.Transcript);
    }

    [Fact]
    public void SessionStart_Compact_NoPrevious_IdleNotStarted()
    {
        var r = Written(EventMapper.Apply(null, In("SessionStart", source: "compact"), Env(), Now));
        Assert.Equal(SessionStatus.Idle, r.State);
        Assert.False(r.Started);
    }

    [Theory]
    [InlineData("startup")]
    [InlineData("resume")]
    [InlineData("clear")]
    [InlineData(null)]
    public void SessionStart_OtherSources_IdleNotStarted(string? source)
    {
        var prev = new SessionRecord { SessionId = "s1", State = SessionStatus.Working, Started = true, TurnStartedAt = 500 };
        var r = Written(EventMapper.Apply(prev, In("SessionStart", source: source), Env(), Now));
        Assert.Equal(SessionStatus.Idle, r.State);
        Assert.False(r.Started);
        Assert.Equal(0, r.TurnStartedAt);
    }

    // Ručna provjera 16. 11.: poruke koje Claude Code ubacuje kao prompt (<agent-message …>, <task-notification> …)
    // nisu korisnikov prompt i ne smiju postati naslov sesije.
    [Theory]
    [InlineData("<agent-message from=\"ae0e\"> [Subagent hand-back] …")]
    [InlineData("  <task-notification>\n<task-id>x</task-id>")]
    public void UserPromptSubmit_SyntheticMessage_NoTitle(string prompt)
    {
        var r = Written(EventMapper.Apply(null, In("UserPromptSubmit", prompt: prompt), Env(), Now));
        Assert.Equal("", r.Title);
    }

    // Ručna provjera 16. 11.: VS Code ispred korisnikova teksta dodaje <ide_opened_file>/<ide_selection> blokove.
    [Theory]
    [InlineData("<ide_opened_file>The user opened the file c:\\x\\plan.md in the IDE.</ide_opened_file>sve ok", "sve ok")]
    [InlineData("<ide_opened_file>a</ide_opened_file>\n<ide_selection>b < c</ide_selection>\n napravi  export", "napravi export")]
    [InlineData("<command-name>/compact</command-name>\n<command-message>compact</command-message>", "")]
    [InlineData("<agent-message from=\"a\">\n[Subagent hand-back] gotovo</agent-message>", "")]
    [InlineData("<b>ovo nije zatvoreno", "")]
    public void UserPromptSubmit_IdeContextPrefix_TitleFromUserText(string prompt, string title)
    {
        var r = Written(EventMapper.Apply(null, In("UserPromptSubmit", prompt: prompt), Env(), Now));
        Assert.Equal(title, r.Title);
    }

    [Fact]
    public void UserPromptSubmit_ReplacesSyntheticTitle()
    {
        var prev = new SessionRecord { SessionId = "s1", Title = "<agent-message from=\"ae0e\"> [Sub" };
        var r = Written(EventMapper.Apply(prev, In("UserPromptSubmit", prompt: "napravi export"), Env(), Now));
        Assert.Equal("napravi export", r.Title);
    }

    // Završni pregled: permission_prompt stiže ~6 s nakon zahtjeva (P1); ako potez u međuvremenu završi,
    // zakašnjela obavijest ne smije vratiti žutu točku.
    [Theory]
    [InlineData(SessionStatus.Done)]
    [InlineData(SessionStatus.Idle)]
    public void LatePermissionPrompt_AfterDoneOrIdle_Ignored(SessionStatus state)
    {
        var prev = new SessionRecord { SessionId = "s1", State = state, Detail = "Gotovo", Started = true };
        Assert.Equal(MapAction.Ignore, EventMapper.Apply(prev, In("Notification", ntype: "permission_prompt"), Env(), Now).Action);
    }

    static HookEnvironment Env(string entry = "claude-vscode", string term = "", string proj = "") => new(entry, term, proj);

    static SessionRecord Written(MapResult r)
    {
        Assert.Equal(MapAction.Write, r.Action);
        Assert.NotNull(r.Record);
        return r.Record!;
    }

    [Fact]
    public void SessionStart_NoPrevious_IdleNotStarted()
    {
        var r = Written(EventMapper.Apply(null, In("SessionStart"), Env(), Now));
        Assert.Equal("s1", r.SessionId);
        Assert.Equal(SessionStatus.Idle, r.State);
        Assert.False(r.Started);
        Assert.Equal(@"C:\proj", r.ProjectDir);
        Assert.Equal(@"C:\proj", r.Cwd);
        Assert.Equal("claude-vscode", r.Entrypoint);
        Assert.Equal(@"C:\t.jsonl", r.Transcript);
        Assert.Equal(Now, r.UpdatedAt);
    }

    [Fact]
    public void SessionStart_KeepsPreviousProjectDir()
    {
        var prev = new SessionRecord { SessionId = "s1", ProjectDir = @"C:\a", Started = true, State = SessionStatus.Working };
        var r = Written(EventMapper.Apply(prev, In("SessionStart", cwd: @"C:\a\sub"), Env(), Now));
        Assert.Equal(@"C:\a", r.ProjectDir);
        Assert.Equal(SessionStatus.Idle, r.State);
        Assert.False(r.Started);
    }

    [Fact]
    public void SessionStart_UsesEnvProjectDir_Normalized()
    {
        var r = Written(EventMapper.Apply(null, In("SessionStart", cwd: @"C:\other"), Env(proj: "c:/Users/x/probe-workspace"), Now));
        Assert.Equal(@"c:\Users\x\probe-workspace", r.ProjectDir);
    }

    [Fact]
    public void FirstEventWithoutSessionStart_SetsProjectDirAndStarted()
    {
        var r = Written(EventMapper.Apply(null, In("UserPromptSubmit", prompt: "bok"), Env(), Now));
        Assert.Equal(@"C:\proj", r.ProjectDir);
        Assert.True(r.Started);
    }

    [Fact]
    public void UserPromptSubmit_WorkingThinkingTimerTitle()
    {
        var r = Written(EventMapper.Apply(null, In("UserPromptSubmit", prompt: "  napravi  export\n u PDF "), Env(), Now));
        Assert.Equal(SessionStatus.Working, r.State);
        Assert.Equal("Razmišlja…", r.Detail);
        Assert.Equal(Now, r.TurnStartedAt);
        Assert.True(r.Started);
        Assert.Equal("napravi export u PDF", r.Title);
    }

    [Fact]
    public void UserPromptSubmit_TitleTruncatedTo50()
    {
        var r = Written(EventMapper.Apply(null, In("UserPromptSubmit", prompt: new string('x', 80)), Env(), Now));
        Assert.Equal(50, r.Title.Length);
    }

    [Fact]
    public void UserPromptSubmit_SessionTitleWins()
    {
        var prev = new SessionRecord { SessionId = "s1", Title = "stari" };
        var r = Written(EventMapper.Apply(prev, In("UserPromptSubmit", prompt: "novi", title: "Moj naslov"), Env(), Now));
        Assert.Equal("Moj naslov", r.Title);
    }

    [Fact]
    public void UserPromptSubmit_KeepsExistingTitle()
    {
        var prev = new SessionRecord { SessionId = "s1", Title = "stari" };
        var r = Written(EventMapper.Apply(prev, In("UserPromptSubmit", prompt: "novi"), Env(), Now));
        Assert.Equal("stari", r.Title);
    }

    [Fact]
    public void UserPromptSubmit_ClearsError()
    {
        var prev = new SessionRecord { SessionId = "s1", State = SessionStatus.Error, Detail = "prekoračen limit" };
        var r = Written(EventMapper.Apply(prev, In("UserPromptSubmit", prompt: "opet"), Env(), Now));
        Assert.Equal(SessionStatus.Working, r.State);
        Assert.Equal("Razmišlja…", r.Detail);
    }

    [Fact]
    public void PreToolUse_Edit_WorkingWithLabel()
    {
        var prev = new SessionRecord { SessionId = "s1", TurnStartedAt = 500 };
        var r = Written(EventMapper.Apply(prev, In("PreToolUse", tool: "Edit"), Env(), Now));
        Assert.Equal(SessionStatus.Working, r.State);
        Assert.Equal("Uređuje", r.Detail);
        Assert.Equal("Edit", r.Tool);
        Assert.Equal(500, r.TurnStartedAt);
    }

    [Fact]
    public void PreToolUse_NoTurnStart_StartsTimer()
    {
        var r = Written(EventMapper.Apply(null, In("PreToolUse", tool: "Read"), Env(), Now));
        Assert.Equal(Now, r.TurnStartedAt);
    }

    [Theory]
    [InlineData("AskUserQuestion")]
    [InlineData("ExitPlanMode")]
    public void PreToolUse_WaitingTools_Waiting(string tool)
    {
        var r = Written(EventMapper.Apply(null, In("PreToolUse", tool: tool), Env(), Now));
        Assert.Equal(SessionStatus.Waiting, r.State);
        Assert.Equal("Pita te", r.Detail);
    }

    [Theory]
    [InlineData("PostToolUse")]
    [InlineData("PostToolUseFailure")]
    public void PostToolUse_And_Failure_BackToThinking(string evt)
    {
        var prev = new SessionRecord { SessionId = "s1", State = SessionStatus.Waiting, Detail = "Pita te", TurnStartedAt = 500 };
        var r = Written(EventMapper.Apply(prev, In(evt, tool: "AskUserQuestion"), Env(), Now));
        Assert.Equal(SessionStatus.Working, r.State);
        Assert.Equal("Razmišlja…", r.Detail);
        Assert.Equal(500, r.TurnStartedAt);
    }

    [Fact]
    public void PermissionRequest_OtherTool_WaitingPermission()
    {
        var r = Written(EventMapper.Apply(null, In("PermissionRequest", tool: "PowerShell"), Env(), Now));
        Assert.Equal(SessionStatus.Waiting, r.State);
        Assert.Equal("Traži dozvolu", r.Detail);
    }

    [Theory]
    [InlineData("AskUserQuestion")]
    [InlineData("ExitPlanMode")]
    public void PermissionRequest_AskingTool_PitaTe(string tool)
    {
        var r = Written(EventMapper.Apply(null, In("PermissionRequest", tool: tool), Env(), Now));
        Assert.Equal(SessionStatus.Waiting, r.State);
        Assert.Equal("Pita te", r.Detail);
    }

    [Theory]
    [InlineData("permission_prompt", SessionStatus.Waiting, "Traži dozvolu")]
    [InlineData("elicitation_dialog", SessionStatus.Waiting, "Pita te")]
    [InlineData("elicitation_url_dialog", SessionStatus.Waiting, "Pita te")]
    [InlineData("agent_needs_input", SessionStatus.Waiting, "Pita te")]
    [InlineData("elicitation_response", SessionStatus.Working, "Razmišlja…")]
    [InlineData("elicitation_complete", SessionStatus.Working, "Razmišlja…")]
    public void Notification_HandledTypes(string ntype, SessionStatus state, string detail)
    {
        var r = Written(EventMapper.Apply(null, In("Notification", ntype: ntype), Env(), Now));
        Assert.Equal(state, r.State);
        Assert.Equal(detail, r.Detail);
    }

    [Theory]
    [InlineData("idle_prompt")]
    [InlineData("auth_success")]
    [InlineData(null)]
    public void Notification_OtherTypes_Ignore(string? ntype)
    {
        Assert.Equal(MapAction.Ignore, EventMapper.Apply(null, In("Notification", ntype: ntype), Env(), Now).Action);
    }

    [Fact]
    public void Notification_PermissionPrompt_AfterAsking_KeepsLabel()
    {
        var prev = new SessionRecord { SessionId = "s1", State = SessionStatus.Waiting, Detail = "Pita te" };
        var r = Written(EventMapper.Apply(prev, In("Notification", ntype: "permission_prompt"), Env(), Now));
        Assert.Equal(SessionStatus.Waiting, r.State);
        Assert.Equal("Pita te", r.Detail);
    }

    [Fact]
    public void Stop_Done_TimerReset()
    {
        var prev = new SessionRecord { SessionId = "s1", State = SessionStatus.Working, TurnStartedAt = 500 };
        var r = Written(EventMapper.Apply(prev, In("Stop"), Env(), Now));
        Assert.Equal(SessionStatus.Done, r.State);
        Assert.Equal("Gotovo", r.Detail);
        Assert.Equal(0, r.TurnStartedAt);
    }

    [Fact]
    public void StopFailure_ErrorWithLabel()
    {
        var prev = new SessionRecord { SessionId = "s1", State = SessionStatus.Working, TurnStartedAt = 500 };
        var r = Written(EventMapper.Apply(prev, In("StopFailure", error: "rate_limit"), Env(), Now));
        Assert.Equal(SessionStatus.Error, r.State);
        Assert.Equal("prekoračen limit", r.Detail);
        Assert.Equal(0, r.TurnStartedAt);
    }

    [Fact]
    public void SessionEnd_Delete() =>
        Assert.Equal(MapAction.Delete, EventMapper.Apply(null, In("SessionEnd"), Env(), Now).Action);

    [Fact]
    public void UnknownEvent_Ignore() =>
        Assert.Equal(MapAction.Ignore, EventMapper.Apply(null, In("PreCompact"), Env(), Now).Action);

    [Fact]
    public void EmptySessionId_Ignore() =>
        Assert.Equal(MapAction.Ignore, EventMapper.Apply(null, In("UserPromptSubmit", sid: ""), Env(), Now).Action);

    [Fact]
    public void EmptyEnv_KeepsPreviousEntrypoint()
    {
        var prev = new SessionRecord { SessionId = "s1", Entrypoint = "cli", TermProgram = "vscode" };
        var r = Written(EventMapper.Apply(prev, In("PreToolUse", tool: "Read"), Env("", "", ""), Now));
        Assert.Equal("cli", r.Entrypoint);
        Assert.Equal("vscode", r.TermProgram);
    }

    [Fact]
    public void Write_CarriesPidAndTranscript()
    {
        var prev = new SessionRecord { SessionId = "s1", Pid = 42, PidStartedAt = 7, Transcript = @"C:\old.jsonl" };
        var r = Written(EventMapper.Apply(prev, In("PreToolUse", tool: "Read", transcript: null), Env(), Now));
        Assert.Equal(42, r.Pid);
        Assert.Equal(7, r.PidStartedAt);
        Assert.Equal(@"C:\old.jsonl", r.Transcript);
    }

    [Fact]
    public void RecordJson_CamelCaseAndStringEnum()
    {
        var rec = new SessionRecord { SessionId = "a", State = SessionStatus.Working };
        string json = JsonSerializer.Serialize(rec, CoreJsonContext.Default.SessionRecord);
        Assert.Contains("\"state\":\"working\"", json);
        Assert.Contains("\"sessionId\":\"a\"", json);
        Assert.Contains("\"v\":1", json);
        Assert.Equal(rec, JsonSerializer.Deserialize(json, CoreJsonContext.Default.SessionRecord));
    }

    [Fact]
    public void Fixtures_AllParseAndMap()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var files = Directory.GetFiles(dir, "*.json");
        Assert.True(files.Length >= 20, $"premalo testnih podataka: {files.Length}");

        var results = new Dictionary<string, MapResult>();
        foreach (var f in files)
        {
            var input = JsonSerializer.Deserialize(File.ReadAllText(f), CoreJsonContext.Default.HookInput);
            Assert.NotNull(input);
            Assert.False(string.IsNullOrEmpty(input!.SessionId), Path.GetFileName(f));
            results[Path.GetFileNameWithoutExtension(f)] = EventMapper.Apply(null, input, Env(), Now);
        }

        Assert.Equal(SessionStatus.Waiting, results["PreToolUse-AskUserQuestion"].Record!.State);
        Assert.Equal("Pita te", results["PermissionRequest-AskUserQuestion"].Record!.Detail);
        Assert.Equal("Uređuje", Labels.ForTool("Edit"));
        Assert.Equal("Piše", results["PreToolUse-Write"].Record!.Detail);
        Assert.Equal(MapAction.Ignore, results["Notification-idle_prompt-sintetski"].Action);
        Assert.Equal(SessionStatus.Error, results["StopFailure-sintetski"].Record!.State);
        Assert.Equal(MapAction.Delete, results["SessionEnd"].Action);
        Assert.Equal(SessionStatus.Done, results["Stop"].Record!.State);
    }
}
