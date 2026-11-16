using ClaudeTray.Core;
using ClaudeTray.Model;
using ClaudeTray.Platform;

namespace ClaudeTray;

/// <summary>Klik na sesiju u izborniku dovodi njezin prozor u prvi plan (spec 7). Nikad ne baca iznimku.</summary>
public sealed class Focuser
{
    readonly VsCodeFocuser _vscode = new();

    public void Focus(SessionView v) => _ = FocusAsync(v.Record);

    async Task FocusAsync(SessionRecord r)
    {
        try
        {
            Win32Windows.AllowAnyForeground();
            bool alive = r.Pid > 0 && NativeProcess.IsAlive(r.Pid, r.PidStartedAt);
            var kind = FocusPlanner.Plan(r, alive);
            string outcome = kind switch
            {
                FocusKind.VsCodeWindowAndUri => await _vscode.FocusAsync(r, withUri: true),
                FocusKind.VsCodeWindowOnly => await _vscode.FocusAsync(r, withUri: false),
                FocusKind.TerminalWindow => TerminalFocuser.Focus(r.Pid),
                _ => "proces nije živ",
            };
            TrayLog.Write($"Fokus {r.SessionId} ({kind}): {outcome}");
        }
        catch (Exception e)
        {
            TrayLog.Write($"Fokus {r.SessionId}: {e}");
        }
    }
}
