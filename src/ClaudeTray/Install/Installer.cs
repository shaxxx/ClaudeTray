using System.Diagnostics;
using ClaudeTray.Core;

namespace ClaudeTray.Install;

/// <param name="StopTray">Gašenje traya; testovi ga zamjenjuju da ne ugase pravi tray na stroju.</param>
public sealed record InstallContext(string SourceDir, string InstallDir, string SettingsPath, string Home,
                                    Func<DateTime> Now, TextWriter Out, Func<TimeSpan, bool>? StopTray = null);

/// <summary><c>--install</c> i <c>--uninstall</c> (spec 10.2, 10.3).</summary>
public static class Installer
{
    public const int Ok = 0, InvalidSettings = 1, CopyFailed = 2, MissingSource = 3;

    const int CopyAttempts = 10, CopyRetryMs = 200;
    const string NewSuffix = ".claudetray-new";
    static readonly TimeSpan TrayExitWait = TimeSpan.FromSeconds(5);
    static readonly string[] ProgramFiles = [TrayPaths.TrayExeName, TrayPaths.HookExeName];

    public static int Install(InstallContext c)
    {
        foreach (string f in ProgramFiles)
        {
            if (!File.Exists(Path.Combine(c.SourceDir, f)))
            {
                c.Out.WriteLine($"Greška: u {c.SourceDir} nema {f}.");
                return MissingSource;
            }
        }

        var root = LoadOrReport(c);
        if (root is null) return InvalidSettings;

        string quit = QuitIntent(c);
        bool held = HoldQuitIntent(quit);
        try
        {
            StopTray(c);
            if (!SameDir(c.SourceDir, c.InstallDir))
            {
                Directory.CreateDirectory(c.InstallDir);
                foreach (string f in ProgramFiles)
                {
                    if (!CopyWithRetry(Path.Combine(c.SourceDir, f), Path.Combine(c.InstallDir, f)))
                    {
                        c.Out.WriteLine($"Greška: {f} se ne može kopirati u {c.InstallDir} (datoteka je zauzeta).");
                        return CopyFailed;
                    }
                }
            }
        }
        finally
        {
            if (held) TryDelete(quit);
        }

        string? backup = SettingsFile.Backup(c.SettingsPath, c.Now());
        int added = SettingsMerger.Add(root, Path.Combine(c.InstallDir, TrayPaths.HookExeName));
        SettingsFile.SaveAtomic(c.SettingsPath, root);

        c.Out.WriteLine("Instalirano u: " + c.InstallDir);
        c.Out.WriteLine("Postavke: " + c.SettingsPath);
        c.Out.WriteLine("Sigurnosna kopija: " + (backup ?? "nije bila potrebna (datoteka nije postojala)"));
        c.Out.WriteLine("Dodano hookova: " + added);
        if (SettingsMerger.HooksDisabled(root))
            c.Out.WriteLine("Upozorenje: disableAllHooks je uključen, hookovi se neće pokretati.");
        return Ok;
    }

    public static int Uninstall(InstallContext c)
    {
        var root = File.Exists(c.SettingsPath) ? LoadOrReport(c) : null;
        if (root is null && File.Exists(c.SettingsPath)) return InvalidSettings;

        HoldQuitIntent(QuitIntent(c));   // briše se zajedno s ostatkom Home mape
        StopTray(c);
        if (root is not null)
        {
            string? backup = SettingsFile.Backup(c.SettingsPath, c.Now());
            int removed = SettingsMerger.Remove(root, c.InstallDir);
            SettingsFile.SaveAtomic(c.SettingsPath, root);
            c.Out.WriteLine("Postavke: " + c.SettingsPath);
            c.Out.WriteLine("Sigurnosna kopija: " + backup);
            c.Out.WriteLine("Uklonjeno hookova: " + removed);
        }
        else c.Out.WriteLine("Postavke ne postoje: " + c.SettingsPath);

        string state = Path.Combine(c.Home, "state");
        DeleteFiles(state, "*.json", "*.tmp");
        RemoveIfEmpty(state, c.Out);
        DeleteFiles(c.Home, "quit-intent", "hooks.log*", "tray.log*");
        RemoveIfEmpty(c.Home, c.Out);

        if (SameDir(AppContext.BaseDirectory, c.InstallDir)) ScheduleSelfDelete(c);
        else
        {
            DeleteFiles(c.InstallDir, ProgramFiles.SelectMany(f => new[] { f, f + NewSuffix }).ToArray());
            RemoveIfEmpty(c.InstallDir, c.Out);
        }
        return Ok;
    }

