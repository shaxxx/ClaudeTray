using ClaudeTray.Core;

namespace ClaudeTray;

/// <summary>Dnevnik traya (spec 9): neuhvaćene iznimke idu ovamo, nikad u prozor.</summary>
public static class TrayLog
{
    const long MaxBytes = 1024 * 1024;
    static readonly object Gate = new();

    public static void Install()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Write("Iznimka na UI niti: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("Neuhvaćena iznimka: " + e.ExceptionObject);
    }

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                string path = TrayPaths.TrayLog;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes) File.Move(path, path + ".1", overwrite: true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
