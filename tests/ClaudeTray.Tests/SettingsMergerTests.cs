using System.Text.Json.Nodes;
using ClaudeTray.Install;

namespace ClaudeTray.Tests;

/// <summary>Spajanje naših hookova u settings.json (spec 10.2, 10.3, 10.4; dodatak A (b)).</summary>
public class SettingsMergerTests
{
    const string Dir = @"C:\Users\Ivo\AppData\Local\Programs\ClaudeTray";
    const string Exe = Dir + @"\claude-tray-hook.exe";

    static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void Registrations_MatchSpec()
    {
        Assert.Equal(10, HookSpec.Registrations.Count);
        foreach (var e in new[] { "SessionStart", "SessionEnd", "UserPromptSubmit", "Stop", "StopFailure" })
            Assert.Contains((e, (string?)null), HookSpec.Registrations);
        foreach (var e in new[] { "PreToolUse", "PostToolUse", "PostToolUseFailure", "PermissionRequest" })
            Assert.Contains((e, (string?)"*"), HookSpec.Registrations);
        Assert.Contains(("Notification", (string?)HookSpec.NotificationMatcher), HookSpec.Registrations);
        Assert.Equal("permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input|elicitation_response|elicitation_complete",
                     HookSpec.NotificationMatcher);
    }

    [Fact]
    public void Add_EmptyHooks_10Registrations()
    {
        var root = Parse("""{"hooks":{}}""");
        Assert.Equal(10, SettingsMerger.Add(root, Exe));
        var hooks = root["hooks"]!.AsObject();
        Assert.Equal(10, hooks.Count);
        Assert.Equal("*", (string?)hooks["PreToolUse"]![0]!["matcher"]);
        Assert.Equal(HookSpec.NotificationMatcher, (string?)hooks["Notification"]![0]!["matcher"]);
        Assert.Null(hooks["Stop"]![0]!["matcher"]);
        foreach (var (_, entries) in hooks)
            foreach (var hook in entries![0]!["hooks"]!.AsArray())
                Assert.Equal(5, (int)hook!["timeout"]!);
    }

    [Fact]
    public void Add_NoHooksKey_CreatesIt()
    {
        var root = Parse("""{"model":"opus"}""");
        SettingsMerger.Add(root, Exe);
        Assert.Equal(["model", "hooks"], root.Select(p => p.Key));
    }

    [Fact]
    public void Add_PreservesForeignHooks()
    {
        var root = Parse("""{"hooks":{"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"node x.js"}]}]}}""");
        SettingsMerger.Add(root, Exe);
        var pre = root["hooks"]!["PreToolUse"]!.AsArray();
        Assert.Equal(2, pre.Count);
        Assert.Equal("node x.js", (string?)pre[0]!["hooks"]![0]!["command"]);
        Assert.True(SettingsMerger.IsOurs(pre[1]!["hooks"]![0]!.AsObject(), Dir));
    }

    [Fact]
    public void Add_Twice_Idempotent()
    {
        var once = Parse("""{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"node y.js"}]}]}}""");
        SettingsMerger.Add(once, Exe);
        var twice = once.DeepClone().AsObject();
        SettingsMerger.Add(twice, Exe);
        Assert.Equal(once.ToJsonString(), twice.ToJsonString());
    }

    [Fact]
    public void Add_ReplacesOldInstallPathVariant()
    {
        // Stara instalacija zapisana s '\\' i velikim slovima: nakon Add ostaje točno jedan naš hook po događaju.
        var root = Parse("""{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"\"C:\\USERS\\IVO\\APPDATA\\LOCAL\\PROGRAMS\\CLAUDETRAY\\claude-tray-hook.exe\""}]}]}}""");
        SettingsMerger.Add(root, Exe);
        Assert.Single(root["hooks"]!["Stop"]!.AsArray());
    }

