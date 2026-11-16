using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClaudeTray.Install;

/// <summary>Čitanje, sigurnosna kopija i atomski zapis korisnikova settings.json (spec 10.2).</summary>
public static class SettingsFile
{
    const int ReplaceAttempts = 20, ReplaceRetryMs = 100;

    static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Locate()
    {
        string? configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        string dir = string.IsNullOrWhiteSpace(configDir)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude")
            : configDir;
        return Path.Combine(dir, "settings.json");
    }

    public static JsonObject Load(string path)
    {
        if (!File.Exists(path)) return new JsonObject();
        string text = File.ReadAllText(path);   // UTF-8 BOM se uklanja
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        JsonNode? node;
        try { node = JsonNode.Parse(text, documentOptions: ReadOptions); }
        catch (JsonException e) { throw new InvalidDataException($"{path} nije ispravan JSON: {e.Message}", e); }
        return node as JsonObject ?? throw new InvalidDataException($"{path} nije JSON objekt.");
    }

    public static string? Backup(string path, DateTime localNow)
    {
        if (!File.Exists(path)) return null;
        string baseName = $"{path}.bak-claudetray-{localNow:yyyyMMdd-HHmmss}";
        string target = baseName;
        for (int i = 1; File.Exists(target); i++) target = $"{baseName}-{i}";
        File.Copy(path, target, overwrite: false);
        return target;
    }

    public static void SaveAtomic(string path, JsonObject root)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        string tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.claudetray-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tmp, root.ToJsonString(WriteOptions) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            // Antivirus ili indeksiranje zna kratko držati datoteku otvorenom bez FILE_SHARE_DELETE; tada zamjena
            // privremeno ne uspijeva, a datoteka ostaje netaknuta, pa se pokušava ponovno (najviše ~2 s).
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(tmp, path, destinationBackupFileName: null);
                    else File.Move(tmp, path);
                    break;
                }
                catch (IOException) when (attempt < ReplaceAttempts)
                {
                    Thread.Sleep(ReplaceRetryMs);
                }
            }
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }
}
