using ClaudeTray.Core;

namespace ClaudeTray.Model;

public enum FocusKind { None, VsCodeWindowAndUri, VsCodeWindowOnly, TerminalWindow }

/// <summary>Što klik na sesiju radi (spec 7, dodatak A (f)).</summary>
public static class FocusPlanner
{
    public static FocusKind Plan(SessionRecord r, bool processAlive)
    {
        if (!processAlive) return FocusKind.None;
        if (r.Entrypoint == "claude-vscode") return FocusKind.VsCodeWindowAndUri;
        if (string.Equals(r.TermProgram, "vscode", StringComparison.OrdinalIgnoreCase)) return FocusKind.VsCodeWindowOnly;
        return FocusKind.TerminalWindow;   // cli u drugom terminalu, sdk-cli i sve ostalo
    }
}
