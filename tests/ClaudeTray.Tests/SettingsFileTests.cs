using System.Text;
using System.Text.Json.Nodes;
using ClaudeTray.Install;

namespace ClaudeTray.Tests;

/// <summary>Čitanje, sigurnosna kopija i atomski zapis settings.json (spec 10.2).</summary>
[Collection("Env")]
public class SettingsFileTests
{
    [Fact]
    public void Load_Bom_CommentsTrailingComma_OK()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, "{\n  // komentar\n  \"model\": \"opus\",\n  \"x\": [1, 2,],\n}\n", new UTF8Encoding(true));
        var root = SettingsFile.Load(path);
        Assert.Equal("opus", (string?)root["model"]);
        Assert.Equal(2, root["x"]!.AsArray().Count);
    }

    [Fact]
    public void Load_Invalid_Throws()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, "{\"model\": ");
        Assert.Throws<InvalidDataException>(() => SettingsFile.Load(path));
    }

    [Fact]
    public void Load_ArrayRoot_Throws()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, "[]");
        Assert.Throws<InvalidDataException>(() => SettingsFile.Load(path));
    }

    [Fact]
    public void Load_Missing_Empty()
    {
        using var t = new TempDir();
        Assert.Empty(SettingsFile.Load(t.Sub("nema.json")));
    }

    [Fact]
    public void Load_WhitespaceOnly_Empty()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, " \r\n");
        Assert.Empty(SettingsFile.Load(path));
    }

    [Fact]
    public void Save_KeepsCroatianUnescaped_NoBom_TrailingNewline()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        SettingsFile.SaveAtomic(path, new JsonObject { ["naziv"] = "Čačić & <x>", ["n"] = new JsonObject { ["a"] = 1 } });
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        string text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"Čačić & <x>\"", text);
        Assert.Equal('\n', text[^1]);
        Assert.Contains("\n  \"n\": {\n    \"a\": 1\n  }", text);
        Assert.DoesNotContain("\r", text);
        Assert.Single(Directory.GetFiles(t.Path));   // privremena datoteka je nestala
    }

    [Fact]
    public void Save_ReplacesExisting()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, "{\"stari\": 1}");
        SettingsFile.SaveAtomic(path, new JsonObject { ["novi"] = 2 });
        Assert.Equal(2, (int)SettingsFile.Load(path)["novi"]!);
        Assert.Single(Directory.GetFiles(t.Path));
    }

    // Ručna provjera 16. 11.: File.Replace povremeno javi "Unable to remove the file to be replaced" kad antivirus
    // ili indeksiranje kratko drži datoteku otvorenom bez FILE_SHARE_DELETE.
    [Fact]
    public void Save_RetriesWhileDestinationBrieflyLocked()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, "{}");
        var holder = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);   // bez FileShare.Delete
        var release = Task.Run(async () => { await Task.Delay(300); holder.Dispose(); });

        SettingsFile.SaveAtomic(path, new JsonObject { ["a"] = 1 });
        release.Wait();
        Assert.Equal(1, (int)SettingsFile.Load(path)["a"]!);
        Assert.Single(Directory.GetFiles(t.Path));
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        using var t = new TempDir();
        string path = Path.Combine(t.Path, ".claude", "settings.json");
        SettingsFile.SaveAtomic(path, new JsonObject());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void KeyOrderPreserved()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, """{"model":"opus","hooks":{},"defaultMode":"bypassPermissions"}""");
        var root = SettingsFile.Load(path);
        SettingsMerger.Add(root, @"C:\Programs\ClaudeTray\claude-tray-hook.exe");
        SettingsFile.SaveAtomic(path, root);
        Assert.Equal(["model", "hooks", "defaultMode"], SettingsFile.Load(path).Select(p => p.Key));
    }

    [Fact]
    public void Backup_NameAndCollision()
    {
        using var t = new TempDir();
        string path = t.Sub("settings.json");
        File.WriteAllText(path, "{\"a\":1}");
        var now = new DateTime(2026, 11, 13, 14, 5, 11);

        Assert.Equal(path + ".bak-claudetray-20261113-140511", SettingsFile.Backup(path, now));
        Assert.Equal(path + ".bak-claudetray-20261113-140511-1", SettingsFile.Backup(path, now));
        Assert.Equal(path + ".bak-claudetray-20261113-140511-2", SettingsFile.Backup(path, now));
        Assert.Equal("{\"a\":1}", File.ReadAllText(path + ".bak-claudetray-20261113-140511-2"));
        Assert.Null(SettingsFile.Backup(t.Sub("nema.json"), now));
    }

    [Fact]
    public void Locate_HonorsClaudeConfigDir()
    {
        using var t = new TempDir();
        using (new EnvVar("CLAUDE_CONFIG_DIR", t.Path))
            Assert.Equal(Path.Combine(t.Path, "settings.json"), SettingsFile.Locate());
        using (new EnvVar("CLAUDE_CONFIG_DIR", null))
            Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json"),
                         SettingsFile.Locate());
    }
}
