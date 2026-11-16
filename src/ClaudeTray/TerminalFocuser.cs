using ClaudeTray.Core;
using ClaudeTray.Platform;

namespace ClaudeTray;

/// <summary>
/// CLI u terminalu (spec 7): od <c>claude.exe</c> prema roditeljima do prvog procesa s vidljivim prozorom najviše razine.
/// Tab se ne bira.
/// </summary>
static class TerminalFocuser
{
    public static string Focus(int pid)
    {
        var windows = Win32Windows.TopLevel();
        foreach (int p in NativeProcess.AncestorChain(pid).Prepend(pid))
        {
            // Explorer je roditelj ljuske pokrenute iz izbornika Start; njegovi prozori nisu terminal.
            if (string.Equals(Win32Windows.ExeName(p), "explorer.exe", StringComparison.OrdinalIgnoreCase)) break;
            var w = windows.FirstOrDefault(x => x.Pid == p);
            if (w.Handle == IntPtr.Zero) continue;
            return Win32Windows.Activate(w.Handle) ? $"terminal \"{w.Title}\" u prvom planu" : $"terminal \"{w.Title}\" odbio prvi plan";
        }
        return "terminal nije nađen";
    }
}
