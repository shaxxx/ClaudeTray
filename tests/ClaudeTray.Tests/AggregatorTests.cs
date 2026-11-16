using ClaudeTray.Core;
using ClaudeTray.Model;

namespace ClaudeTray.Tests;

public class AggregatorTests
{
    const long Now = 100_000_000;
    const long Min = 60_000;

    internal static SessionView View(string id, SessionStatus status, bool ack = false, long age = 0, bool started = true,
                                     string project = @"C:\p", string title = "", string detail = "", long turnStart = 0) =>
        new(new SessionRecord
        {
            SessionId = id, State = status, Started = started, UpdatedAt = Now - age, ProjectDir = project,
            Title = title, Detail = detail, TurnStartedAt = turnStart,
        }, status, ack);

    [Fact]
    public void Rank_Order()
    {
        Assert.Equal(5, View("a", SessionStatus.Waiting).Rank);
        Assert.Equal(4, View("a", SessionStatus.Error).Rank);
        Assert.Equal(3, View("a", SessionStatus.Working).Rank);
        Assert.Equal(2, View("a", SessionStatus.Error, ack: true).Rank);
        Assert.Equal(1, View("a", SessionStatus.Done).Rank);
        Assert.Equal(1, View("a", SessionStatus.Idle).Rank);
    }

    [Fact]
    public void Icon_Cases()
    {
        Assert.Equal(IconKind.Rest, Aggregator.Icon([]));
        Assert.Equal(IconKind.Waiting, Icon(View("a", SessionStatus.Waiting), View("b", SessionStatus.Error)));
        Assert.Equal(IconKind.Error, Icon(View("a", SessionStatus.Error), View("b", SessionStatus.Working)));
        Assert.Equal(IconKind.Working, Icon(View("a", SessionStatus.Error, ack: true), View("b", SessionStatus.Working)));
        Assert.Equal(IconKind.Rest, Icon(View("a", SessionStatus.Error, ack: true), View("b", SessionStatus.Done)));   // korisnik 16. 11.: potvrđena greška ne boji ikonu
        Assert.Equal(IconKind.Rest, Icon(View("a", SessionStatus.Error, ack: true)));
        Assert.Equal(IconKind.Rest, Icon(View("a", SessionStatus.Done), View("b", SessionStatus.Idle)));
    }

    static IconKind Icon(params SessionView[] views) => Aggregator.Icon(Aggregator.Visible(views, Now));

    [Fact]
    public void SpecExample_6_1()
    {
        var acks = new ErrorAcks();
        var b = new SessionRecord { SessionId = "B", State = SessionStatus.Error, Started = true, UpdatedAt = Now - 1000 };
        SessionView V(SessionRecord r) => new(r, r.State, r.State == SessionStatus.Error && acks.IsAcknowledged(r));

        Assert.Equal(IconKind.Waiting, Icon(View("A", SessionStatus.Waiting), V(b)));
        Assert.Equal(IconKind.Error, Icon(View("A", SessionStatus.Working), V(b)));
        acks.Acknowledge(b);
        Assert.Equal(IconKind.Working, Icon(View("A", SessionStatus.Working), V(b)));
        Assert.Equal(IconKind.Rest, Icon(View("A", SessionStatus.Done), V(b)));   // izmjena 16. 11.: bila je crvena

        var newError = b with { UpdatedAt = Now };
        Assert.False(acks.IsAcknowledged(newError));
        Assert.Equal(IconKind.Error, Icon(View("A", SessionStatus.Working), V(newError)));
    }

    [Fact]
    public void ErrorAcks_PruneRemovesDeadSessions()
    {
        var acks = new ErrorAcks();
        var r = new SessionRecord { SessionId = "B", UpdatedAt = 5 };
        acks.Acknowledge(r);
        acks.Prune(["A"]);
        Assert.False(acks.IsAcknowledged(r));
    }

    [Fact]
    public void Visible_HidesNotStarted() =>
        Assert.Equal(["b"], Aggregator.Visible([View("a", SessionStatus.Idle, started: false), View("b", SessionStatus.Working)], Now)
            .Select(v => v.Record.SessionId));

    [Fact]
    public void Visible_HidesOldRest() =>
        Assert.Equal(["w", "fresh"], Aggregator.Visible(
            [View("old", SessionStatus.Done, age: 16 * Min), View("fresh", SessionStatus.Idle, age: 14 * Min), View("w", SessionStatus.Working, age: 60 * Min)],
            Now).Select(v => v.Record.SessionId));

    [Fact]
    public void Visible_AllHidden_KeepsMostRecent() =>
        Assert.Equal(["newer"], Aggregator.Visible(
            [View("older", SessionStatus.Done, age: 40 * Min), View("newer", SessionStatus.Idle, age: 20 * Min), View("x", SessionStatus.Idle, started: false)],
            Now).Select(v => v.Record.SessionId));

    [Fact]
    public void Visible_Order() =>
        Assert.Equal(["wait", "err", "work2", "work1", "ack", "done"], Aggregator.Visible(
            [View("done", SessionStatus.Done), View("work1", SessionStatus.Working, age: 2000), View("ack", SessionStatus.Error, ack: true),
             View("wait", SessionStatus.Waiting), View("work2", SessionStatus.Working, age: 1000), View("err", SessionStatus.Error)],
            Now).Select(v => v.Record.SessionId));
}