    [Fact]
    public void AddThenRemove_RestoresOriginal()
    {
        const string original = """{"model":"opus","hooks":{},"defaultMode":"bypassPermissions"}""";
        var root = Parse(original);
        SettingsMerger.Add(root, Exe);
        Assert.Equal(10, SettingsMerger.Remove(root, Dir));
        Assert.Equal(Parse(original).ToJsonString(), root.ToJsonString());
    }

    [Fact]
    public void Remove_OnlyOurs_EmptyEntriesAndEventsDropped()
    {
        var root = Parse($$$"""
            {"hooks":{
              "Stop":[{"hooks":[{"type":"command","command":"node a.js"},{"type":"command","command":"{{{Exe.Replace('\\', '/')}}}","args":[]}]}],
              "SessionEnd":[{"hooks":[{"type":"command","command":"{{{Exe.Replace('\\', '/')}}}","args":[]}]}]
            }}
            """);
        Assert.Equal(2, SettingsMerger.Remove(root, Dir));
        Assert.Equal("""{"Stop":[{"hooks":[{"type":"command","command":"node a.js"}]}]}""", root["hooks"]!.ToJsonString());
    }

    [Fact]
    public void Remove_NoHooksKey_Zero()
    {
        var root = Parse("""{"model":"opus"}""");
        Assert.Equal(0, SettingsMerger.Remove(root, Dir));
        Assert.Equal("""{"model":"opus"}""", root.ToJsonString());
    }

    [Theory]
    [InlineData("""{"hooks":[]}""")]
    [InlineData("""{"hooks":{"Stop":{}}}""")]
    public void Add_MalformedHooks_Throws(string json) =>
        Assert.Throws<InvalidDataException>(() => SettingsMerger.Add(Parse(json), Exe));

    [Theory]
    [InlineData(@"C:\Users\Ivo\AppData\Local\Programs\ClaudeTray\claude-tray-hook.exe", true)]
    [InlineData("C:/Users/Ivo/AppData/Local/Programs/ClaudeTray/claude-tray-hook.exe", true)]
    [InlineData("C:/USERS/IVO/APPDATA/LOCAL/PROGRAMS/CLAUDETRAY/CLAUDE-TRAY-HOOK.EXE", true)]
    [InlineData("\"C:/Users/Ivo/AppData/Local/Programs/ClaudeTray/claude-tray-hook.exe\"", true)]
    [InlineData("C:/Users/Ivo/AppData/Local/Programs/ClaudeTrayOld/x.exe", false)]
    [InlineData("node x.js", false)]
    public void IsOurs_PathVariants(string command, bool expected)
    {
        var hook = new JsonObject { ["type"] = "command", ["command"] = command };
        Assert.Equal(expected, SettingsMerger.IsOurs(hook, Dir));
        Assert.Equal(expected, SettingsMerger.IsOurs(hook, Dir + @"\"));
    }

    [Fact]
    public void IsOurs_LooksAtArgs()
    {
        var hook = new JsonObject { ["type"] = "command", ["command"] = "node", ["args"] = new JsonArray("C:/Users/Ivo/AppData/Local/Programs/ClaudeTray/x.js") };
        Assert.True(SettingsMerger.IsOurs(hook, Dir));
    }

    [Fact]
    public void HookEntry_SpaceAndCroatianPath()
    {
        var entry = HookSpec.HookEntry(@"C:\Users\Ivo Čačić\AppData\Local\Programs\ClaudeTray\claude-tray-hook.exe");
        Assert.Equal("""{"type":"command","command":"C:/Users/Ivo Čačić/AppData/Local/Programs/ClaudeTray/claude-tray-hook.exe","args":[],"timeout":5}""",
                     entry.ToJsonString(new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    }

    [Fact]
    public void HooksDisabled_True()
    {
        Assert.True(SettingsMerger.HooksDisabled(Parse("""{"disableAllHooks":true}""")));
        Assert.False(SettingsMerger.HooksDisabled(Parse("""{"disableAllHooks":false}""")));
        Assert.False(SettingsMerger.HooksDisabled(Parse("{}")));
        Assert.False(SettingsMerger.HooksDisabled(Parse("""{"disableAllHooks":"yes"}""")));
    }
}
