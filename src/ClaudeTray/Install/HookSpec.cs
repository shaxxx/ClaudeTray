using System.Text.Json.Nodes;

namespace ClaudeTray.Install;

/// <summary>Hookovi koje prijavljujemo (spec 10.4) u obliku <c>args</c> (dodatak A (b)).</summary>
public static class HookSpec
{
    public const string NotificationMatcher =
        "permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input|elicitation_response|elicitation_complete";

    public static IReadOnlyList<(string Event, string? Matcher)> Registrations { get; } =
    [
        ("SessionStart", null),
        ("SessionEnd", null),
        ("UserPromptSubmit", null),
        ("Stop", null),
        ("StopFailure", null),
        ("PreToolUse", "*"),
        ("PostToolUse", "*"),
        ("PostToolUseFailure", "*"),
        ("PermissionRequest", "*"),
        ("Notification", NotificationMatcher),
    ];

    /// <summary>Claude Code pokreće exe izravno, bez ljuske, pa putanja s razmacima ne treba navodnike (P2).</summary>
    public static JsonObject HookEntry(string hookExePath) => new()
    {
        ["type"] = "command",
        ["command"] = hookExePath.Replace('\\', '/'),
        ["args"] = new JsonArray(),
        ["timeout"] = 5,
    };
}
