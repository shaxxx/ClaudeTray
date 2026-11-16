using System.Diagnostics;
using ClaudeTray.Core;
using ClaudeTray.Platform;

namespace ClaudeTray;

/// <summary>Naslov prozora VS Codea: <c>[● ]&lt;aktivni editor&gt; - &lt;mapa&gt; - Visual Studio Code</c> (dodatak A, P4).</summary>
public static class VsCodeTitles
{
    const string Suffix = " - Visual Studio Code";
    const string DirtyPrefix = "● ";

    public static bool Matches(string windowTitle, string projectDir)
    {
        string folder = FolderName(projectDir);
        if (folder.Length == 0) return false;
        string t = windowTitle.StartsWith(DirtyPrefix, StringComparison.Ordinal) ? windowTitle[DirtyPrefix.Length..] : windowTitle;
        if (!t.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase)) return false;
        return t.StartsWith(folder + " - ", StringComparison.OrdinalIgnoreCase)
            || t.Contains(" - " + folder + " - ", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mapa ili njezin najbliži roditelj za koju postoji prozor. CLI u terminalu VS Codea može raditi u podmapi,
    /// a <c>code "&lt;podmapa&gt;"</c> bi otvorio novi prozor umjesto da fokusira postojeći.
    /// </summary>
    public static string? ResolveDir(string projectDir, IEnumerable<string> titles)
    {
        var list = titles as IReadOnlyCollection<string> ?? titles.ToList();
        for (string? d = Trim(projectDir); !string.IsNullOrEmpty(d); d = Path.GetDirectoryName(d))
        {
            string dir = d;
            if (list.Any(t => Matches(t, dir))) return Trim(dir);
        }
        return null;
    }

    static string FolderName(string dir) => Path.GetFileName(Trim(dir));

    static string Trim(string dir) => dir.TrimEnd('\\', '/');
}

public static class VsCodeUri
{
    public static string ForSession(string sessionId) =>
        "vscode://anthropic.claude-code/open?session=" + Uri.EscapeDataString(sessionId);
}

/// <summary>Pokretanje <c>code "&lt;mapa&gt;"</c>, koje dovodi postojeći prozor u prvi plan (dodatak A (d)).</summary>
public static class VsCodeLauncher
{
    public static string? FindCodeCmd()
    {
        string user = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   "Programs", "Microsoft VS Code", "bin", "code.cmd");
        if (File.Exists(user)) return user;
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                string candidate = Path.Combine(dir.Trim('"'), "code.cmd");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }

    /// <summary>
    /// Argumenti za cmd.exe: <c>/c ""&lt;codeCmd&gt;" "&lt;mapa&gt;""</c>. Kosa crta na kraju mape bi pred navodnikom
    /// postala escape za Node, pa se uklanja.
    /// </summary>
    public static string BuildArgs(string codeCmd, string projectDir)
    {
        string dir = projectDir.TrimEnd('\\', '/');
        if (dir.EndsWith(':')) dir += @"\.";
        return $"/c \"\"{codeCmd}\" \"{dir}\"\"";
    }

    public static Process? Launch(string codeCmd, string projectDir)
    {
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), BuildArgs(codeCmd, projectDir))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory,
        };
        foreach (string key in psi.Environment.Keys.ToList())
            if (!ChildEnvironment.IsInherited(key)) psi.Environment.Remove(key);
        return Process.Start(psi);
    }
}

public enum VsCodeStep1 { Nothing, RunCode, ActivateFirst }

/// <summary>
/// Korak 1 nikad ne otvara novi prozor. Bez prozora čiji naslov odgovara mapi ne radi se ništa, jer bi
/// <c>code "&lt;mapa&gt;"</c> otvorio novi prozor. Za CLI u terminalu VS Codea mapa je nađena samo po imenu (možda
/// nadmapa ili istoimena tuđa mapa), pa se umjesto <c>code</c> izravno fokusira nađeni prozor.
/// </summary>
public static class VsCodeSteps
{
    public static VsCodeStep1 Step1(bool withUri, int candidates, bool codeAvailable) =>
        candidates == 0 ? VsCodeStep1.Nothing
        : withUri && codeAvailable ? VsCodeStep1.RunCode
        : VsCodeStep1.ActivateFirst;
}

/// <summary>Korak 1 (pravi prozor VS Codea) i korak 2 (URI sesije) iz spec 7.</summary>
sealed class VsCodeFocuser
{
    const int PollMs = 50, ForegroundWaitMs = 1000, LaunchWaitMs = 5000;

    public async Task<string> FocusAsync(SessionRecord r, bool withUri)
    {
        var windows = CodeWindows();
        string? dir = withUri ? r.ProjectDir : VsCodeTitles.ResolveDir(r.ProjectDir, windows.Select(w => w.Title));
        if (string.IsNullOrEmpty(dir)) return "nema prozora VS Codea za " + r.ProjectDir;
        var candidates = windows.Where(w => VsCodeTitles.Matches(w.Title, dir)).ToList();

        string? code = VsCodeLauncher.FindCodeCmd();
        switch (VsCodeSteps.Step1(withUri, candidates.Count, code is not null))
        {
            case VsCodeStep1.RunCode:
                Win32Windows.AllowAnyForeground();
                using (var p = VsCodeLauncher.Launch(code!, dir)) await WaitForExit(p);
                break;
            case VsCodeStep1.ActivateFirst:
                Win32Windows.Activate(candidates[0].Handle);
                break;
            default:
                return "nema prozora VS Codea za " + dir;
        }

        if (candidates.Count != 1) return $"{candidates.Count} kandidata za {dir}, URI se ne šalje";
        IntPtr target = candidates[0].Handle;
        if (!await WaitForeground(target))
        {
            Win32Windows.Activate(target);   // code nije doveo prozor; pokušaj izravno
            if (!await WaitForeground(target)) return "prozor nije došao u prvi plan, URI se ne šalje";
        }
        if (!withUri) return "prozor u prvom planu";

        Win32Windows.AllowAnyForeground();
        using (Process.Start(new ProcessStartInfo(VsCodeUri.ForSession(r.SessionId)) { UseShellExecute = true })) { }
        return "prozor u prvom planu, URI poslan";
    }

    static List<TopWindow> CodeWindows()
    {
        var exe = new Dictionary<int, string?>();
        return Win32Windows.TopLevel()
                           .Where(w => string.Equals(exe.TryGetValue(w.Pid, out var e) ? e : exe[w.Pid] = Win32Windows.ExeName(w.Pid),
                                                     "Code.exe", StringComparison.OrdinalIgnoreCase))
                           .ToList();
    }

    static async Task WaitForExit(Process? p)
    {
        if (p is null) return;
        using var cts = new CancellationTokenSource(LaunchWaitMs);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { }
    }

    static async Task<bool> WaitForeground(IntPtr hwnd)
    {
        for (int waited = 0; ; waited += PollMs)
        {
            if (Win32Windows.Foreground() == hwnd) return true;
            if (waited >= ForegroundWaitMs) return false;
            await Task.Delay(PollMs);
        }
    }
}
