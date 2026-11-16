using ClaudeTray.Core;

namespace ClaudeTray.Tests;

[Collection("Env")]
public class TrayPathsTests
{
    [Fact]
    public void TrayPaths_HonorsEnvOverrides()
    {
        using var home = new TempDir();
        using var install = new TempDir();
        using var e1 = new EnvVar("CLAUDE_TRAY_HOME", home.Path);
        using var e2 = new EnvVar("CLAUDE_TRAY_INSTALL_DIR", install.Path);

        Assert.Equal(home.Path, TrayPaths.Home);
        Assert.Equal(Path.Combine(home.Path, "state"), TrayPaths.StateDir);
        Assert.Equal(Path.Combine(home.Path, "quit-intent"), TrayPaths.QuitIntentFile);
        Assert.Equal(Path.Combine(home.Path, "hooks.log"), TrayPaths.HooksLog);
        Assert.Equal(Path.Combine(home.Path, "tray.log"), TrayPaths.TrayLog);
        Assert.Equal(install.Path, TrayPaths.InstallDir);
    }

    [Fact]
    public void TrayPaths_DefaultsUnderLocalAppData()
    {
        using var e1 = new EnvVar("CLAUDE_TRAY_HOME", null);
        using var e2 = new EnvVar("CLAUDE_TRAY_INSTALL_DIR", null);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.Equal(Path.Combine(local, "ClaudeTray"), TrayPaths.Home);
        Assert.Equal(Path.Combine(local, "Programs", "ClaudeTray"), TrayPaths.InstallDir);
    }

    [Fact]
    public void Names_MatchSpec()
    {
        Assert.Equal(@"Local\ClaudeTray", TrayPaths.MutexName);
        Assert.Equal(@"Local\ClaudeTray.Exit", TrayPaths.ExitEventName);
        Assert.Equal("claude-tray-hook.exe", TrayPaths.HookExeName);
        Assert.Equal("ClaudeTray.exe", TrayPaths.TrayExeName);
    }
}
