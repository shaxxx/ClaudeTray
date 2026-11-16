using ClaudeTray.Core;
using ClaudeTray.Model;

namespace ClaudeTray.Platform;

public sealed class SystemClock : IClock
{
    public long NowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

public sealed class SystemProcessProbe : IProcessProbe
{
    public bool IsAlive(int pid, long startedAtMs) => NativeProcess.IsAlive(pid, startedAtMs);
}
