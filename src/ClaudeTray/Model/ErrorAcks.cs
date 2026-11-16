using ClaudeTray.Core;

namespace ClaudeTray.Model;

/// <summary>
/// Potvrđene greške, u memoriji traya. Ključ je (sesija, trenutak greške), pa je nova greška u istoj
/// sesiji opet nepotvrđena (spec 6.1).
/// </summary>
public sealed class ErrorAcks
{
    readonly HashSet<(string SessionId, long UpdatedAt)> _acks = [];

    public void Acknowledge(SessionRecord r) => _acks.Add((r.SessionId, r.UpdatedAt));

    public bool IsAcknowledged(SessionRecord r) => _acks.Contains((r.SessionId, r.UpdatedAt));

    public void Prune(IEnumerable<string> liveSessionIds)
    {
        var live = liveSessionIds.ToHashSet();
        _acks.RemoveWhere(a => !live.Contains(a.SessionId));
    }
}
