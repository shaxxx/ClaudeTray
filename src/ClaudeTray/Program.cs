using ClaudeTray.Core;
using ClaudeTray.Icons;
using ClaudeTray.Install;

namespace ClaudeTray;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--render-icons")
        {
            IconExporter.Export(args[1]);
            IconExporter.ExportPreview(args[1]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "--render-app-icon")
        {
            AppIconRenderer.WriteIco(args[1]);
            return 0;
        }
        if (args.Length == 1 && args[0] is "--install" or "--uninstall") return RunInstaller(args[0] == "--install");

        using var single = new Mutex(true, TrayPaths.MutexName, out bool created);
        if (!created) return 0;   // tray već radi

        TrayLog.Install();
        if (Environment.GetEnvironmentVariable("CLAUDE_TRAY_DEBUG") == "1")
            TrayLog.Write($"pokretanje; ELECTRON_RUN_AS_NODE={Env("ELECTRON_RUN_AS_NODE")} VSCODE_PID={Env("VSCODE_PID")} CLAUDE_PID={Env("CLAUDE_PID")}");

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
        return 0;
    }

    /// <summary>Kodovi izlaza: 0 u redu, 1 neispravan settings.json, 2 kopiranje, 3 nema izvornih datoteka, 4 ostale greške.</summary>
    static int RunInstaller(bool install)
    {
        ConsoleAttach.Attach();
        var c = new InstallContext(AppContext.BaseDirectory, TrayPaths.InstallDir, SettingsFile.Locate(), TrayPaths.Home,
                                   () => DateTime.Now, Console.Out);
        try
        {
            return install ? Installer.Install(c) : Installer.Uninstall(c);
        }
        catch (Exception e)
        {
            Console.Out.WriteLine("Greška: " + e.Message);
            return 4;
        }
    }

    static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "(nema)";
}