    /// <summary>Postavi <c>Local\ClaudeTray.Exit</c> i čekaj dok tray ne otpusti mutex.</summary>
    public static bool SignalTrayExit(TimeSpan wait)
    {
        if (EventWaitHandle.TryOpenExisting(TrayPaths.ExitEventName, out var exit))
            using (exit) exit.Set();
        var sw = Stopwatch.StartNew();
        while (Mutex.TryOpenExisting(TrayPaths.MutexName, out var mutex))
        {
            mutex.Dispose();
            if (sw.Elapsed >= wait) return false;
            Thread.Sleep(100);
        }
        return true;
    }

    static System.Text.Json.Nodes.JsonObject? LoadOrReport(InstallContext c)
    {
        try { return SettingsFile.Load(c.SettingsPath); }
        catch (InvalidDataException e)
        {
            c.Out.WriteLine("Greška: " + e.Message);
            c.Out.WriteLine("Ništa nije promijenjeno. Ispravite datoteku i pokrenite ponovno.");
            return null;
        }
    }

    static string QuitIntent(InstallContext c) => Path.Combine(c.Home, "quit-intent");

    /// <summary>
    /// Dok se tray gasi i exe zamjenjuje, hook druge sesije ne smije pokrenuti stari tray (TrayLauncher poštuje
    /// quit-intent). Vraća true ako je oznaku postavio instalater; korisnikovu oznaku ("Izlaz") ne dira.
    /// </summary>
    static bool HoldQuitIntent(string quit)
    {
        if (File.Exists(quit)) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(quit)!);
            File.WriteAllText(quit, "");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    static void StopTray(InstallContext c)
    {
        if (!(c.StopTray ?? SignalTrayExit)(TrayExitWait))
            c.Out.WriteLine("Upozorenje: tray se nije ugasio u 5 s.");
    }

    /// <summary>Kopija pod privremenim imenom pa zamjena, da hook nikad ne bude napola zapisan.</summary>
    static bool CopyWithRetry(string source, string target)
    {
        string tmp = target + NewSuffix;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(source, tmp, overwrite: true);
                File.Move(tmp, target, overwrite: true);
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (attempt >= CopyAttempts)
                {
                    TryDelete(tmp);
                    return false;
                }
                Thread.Sleep(CopyRetryMs);
            }
        }
    }

    /// <summary>Brišu se samo naše datoteke, pa mapa samo ako je ostala prazna.</summary>
    static void DeleteFiles(string dir, params string[] patterns)
    {
        if (!Directory.Exists(dir)) return;
        foreach (string pattern in patterns)
            foreach (string f in Directory.GetFiles(dir, pattern))
                TryDelete(f);
    }

    static void RemoveIfEmpty(string dir, TextWriter o)
    {
        if (!Directory.Exists(dir)) return;
        if (Directory.EnumerateFileSystemEntries(dir).Any())
        {
            o.WriteLine("Mapa nije obrisana jer sadrži i druge datoteke: " + dir);
            return;
        }
        try
        {
            Directory.Delete(dir);
            o.WriteLine("Obrisano: " + dir);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            o.WriteLine($"Mapa se ne može obrisati ({e.Message}): {dir}");
        }
    }

    /// <summary>ClaudeTray.exe se ne može obrisati dok radi: odvojeni cmd ga briše nakon izlaza.</summary>
    static void ScheduleSelfDelete(InstallContext c)
    {
        string dir = c.InstallDir.TrimEnd('\\', '/');
        DeleteFiles(dir, TrayPaths.HookExeName, TrayPaths.HookExeName + NewSuffix, TrayPaths.TrayExeName + NewSuffix);
        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            $"/c ping -n 3 127.0.0.1 >nul & del /q \"{dir}\\{TrayPaths.TrayExeName}\" & rmdir \"{dir}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory,
        };
        using (Process.Start(psi)) { }
        c.Out.WriteLine("Mapa se briše nakon izlaza: " + dir);
    }

    static bool SameDir(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

    static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
