using ClaudeTray.Core;
using ClaudeTray.Model;

namespace ClaudeTray;

/// <summary>
/// Prati mapu stanja (spec 9, mreža 4): FileSystemWatcher za trenutnu reakciju i puno čitanje svakih 500 ms.
/// Sve se obrađuje na UI niti, pa ErrorAcks i TranscriptProbe ne trebaju zaključavanje.
/// </summary>
public sealed class SessionMonitor : IDisposable
{
    const int IntervalMs = 500;

    readonly string _stateDir;
    readonly SessionEvaluator _evaluator;
    readonly ErrorAcks _acks;
    readonly SynchronizationContext _ui;
    FileSystemWatcher? _watcher;
    System.Threading.Timer? _timer;
    int _queued;

    /// <summary>(sve žive sesije, broj živih uključujući one koje još nisu ništa radile)</summary>
    public event Action<IReadOnlyList<SessionView>, int>? Changed;

    public SessionMonitor(string stateDir, SessionEvaluator evaluator, ErrorAcks acks, SynchronizationContext ui)
    {
        _stateDir = stateDir;
        _evaluator = evaluator;
        _acks = acks;
        _ui = ui;
    }

    public void Start()
    {
        Directory.CreateDirectory(_stateDir);
        _watcher = new FileSystemWatcher(_stateDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += (_, _) => Queue();
        _watcher.Changed += (_, _) => Queue();
        _watcher.Renamed += (_, _) => Queue();
        _watcher.Deleted += (_, _) => Queue();
        _watcher.Error += (_, _) => Queue();
        _watcher.EnableRaisingEvents = true;
        _timer = new System.Threading.Timer(_ => Queue(), null, 0, IntervalMs);
    }

    /// <summary>Odmah ponovno pročitaj stanje (npr. nakon potvrde greške).</summary>
    public void RefreshNow() => Refresh();

    void Queue()
    {
        if (Interlocked.Exchange(ref _queued, 1) == 1) return;   // već čeka na UI niti
        _ui.Post(_ =>
        {
            Interlocked.Exchange(ref _queued, 0);
            Refresh();
        }, null);
    }

    void Refresh()
    {
        var views = new List<SessionView>();
        foreach (var record in StateStore.ReadAll(_stateDir))
        {
            var e = _evaluator.Evaluate(record);
            if (e.Remove)
            {
                StateStore.Delete(_stateDir, record.SessionId);
                continue;
            }
            views.Add(new SessionView(record, e.Effective, e.Effective == SessionStatus.Error && _acks.IsAcknowledged(record)));
        }
        _acks.Prune(views.Select(v => v.Record.SessionId));
        Changed?.Invoke(views, views.Count);
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _watcher?.Dispose();
    }
}
