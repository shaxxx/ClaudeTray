using System.Text;
using System.Text.Json;

namespace ClaudeTray.Core;

/// <summary>Datoteke stanja <c>&lt;id&gt;.json</c>: atomski zapis i čitanje koje ne blokira zapis.</summary>
public static class StateStore
{
    const int MaxIdLength = 64;
    const int MoveAttempts = 10;

    public static string SafeId(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return "unknown";
        var sb = new StringBuilder(Math.Min(sessionId.Length, MaxIdLength));
        foreach (char c in sessionId)
        {
            if (sb.Length == MaxIdLength) break;
            if (char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-') sb.Append(c);
        }
        return sb.Length == 0 ? "unknown" : sb.ToString();
    }

    public static string PathFor(string stateDir, string sessionId) => Path.Combine(stateDir, SafeId(sessionId) + ".json");

    /// <summary>
    /// Zapisuje u privremenu datoteku pa je zamjenjuje preko odredišta. Zamjena s POSIX semantikom
    /// uspijeva i dok tray čita odredište. Ako ona nije dostupna, koristi se File.Move s ponavljanjem;
    /// ako se zapis i tada sudari s drugim istovremenim zapisom iste sesije, odustaje tiho jer je drugi
    /// zapis jednako svjež.
    /// </summary>
    public static void WriteAtomic(string stateDir, SessionRecord r)
    {
        Directory.CreateDirectory(stateDir);
        string target = PathFor(stateDir, r.SessionId);
        string tmp = $"{target}.{Environment.ProcessId}.{Environment.CurrentManagedThreadId}.tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(r, CoreJsonContext.Default.SessionRecord));

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                if (NativeFile.TryPosixReplace(tmp, target)) return;
                File.Move(tmp, target, overwrite: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt == MoveAttempts)
                {
                    TryDelete(tmp);
                    return;
                }
                Thread.Sleep(5);
            }
        }
    }

    public static SessionRecord? TryRead(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize(fs, CoreJsonContext.Default.SessionRecord);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static IReadOnlyList<SessionRecord> ReadAll(string stateDir)
    {
        if (!Directory.Exists(stateDir)) return [];
        var list = new List<SessionRecord>();
        foreach (string f in Directory.EnumerateFiles(stateDir, "*.json"))
        {
            if (!f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            if (TryRead(f) is { } r) list.Add(r);
        }
        return list;
    }

    public static void Delete(string stateDir, string sessionId) => TryDelete(PathFor(stateDir, sessionId));

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
