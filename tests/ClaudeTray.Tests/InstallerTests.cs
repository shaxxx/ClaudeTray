using System.Text.Json.Nodes;
using ClaudeTray.Install;

namespace ClaudeTray.Tests;

/// <summary>--install i --uninstall u privremenim mapama, s lažnim exe datotekama (spec 10.2, 10.3).</summary>
public sealed class InstallerTests : IDisposable
{
    readonly TempDir _t = new();
    readonly StringWriter _out = new();
    int _stops;
    DateTime _now = new(2026, 11, 13, 14, 5, 11);

    string Source => _t.Sub("src");
    string InstallDir => _t.Sub("install");
    string Settings => Path.Combine(_t.Path, "cfg", "settings.json");
    string Home => _t.Sub("home");

    public InstallerTests()
    {
        Directory.CreateDirectory(Source);
        File.WriteAllText(Path.Combine(Source, "ClaudeTray.exe"), "MZ tray");
        File.WriteAllText(Path.Combine(Source, "claude-tray-hook.exe"), "MZ hook");
    }

    public void Dispose() => _t.Dispose();

    InstallContext Ctx() => new(Source, InstallDir, Settings, Home, () => _now, _out, _ => { _stops++; return true; });

    void WriteSettings(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        File.WriteAllText(Settings, json);
    }

    static int HookCount(JsonObject root) =>
        root["hooks"]!.AsObject().Sum(e => e.Value!.AsArray().Sum(entry => entry!["hooks"]!.AsArray().Count));

