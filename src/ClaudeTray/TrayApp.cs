using System.Reflection;
using ClaudeTray.Core;
using ClaudeTray.Icons;
using ClaudeTray.Model;
using ClaudeTray.Platform;

namespace ClaudeTray;

/// <summary>Ikona u traci sustava: zajedničko stanje, animacija, tooltip, izbornik i životni ciklus (spec 6, 8).</summary>
sealed class TrayApp : ApplicationContext
{
    const int AnimationIntervalMs = 83;

    readonly IClock _clock = new SystemClock();
    readonly ErrorAcks _acks = new();
    readonly Focuser _focuser = new();
    readonly LifecyclePolicy _life;
    readonly NotifyIcon _notify;
    readonly SessionMonitor _monitor;
    readonly System.Windows.Forms.Timer _animation = new() { Interval = AnimationIntervalMs };
    readonly System.Windows.Forms.Timer _second = new() { Interval = 1000 };
    readonly SystemEventsWindow _systemEvents;
    readonly EventWaitHandle _exitEvent;
    readonly RegisteredWaitHandle _exitWait;

    IconSet _icons;
    int _iconSize;
    bool _lightTaskbar;
    IReadOnlyList<SessionView> _visible = [];
    IconKind _kind = IconKind.Rest;
    int _frame;
    bool _exiting;

    public TrayApp()
    {
        var ui = new WindowsFormsSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(ui);
        _life = new LifecyclePolicy(_clock.NowMs);

        _iconSize = Theme.SmallIconSize();
        _lightTaskbar = Theme.TaskbarIsLight();
        _icons = IconSet.Create(_iconSize, _lightTaskbar);
        _notify = new NotifyIcon { Icon = _icons.Rest, Text = TooltipFormatter.Empty, Visible = true };
        _notify.MouseDown += (_, _) => RebuildMenu();
        _notify.MouseUp += OnMouseUp;

        _animation.Tick += (_, _) =>
        {
            _frame = (_frame + 1) % _icons.Working.Count;
            _notify.Icon = _icons.Working[_frame];
        };
        _second.Tick += (_, _) =>
        {
            CheckTheme();
            UpdateTooltip();
        };
        _second.Start();
        _systemEvents = new SystemEventsWindow(CheckTheme);

        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, TrayPaths.ExitEventName);
        _exitWait = ThreadPool.RegisterWaitForSingleObject(_exitEvent, (_, _) => ui.Post(_ => ExitThread(), null), null, Timeout.Infinite, true);

        var evaluator = new SessionEvaluator(_clock, new SystemProcessProbe(), new TranscriptProbe());
        _monitor = new SessionMonitor(TrayPaths.StateDir, evaluator, _acks, ui);
        _monitor.Changed += OnChanged;
        _monitor.Start();
    }

    void OnChanged(IReadOnlyList<SessionView> all, int liveSessions)
    {
        if (_exiting) return;
        long now = _clock.NowMs;
        _visible = Aggregator.Visible(all, now);
        _kind = Aggregator.Icon(_visible);
        ApplyIcon();
        UpdateTooltip();
        if (_life.ShouldExit(now, liveSessions)) ExitThread();
    }

    void ApplyIcon()
    {
        bool animate = _kind == IconKind.Working && Theme.AnimationsEnabled();
        if (_animation.Enabled != animate) _animation.Enabled = animate;
        if (animate && _animation.Enabled && _notify.Icon is not null && _icons.Working.Contains(_notify.Icon)) return; // tajmer vrti
        _notify.Icon = _kind switch
        {
            IconKind.Working => _icons.Working[animate ? _frame % _icons.Working.Count : 0],
            IconKind.Waiting => _icons.Waiting,
            IconKind.Error => _icons.Error,
            _ => _icons.Rest,
        };
    }

    void UpdateTooltip()
    {
        if (_exiting) return;
        _notify.Text = TooltipFormatter.Format(_visible, _clock.NowMs);
    }

    /// <summary>Boja trake ili DPI promijenjeni: nove sličice.</summary>
    void CheckTheme()
    {
        if (_exiting) return;
        int size = Theme.SmallIconSize();
        bool light = Theme.TaskbarIsLight();
        if (size == _iconSize && light == _lightTaskbar) return;
        var old = _icons;
        (_iconSize, _lightTaskbar) = (size, light);
        _icons = IconSet.Create(size, light);
        _notify.Icon = _icons.Rest;   // makni stare HICON-e iz NotifyIcona prije Dispose
        ApplyIcon();
        old.Dispose();
    }

    void RebuildMenu()
    {
        var old = _notify.ContextMenuStrip;
        _notify.ContextMenuStrip = MenuBuilder.Build(_visible, _clock.NowMs, OnSessionClick, Quit, Theme.TaskbarIsLight());
        old?.Dispose();
    }

    void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;   // desni klik NotifyIcon otvara sam
        var show = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        if (show is not null) show.Invoke(_notify, null);
        else _notify.ContextMenuStrip?.Show(Cursor.Position);
    }

    void OnSessionClick(SessionView v)
    {
        if (v.Effective == SessionStatus.Error)
        {
            _acks.Acknowledge(v.Record);
            _monitor.RefreshNow();
        }
        _focuser.Focus(v);
    }

    void Quit()
    {
        try
        {
            Directory.CreateDirectory(TrayPaths.Home);
            File.WriteAllText(TrayPaths.QuitIntentFile, DateTimeOffset.Now.ToString("O"));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            TrayLog.Write("quit-intent nije zapisan: " + e.Message);
        }
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (!_exiting)
        {
            _exiting = true;
            _animation.Stop();
            _second.Stop();
            _monitor.Dispose();
            _exitWait.Unregister(null);
            _exitEvent.Dispose();
            _systemEvents.DestroyHandle();
            _notify.Visible = false;
            _notify.ContextMenuStrip?.Dispose();
            _notify.Dispose();
            _icons.Dispose();
        }
        base.ExitThreadCore();
    }

    /// <summary>Skriveni prozor najviše razine: prima WM_SETTINGCHANGE, WM_DISPLAYCHANGE i WM_DPICHANGED.</summary>
    sealed class SystemEventsWindow : NativeWindow
    {
        const int WM_SETTINGCHANGE = 0x001A, WM_DISPLAYCHANGE = 0x007E, WM_DPICHANGED = 0x02E0, WS_EX_TOOLWINDOW = 0x80;
        readonly Action _changed;

        public SystemEventsWindow(Action changed)
        {
            _changed = changed;
            CreateHandle(new CreateParams { Caption = "ClaudeTray.SystemEvents", ExStyle = WS_EX_TOOLWINDOW });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg is WM_SETTINGCHANGE or WM_DISPLAYCHANGE or WM_DPICHANGED) _changed();
            base.WndProc(ref m);
        }
    }
}
