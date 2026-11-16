namespace ClaudeTray.Model;

/// <summary>
/// Kad se tray sam gasi (spec 8, kao original): prvih 5 s nakon pokretanja nikad, a nakon toga kad
/// 3 s zaredom nema nijedne žive sesije.
/// </summary>
public sealed class LifecyclePolicy(long launchedAtMs)
{
    public const long LaunchGraceMs = 5000;
    public const long EmptyQuitDelayMs = 3000;

    long? _emptySince;

    public bool ShouldExit(long nowMs, int liveSessions)
    {
        if (nowMs - launchedAtMs < LaunchGraceMs) return false;
        if (liveSessions > 0)
        {
            _emptySince = null;
            return false;
        }
        _emptySince ??= nowMs;
        return nowMs - _emptySince.Value >= EmptyQuitDelayMs;
    }
}
