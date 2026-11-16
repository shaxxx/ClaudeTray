using ClaudeTray.Core;

namespace ClaudeTray.Model;

public enum IconKind { Rest, Working, Waiting, Error }

/// <summary>Sesija kakvu prikazujemo: zapis hooka, stvarno stanje i je li greška potvrđena (spec 6.1).</summary>
public sealed record SessionView(SessionRecord Record, SessionStatus Effective, bool ErrorAcknowledged)
{
    /// <summary>čeka te (5) &gt; greška (4) &gt; radi (3) &gt; potvrđena greška (2) &gt; gotovo/mirovanje (1).</summary>
    public int Rank => Effective switch
    {
        SessionStatus.Waiting => 5,
        SessionStatus.Error => ErrorAcknowledged ? 2 : 4,
        SessionStatus.Working => 3,
        _ => 1,
    };
}
