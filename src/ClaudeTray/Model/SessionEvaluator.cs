using ClaudeTray.Core;

namespace ClaudeTray.Model;

public readonly record struct Evaluation(SessionStatus Effective, bool Remove);

/// <summary>Stvarno stanje sesije uz sigurnosne mreže 1–3 (spec 9).</summary>
public sealed class SessionEvaluator(IClock clock, IProcessProbe processes, ITranscriptProbe transcripts)
{
    public const long WorkingCapMs = 15 * 60_000;
    public const long WaitingCapMs = 2 * 3_600_000;
    public const long NoPidRemoveAfterMs = 15 * 60_000;

    public Evaluation Evaluate(SessionRecord r)
    {
        // Mreža 1: proces sesije više ne postoji.
        if (r.Pid > 0 && !processes.IsAlive(r.Pid, r.PidStartedAt)) return new Evaluation(r.State, Remove: true);

        long age = clock.NowMs - r.UpdatedAt;
        var effective = r.State;

        if (effective is SessionStatus.Working or SessionStatus.Waiting)
        {
            // Mreža 2: Esc ili odbijena dozvola ne okidaju nijedan hook.
            if (r.Transcript.Length > 0 && transcripts.EndsWithInterrupt(r.Transcript)) effective = SessionStatus.Idle;
            // Mreža 3: propušten događaj.
            else if (effective == SessionStatus.Working && age > WorkingCapMs) effective = SessionStatus.Idle;
            else if (effective == SessionStatus.Waiting && age > WaitingCapMs) effective = SessionStatus.Idle;
        }

        bool remove = r.Pid == 0 && (effective is SessionStatus.Idle or SessionStatus.Done) && age > NoPidRemoveAfterMs;
        return new Evaluation(effective, remove);
    }
}
