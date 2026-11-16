using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeTray.Install;

/// <summary>
/// Dodaje i uklanja samo naše hookove u settings.json (spec 10.2, 10.3). Tuđi hookovi, ostali ključevi i njihov
/// redoslijed ostaju. Neočekivan oblik "hooks" baca <see cref="InvalidDataException"/> prije ikakve izmjene.
/// </summary>
public static class SettingsMerger
{
    /// <summary>Naredba ili argument sadrži programsku mapu, bez obzira na veličinu slova i smjer kose crte.</summary>
    public static bool IsOurs(JsonObject hook, string installDir)
    {
        string dir = Normalize(installDir).TrimEnd('/') + "/";
        if (AsString(hook["command"]) is { } command && Normalize(command).Contains(dir, StringComparison.Ordinal)) return true;
        if (hook["args"] is JsonArray args)
            foreach (var arg in args)
                if (AsString(arg) is { } a && Normalize(a).Contains(dir, StringComparison.Ordinal)) return true;
        return false;
    }

    public static int Remove(JsonObject root, string installDir)
    {
        if (Hooks(root, create: false) is not { } hooks) return 0;
        int removed = 0;
        foreach (string evt in hooks.Select(p => p.Key).ToList())
        {
            var entries = (JsonArray)hooks[evt]!;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i] is not JsonObject entry || entry["hooks"] is not JsonArray list) continue;
                for (int j = list.Count - 1; j >= 0; j--)
                {
                    if (list[j] is JsonObject hook && IsOurs(hook, installDir))
                    {
                        list.RemoveAt(j);
                        removed++;
                    }
                }
                if (list.Count == 0) entries.RemoveAt(i);
            }
            if (entries.Count == 0) hooks.Remove(evt);
        }
        return removed;
    }

    public static int Add(JsonObject root, string hookExePath)
    {
        string installDir = Path.GetDirectoryName(hookExePath) ?? throw new ArgumentException("putanja bez mape", nameof(hookExePath));
        Hooks(root, create: false);   // provjera oblika prije ikakve izmjene
        Remove(root, installDir);
        var hooks = Hooks(root, create: true)!;
        foreach (var (evt, matcher) in HookSpec.Registrations)
        {
            if (hooks[evt] is not JsonArray entries) hooks[evt] = entries = [];
            var entry = new JsonObject();
            if (matcher is not null) entry["matcher"] = matcher;
            entry["hooks"] = new JsonArray(HookSpec.HookEntry(hookExePath));
            entries.Add(entry);
        }
        return HookSpec.Registrations.Count;
    }

    public static bool HooksDisabled(JsonObject root) =>
        root["disableAllHooks"] is JsonValue v && v.GetValueKind() == JsonValueKind.True;

    /// <summary>Objekt "hooks" čiji su svi događaji nizovi; inače <see cref="InvalidDataException"/>.</summary>
    static JsonObject? Hooks(JsonObject root, bool create)
    {
        switch (root["hooks"])
        {
            case null when !root.ContainsKey("hooks"):
                if (!create) return null;
                var created = new JsonObject();
                root["hooks"] = created;
                return created;
            case JsonObject hooks:
                foreach (var (evt, value) in hooks)
                    if (value is not JsonArray) throw new InvalidDataException($"\"hooks.{evt}\" u settings.json nije niz.");
                return hooks;
            default:
                throw new InvalidDataException("\"hooks\" u settings.json nije objekt.");
        }
    }

    static string? AsString(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.String ? v.GetValue<string>() : null;

    static string Normalize(string s) => s.Replace('\\', '/').ToLowerInvariant();
}
