using ClaudeTray.Core;

namespace ClaudeTray.Model;

/// <summary>Koje se sesije prikazuju i koju ikonu ima tray (spec 6.1, 6.4).</summary>
public static class Aggregator
{
    public const long HideRestAfterMs = 15 * 60_000;

    /// <summary>
    /// Samo sesije koje su nešto radile, poredane po prioritetu pa po svježini. Sesije u mirovanju ili
    /// gotove dulje od 15 min skrivaju se, ali barem jedna uvijek ostaje.
    /// </summary>
    public static IReadOnlyList<SessionView> Visible(IEnumerable<SessionView> all, long nowMs)
    {
        var ordered = all.Where(v => v.Record.Started)
                         .OrderByDescending(v => v.Rank)
                         .ThenByDescending(v => v.Record.UpdatedAt)
                         .ToList();
        var visible = ordered.Where(v => !(v.Rank == 1 && nowMs - v.Record.UpdatedAt > HideRestAfterMs)).ToList();
        if (visible.Count == 0 && ordered.Count > 0) visible.Add(ordered.MaxBy(v => v.Record.UpdatedAt)!);
        return visible;
    }

    public static IconKind Icon(IReadOnlyList<SessionView> visible)
    {
        if (visible.Count == 0) return IconKind.Rest;
        var top = visible.MaxBy(v => v.Rank)!;
        return top.Effective switch
        {
            SessionStatus.Waiting => IconKind.Waiting,
            SessionStatus.Error when !top.ErrorAcknowledged => IconKind.Error,   // potvrđena greška ne boji ikonu (16. 11.)
            SessionStatus.Working => IconKind.Working,
            _ => IconKind.Rest,
        };
    }
}
