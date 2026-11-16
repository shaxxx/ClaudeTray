using System.Collections;
using System.Text;

namespace ClaudeTray.Core;

/// <summary>
/// Okruženje za procese koje pokrećemo (spec 5.4, dodatak A). Hook nasljeđuje okruženje extension hosta
/// VS Codea (ELECTRON_RUN_AS_NODE=1, VSCODE_*) i Claudea (CLAUDE_*); ono ne smije prijeći na tray.
/// </summary>
public static class ChildEnvironment
{
    public static bool IsInherited(string name)
    {
        if (name.StartsWith("CLAUDE_TRAY_", StringComparison.OrdinalIgnoreCase)) return true;
        return !(name.StartsWith("VSCODE_", StringComparison.OrdinalIgnoreCase)
                 || name.StartsWith("ELECTRON_", StringComparison.OrdinalIgnoreCase)
                 || name.StartsWith("CLAUDE", StringComparison.OrdinalIgnoreCase)
                 || name.Equals("TERM_PROGRAM", StringComparison.OrdinalIgnoreCase)
                 || name.Equals("TERM_PROGRAM_VERSION", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Blok za CreateProcessW (CREATE_UNICODE_ENVIRONMENT): sortiran, "ime=vrijednost\0…\0".</summary>
    public static string BuildBlock(IEnumerable<KeyValuePair<string, string>> vars)
    {
        var sb = new StringBuilder();
        foreach (var (name, value) in vars.Where(v => IsInherited(v.Key)).OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
            sb.Append(name).Append('=').Append(value).Append('\0');
        return sb.Append('\0').ToString();
    }

    public static Dictionary<string, string> Current()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry e in Environment.GetEnvironmentVariables())
            if (e.Key is string k && e.Value is string v) d[k] = v;
        return d;
    }
}
