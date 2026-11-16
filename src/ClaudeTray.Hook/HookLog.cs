using ClaudeTray.Core;

namespace ClaudeTray.Hook;

/// <summary>Dnevnik hookova za otklanjanje grešaka, samo uz CLAUDE_TRAY_DEBUG=1.</summary>
static class HookLog
{
    public static void Write(int stdinLength, HookInput? input)
    {
        if (Environment.GetEnvironmentVariable("CLAUDE_TRAY_DEBUG") != "1") return;
        try
        {
            Directory.CreateDirectory(TrayPaths.Home);
            File.AppendAllText(TrayPaths.HooksLog,
                $"{DateTimeOffset.Now:O} [{input?.HookEventName ?? "?"}] session={input?.SessionId} tool={input?.ToolName} " +
                $"type={input?.NotificationType} stdin={stdinLength}{Environment.NewLine}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
