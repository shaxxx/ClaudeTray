namespace ClaudeTray.Model;

public interface IClock
{
    long NowMs { get; }
}

public interface IProcessProbe
{
    bool IsAlive(int pid, long startedAtMs);
}

public interface ITranscriptProbe
{
    bool EndsWithInterrupt(string transcriptPath);
}
