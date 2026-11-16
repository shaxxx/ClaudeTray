using ClaudeTray.Core;
using ClaudeTray.Model;

namespace ClaudeTray.Tests;

public class SessionEvaluatorTests
{
    const long Now = 100_000_000;
    const long Min = 60_000;

    sealed class FakeClock(long now) : IClock { public long NowMs => now; }
    sealed class FakeProcesses(bool alive) : IProcessProbe { public bool IsAlive(int pid, long startedAtMs) => alive; }
    sealed class FakeTranscripts(bool interrupted) : ITranscriptProbe { public bool EndsWithInterrupt(string path) => interrupted; }
    sealed class ThrowingTranscripts : ITranscriptProbe
    {
        public bool EndsWithInterrupt(string path) => throw new InvalidOperationException("ne smije se pozvati");
    }

    static SessionEvaluator Eval(bool alive = true, ITranscriptProbe? transcripts = null) =>
        new(new FakeClock(Now), new FakeProcesses(alive), transcripts ?? new FakeTranscripts(false));

    static SessionRecord Rec(SessionStatus state, long ageMs, int pid = 42) => new()
    {
        SessionId = "s1", State = state, Pid = pid, PidStartedAt = 7, UpdatedAt = Now - ageMs,
        Transcript = @"C:\t.jsonl", Started = true,
    };

    [Fact]
    public void DeadProcess_Removes() => Assert.True(Eval(alive: false).Evaluate(Rec(SessionStatus.Working, 0)).Remove);

    [Fact]
    public void AliveWorking_StaysWorking() =>
        Assert.Equal(new Evaluation(SessionStatus.Working, false), Eval().Evaluate(Rec(SessionStatus.Working, Min)));

    [Fact]
    public void Working_14m59s_Working() =>
        Assert.Equal(SessionStatus.Working, Eval().Evaluate(Rec(SessionStatus.Working, 15 * Min - 1000)).Effective);

    [Fact]
    public void Working_15m01s_Idle() =>
        Assert.Equal(SessionStatus.Idle, Eval().Evaluate(Rec(SessionStatus.Working, 15 * Min + 1000)).Effective);

    [Fact]
    public void Waiting_1h59m_Waiting() =>
        Assert.Equal(SessionStatus.Waiting, Eval().Evaluate(Rec(SessionStatus.Waiting, 119 * Min)).Effective);

    [Fact]
    public void Waiting_2h01m_Idle() =>
        Assert.Equal(SessionStatus.Idle, Eval().Evaluate(Rec(SessionStatus.Waiting, 121 * Min)).Effective);

    [Theory]
    [InlineData(SessionStatus.Working)]
    [InlineData(SessionStatus.Waiting)]
    public void Interrupt_BecomesIdle(SessionStatus state) =>
        Assert.Equal(SessionStatus.Idle, Eval(transcripts: new FakeTranscripts(true)).Evaluate(Rec(state, Min)).Effective);

    [Theory]
    [InlineData(SessionStatus.Done)]
    [InlineData(SessionStatus.Error)]
    [InlineData(SessionStatus.Idle)]
    public void Interrupt_NotConsultedForRestingStates(SessionStatus state) =>
        Assert.Equal(state, Eval(transcripts: new ThrowingTranscripts()).Evaluate(Rec(state, Min)).Effective);

    [Fact]
    public void Interrupt_NotConsultedWithoutTranscript() =>
        Assert.Equal(SessionStatus.Working,
            Eval(transcripts: new ThrowingTranscripts()).Evaluate(Rec(SessionStatus.Working, Min) with { Transcript = "" }).Effective);

    [Fact]
    public void Error_NeverAgesOut()
    {
        var e = Eval().Evaluate(Rec(SessionStatus.Error, 600 * Min, pid: 0));
        Assert.Equal(new Evaluation(SessionStatus.Error, false), e);
    }

    [Fact]
    public void NoPid_DoneOld_Removes() => Assert.True(Eval().Evaluate(Rec(SessionStatus.Done, 16 * Min, pid: 0)).Remove);

    [Fact]
    public void NoPid_DoneFresh_Stays() => Assert.False(Eval().Evaluate(Rec(SessionStatus.Done, 14 * Min, pid: 0)).Remove);

    [Fact]
    public void NoPid_WorkingStale_Removes() => Assert.True(Eval().Evaluate(Rec(SessionStatus.Working, 16 * Min, pid: 0)).Remove);

    [Fact]
    public void WithPid_OldDone_Stays() => Assert.False(Eval().Evaluate(Rec(SessionStatus.Done, 600 * Min)).Remove);
}