    [Fact]
    public void Install_NoSettings_CreatesWithHooks()
    {
        Assert.Equal(0, Installer.Install(Ctx()));
        var root = SettingsFile.Load(Settings);
        Assert.Equal(10, root["hooks"]!.AsObject().Count);
        Assert.Equal("MZ hook", File.ReadAllText(Path.Combine(InstallDir, "claude-tray-hook.exe")));
        Assert.Equal("MZ tray", File.ReadAllText(Path.Combine(InstallDir, "ClaudeTray.exe")));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Settings)!));
        string text = _out.ToString();
        Assert.Contains("Instalirano u: " + InstallDir, text);
        Assert.Contains("Postavke: " + Settings, text);
        Assert.Contains("Sigurnosna kopija: nije bila potrebna (datoteka nije postojala)", text);
        Assert.Contains("Dodano hookova: 10", text);
        Assert.Equal(1, _stops);
    }

    [Fact]
    public void Install_Existing_BackupIdenticalAndMerged()
    {
        WriteSettings("""{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"node x.js"}]}]}}""");
        byte[] original = File.ReadAllBytes(Settings);

        Assert.Equal(0, Installer.Install(Ctx()));
        string backup = Settings + ".bak-claudetray-20261113-140511";
        Assert.Equal(original, File.ReadAllBytes(backup));
        Assert.Contains("Sigurnosna kopija: " + backup, _out.ToString());
        var root = SettingsFile.Load(Settings);
        Assert.Equal("node x.js", (string?)root["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]);
        Assert.Equal(11, HookCount(root));
    }

    [Fact]
    public void Install_InvalidSettings_Exit1_NothingChanged()
    {
        WriteSettings("{\"model\": ");
        byte[] original = File.ReadAllBytes(Settings);

        Assert.Equal(1, Installer.Install(Ctx()));
        Assert.False(Directory.Exists(InstallDir));
        Assert.Equal(original, File.ReadAllBytes(Settings));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Settings)!));
        Assert.Equal(0, _stops);
    }

    [Fact]
    public void Install_Twice_SameSettings()
    {
        WriteSettings("""{"model":"opus"}""");
        Assert.Equal(0, Installer.Install(Ctx()));
        string once = File.ReadAllText(Settings);
        _now = _now.AddMinutes(1);
        Assert.Equal(0, Installer.Install(Ctx()));
        Assert.Equal(once, File.ReadAllText(Settings));
    }

    [Fact]
    public void Install_MissingSource_Exit3()
    {
        File.Delete(Path.Combine(Source, "claude-tray-hook.exe"));
        Assert.Equal(3, Installer.Install(Ctx()));
        Assert.False(Directory.Exists(InstallDir));
        Assert.False(File.Exists(Settings));
    }

    [Fact]
    public void Install_TargetLocked_Exit2_SettingsUntouched()
    {
        WriteSettings("""{"model":"opus"}""");
        byte[] original = File.ReadAllBytes(Settings);
        Directory.CreateDirectory(InstallDir);
        using (new FileStream(Path.Combine(InstallDir, "claude-tray-hook.exe"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(2, Installer.Install(Ctx()));
        Assert.Equal(original, File.ReadAllBytes(Settings));
        Assert.Empty(Directory.GetFiles(InstallDir, "*.claudetray-new"));
    }

    // Završni pregled: dok se tray gasi i exe kopira, hook druge sesije ne smije ponovno pokrenuti stari tray.
    [Fact]
    public void Install_HoldsQuitIntentWhileReplacing_ThenRemovesIt()
    {
        string quit = Path.Combine(Home, "quit-intent");
        bool heldDuringStop = false;
        var c = Ctx() with { StopTray = _ => { heldDuringStop = File.Exists(quit); return true; } };
        Assert.Equal(0, Installer.Install(c));
        Assert.True(heldDuringStop);
        Assert.False(File.Exists(quit));
    }

    [Fact]
    public void Install_KeepsUsersQuitIntent()
    {
        Directory.CreateDirectory(Home);
        string quit = Path.Combine(Home, "quit-intent");
        File.WriteAllText(quit, "");
        Assert.Equal(0, Installer.Install(Ctx()));
        Assert.True(File.Exists(quit));
    }

    [Fact]
    public void Install_CopyFails_RemovesTemporaryQuitIntent()
    {
        Directory.CreateDirectory(InstallDir);
        using (new FileStream(Path.Combine(InstallDir, "claude-tray-hook.exe"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            Assert.Equal(2, Installer.Install(Ctx()));
        Assert.False(File.Exists(Path.Combine(Home, "quit-intent")));
    }

    [Fact]
    public void Uninstall_HoldsQuitIntentWhileStopping()
    {
        Assert.Equal(0, Installer.Install(Ctx()));
        string quit = Path.Combine(Home, "quit-intent");
        bool heldDuringStop = false;
        var c = Ctx() with { StopTray = _ => { heldDuringStop = File.Exists(quit); return true; } };
        Assert.Equal(0, Installer.Uninstall(c));
        Assert.True(heldDuringStop);
        Assert.False(Directory.Exists(Home));
    }

    [Fact]
    public void Install_HooksDisabled_Warns()
    {
        WriteSettings("""{"disableAllHooks":true}""");
        Assert.Equal(0, Installer.Install(Ctx()));
        Assert.Contains("Upozorenje: disableAllHooks je uključen, hookovi se neće pokretati.", _out.ToString());
    }

    [Fact]
    public void Install_FromInstallDir_SkipsCopy()
    {
        var c = Ctx() with { SourceDir = Source, InstallDir = Source };
        Assert.Equal(0, Installer.Install(c));
        Assert.Equal("MZ hook", File.ReadAllText(Path.Combine(Source, "claude-tray-hook.exe")));
    }

    [Fact]
    public void Uninstall_RestoresAndDeletesDirs()
    {
        const string original = """{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"node x.js"}]}]}}""";
        WriteSettings(original);
        Assert.Equal(0, Installer.Install(Ctx()));
        Directory.CreateDirectory(Path.Combine(Home, "state"));
        File.WriteAllText(Path.Combine(Home, "state", "s1.json"), "{}");
        File.WriteAllText(Path.Combine(Home, "state", "s1.json.12.1.tmp"), "{}");
        File.WriteAllText(Path.Combine(Home, "tray.log"), "x");
        File.WriteAllText(Path.Combine(Home, "tray.log.1"), "x");
        File.WriteAllText(Path.Combine(Home, "hooks.log"), "x");
        File.WriteAllText(Path.Combine(Home, "quit-intent"), "");
        _now = _now.AddMinutes(1);

        Assert.Equal(0, Installer.Uninstall(Ctx()));
        Assert.Equal(JsonNode.Parse(original)!.ToJsonString(), SettingsFile.Load(Settings).ToJsonString());
        Assert.False(Directory.Exists(Home));
        Assert.False(Directory.Exists(InstallDir));
        Assert.Contains("Uklonjeno hookova: 10", _out.ToString());
        Assert.True(File.Exists(Settings + ".bak-claudetray-20261113-140611"));
        Assert.Equal(2, _stops);
    }

    [Fact]
    public void Uninstall_KeepsForeignFiles()
    {
        Assert.Equal(0, Installer.Install(Ctx()));
        File.WriteAllText(Path.Combine(InstallDir, "moje.txt"), "ne diraj");
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Home, "biljeske.txt"), "ne diraj");

        Assert.Equal(0, Installer.Uninstall(Ctx()));
        Assert.Equal(["moje.txt"], Directory.GetFiles(InstallDir).Select(Path.GetFileName));
        Assert.Equal(["biljeske.txt"], Directory.GetFiles(Home).Select(Path.GetFileName));
    }

    [Fact]
    public void Uninstall_InvalidSettings_Exit1_KeepsEverything()
    {
        Assert.Equal(0, Installer.Install(Ctx()));
        File.WriteAllText(Settings, "[");
        Assert.Equal(1, Installer.Uninstall(Ctx()));
        Assert.True(File.Exists(Path.Combine(InstallDir, "claude-tray-hook.exe")));
        Assert.Equal("[", File.ReadAllText(Settings));
    }

    [Fact]
    public void Uninstall_NoSettings_DeletesDirs()
    {
        Assert.Equal(0, Installer.Install(Ctx()));
        File.Delete(Settings);
        Assert.Equal(0, Installer.Uninstall(Ctx()));
        Assert.False(Directory.Exists(InstallDir));
        Assert.False(File.Exists(Settings));
    }
}
