using System.Text;
using System.Text.Json;

namespace ClaudeTray.Model;

/// <summary>
/// Mreža 2 (spec 9, dodatak A (e)): je li zadnji redak tipa user/assistant u transkriptu oznaka prekida.
/// Ostali redovi se preskaču, uključujući api-request-blob koji nosi kopije starih poruka.
/// Rezultat se pamti dok se ne promijeni vrijeme izmjene datoteke.
/// </summary>
public sealed class TranscriptProbe : ITranscriptProbe
{
    const int TailBytes = 64 * 1024;
    const string Marker = "interrupted by user";

    readonly Dictionary<string, (DateTime Modified, bool Result)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool EndsWithInterrupt(string transcriptPath)
    {
        try
        {
            var info = new FileInfo(transcriptPath);
            if (!info.Exists) return false;
            if (_cache.TryGetValue(transcriptPath, out var hit) && hit.Modified == info.LastWriteTimeUtc) return hit.Result;
            bool result = Compute(transcriptPath);
            _cache[transcriptPath] = (info.LastWriteTimeUtc, result);
            return result;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    static bool Compute(string path)
    {
        string tail = ReadTail(path, out bool fromStart);
        var lines = tail.Split('\n');
        int first = fromStart ? 0 : 1;   // prvi redak repa je možda odrezan
        for (int i = lines.Length - 1; i >= first; i--)
        {
            string line = lines[i].Trim();
            if (line.Length == 0) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                string? type = root.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                if (type == "assistant") return false;
                if (type == "user") return UserTextContainsMarker(root);
            }
            catch (JsonException) { }
        }
        return false;
    }

    static bool UserTextContainsMarker(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content)) return false;
        if (content.ValueKind == JsonValueKind.String) return content.GetString()!.Contains(Marker, StringComparison.Ordinal);
        if (content.ValueKind != JsonValueKind.Array) return false;
        foreach (var block in content.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var bt) && bt.ValueEquals("text")
                && block.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                && text.GetString()!.Contains(Marker, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static string ReadTail(string path, out bool fromStart)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long start = Math.Max(0, fs.Length - TailBytes);
        fromStart = start == 0;
        fs.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[fs.Length - start];
        fs.ReadExactly(buffer);
        return Encoding.UTF8.GetString(buffer);
    }
}
