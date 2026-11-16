namespace ClaudeTray.Core;

public enum MapAction { Write, Delete, Ignore }

public readonly record struct MapResult(MapAction Action, SessionRecord? Record);

/// <summary>Pretvara događaj hooka u novo stanje sesije (spec 5.1, dodatak A). Čista funkcija.</summary>
public static class EventMapper
{
    static readonly HashSet<string> s_waitingTools = new(StringComparer.Ordinal) { "AskUserQuestion", "ExitPlanMode" };
    static readonly HashSet<string> s_askingNotifications = new(StringComparer.Ordinal)
        { "elicitation_dialog", "elicitation_url_dialog", "agent_needs_input" };
    static readonly HashSet<string> s_resumeNotifications = new(StringComparer.Ordinal)
        { "elicitation_response", "elicitation_complete" };

    const int TitleMaxLength = 50;

    /// <summary>Alati čiji poziv znači da Claude čeka korisnika (dodatak A (a)).</summary>
    public static IReadOnlySet<string> WaitingTools => s_waitingTools;

    public static MapResult Apply(SessionRecord? previous, HookInput input, HookEnvironment env, long nowMs)
    {
        if (string.IsNullOrEmpty(input.SessionId)) return Ignore();
        string evt = input.HookEventName ?? "";
        if (evt == "SessionEnd") return new MapResult(MapAction.Delete, null);

        var prev = previous ?? new SessionRecord { SessionId = input.SessionId };
        var common = prev with
        {
            SessionId = input.SessionId,
            Cwd = NonEmpty(input.Cwd, prev.Cwd),
            Transcript = NonEmpty(input.TranscriptPath, prev.Transcript),
            Entrypoint = NonEmpty(env.Entrypoint, prev.Entrypoint),
            TermProgram = NonEmpty(env.TermProgram, prev.TermProgram),
            ProjectDir = prev.ProjectDir.Length > 0 ? prev.ProjectDir : Normalize(NonEmpty(env.ProjectDir, input.Cwd ?? "")),
            Started = evt != "SessionStart",
            UpdatedAt = nowMs,
        };
        long carriedTurn = prev.TurnStartedAt != 0 ? prev.TurnStartedAt : nowMs;
        string tool = input.ToolName ?? "";

        switch (evt)
        {
            case "SessionStart" when IsCompact(input) && previous is not null:
                // Sažimanje (i automatsko usred poteza) nastavlja istu sesiju: stanje, potez i UpdatedAt (ključ potvrde
                // greške) ostaju; osvježavaju se samo putanje i okruženje.
                return Write(common with { Started = prev.Started, UpdatedAt = prev.UpdatedAt });

            case "SessionStart":
                return Write(common with { State = SessionStatus.Idle, Detail = "", Tool = "", TurnStartedAt = 0 });

            case "UserPromptSubmit":
                return Write(common with
                {
                    State = SessionStatus.Working, Detail = Labels.Thinking, Tool = "", TurnStartedAt = nowMs,
                    Title = NonEmpty(input.SessionTitle, NonEmpty(IsSynthetic(prev.Title) ? "" : prev.Title, MakeTitle(input.Prompt))),
                });

            case "PreToolUse":
                return s_waitingTools.Contains(tool)
                    ? Write(common with { State = SessionStatus.Waiting, Detail = Labels.Asking, Tool = tool, TurnStartedAt = carriedTurn })
                    : Write(common with { State = SessionStatus.Working, Detail = Labels.ForTool(tool), Tool = tool, TurnStartedAt = carriedTurn });

            case "PostToolUse":
            case "PostToolUseFailure":
                return Write(common with { State = SessionStatus.Working, Detail = Labels.Thinking, Tool = "", TurnStartedAt = carriedTurn });

            case "PermissionRequest":
                return Write(common with
                {
                    State = SessionStatus.Waiting,
                    Detail = s_waitingTools.Contains(tool) ? Labels.Asking : Labels.Permission,
                    Tool = tool, TurnStartedAt = carriedTurn,
                });

            case "Notification":
                string type = input.NotificationType ?? "";
                if (type == "permission_prompt")
                {
                    // Stiže ~6 s nakon zahtjeva (P1); ako je potez u međuvremenu završio, obavijest je zastarjela.
                    if (previous?.State is SessionStatus.Done or SessionStatus.Idle) return Ignore();
                    string detail = prev.State == SessionStatus.Waiting && prev.Detail.Length > 0 ? prev.Detail : Labels.Permission;
                    return Write(common with { State = SessionStatus.Waiting, Detail = detail, TurnStartedAt = carriedTurn });
                }
                if (s_askingNotifications.Contains(type))
                    return Write(common with { State = SessionStatus.Waiting, Detail = Labels.Asking, TurnStartedAt = carriedTurn });
                if (s_resumeNotifications.Contains(type))
                    return Write(common with { State = SessionStatus.Working, Detail = Labels.Thinking, TurnStartedAt = carriedTurn });
                return Ignore();

            case "Stop":
                return Write(common with { State = SessionStatus.Done, Detail = Labels.Done, Tool = "", TurnStartedAt = 0 });

            case "StopFailure":
                return Write(common with { State = SessionStatus.Error, Detail = Labels.ForError(input.Error), Tool = "", TurnStartedAt = 0 });

            default:
                return Ignore();
        }
    }

    /// <summary>SessionStart nakon sažimanja konteksta nije nova sesija.</summary>
    public static bool IsCompact(HookInput input) => input.HookEventName == "SessionStart" && input.Source == "compact";

    static MapResult Write(SessionRecord r) => new(MapAction.Write, r);
    static MapResult Ignore() => new(MapAction.Ignore, null);

    static string NonEmpty(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    static string Normalize(string path)
    {
        if (path.Length == 0) return path;
        try { return Path.GetFullPath(path); }
        catch (Exception) { return path; }
    }

    /// <summary>Poruke koje Claude Code ubacuje kao prompt (&lt;agent-message&gt;, &lt;task-notification&gt; …).</summary>
    static bool IsSynthetic(string? text) => text is not null && text.TrimStart().StartsWith('<');

    /// <summary>
    /// Preskače početne zatvorene blokove <c>&lt;oznaka …&gt;…&lt;/oznaka&gt;</c> koje VS Code (<c>ide_opened_file</c>,
    /// <c>ide_selection</c>) i Claude Code (<c>command-name</c> …) dodaju ispred korisnikova teksta.
    /// </summary>
    static string SkipLeadingTagBlocks(string text)
    {
        string s = text.TrimStart();
        while (s.StartsWith('<'))
        {
            int nameEnd = s.IndexOfAny([' ', '>', '\n', '\r', '\t'], 1);
            if (nameEnd <= 1) break;
            string close = "</" + s[1..nameEnd] + ">";
            int end = s.IndexOf(close, StringComparison.Ordinal);
            if (end < 0) break;
            s = s[(end + close.Length)..].TrimStart();
        }
        return s;
    }

    static string MakeTitle(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return "";
        prompt = SkipLeadingTagBlocks(prompt);
        if (prompt.Length == 0 || IsSynthetic(prompt)) return "";
        string collapsed = string.Join(' ', prompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= TitleMaxLength ? collapsed : collapsed[..TitleMaxLength];
    }
}
