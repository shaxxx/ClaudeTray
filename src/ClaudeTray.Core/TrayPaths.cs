namespace ClaudeTray.Core;

/// <summary>Putanje i imena (spec 4.2). Varijable okruženja čitaju se pri svakom pozivu, radi testova.</summary>
public static class TrayPaths
{
    public const string MutexName = @"Local\ClaudeTray";
    public const string ExitEventName = @"Local\ClaudeTray.Exit";
    public const string HookExeName = "claude-tray-hook.exe";
    public const string TrayExeName = "ClaudeTray.exe";

    static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Home => FromEnv("CLAUDE_TRAY_HOME") ?? Path.Combine(LocalAppData, "ClaudeTray");
    public static string StateDir => Path.Combine(Home, "state");
    public static string QuitIntentFile => Path.Combine(Home, "quit-intent");
    public static string HooksLog => Path.Combine(Home, "hooks.log");
    public static string TrayLog => Path.Combine(Home, "tray.log");
    public static string InstallDir => FromEnv("CLAUDE_TRAY_INSTALL_DIR") ?? Path.Combine(LocalAppData, "Programs", "ClaudeTray");

    static string? FromEnv(string name)
    {
        string? v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}
