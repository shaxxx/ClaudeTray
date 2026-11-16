using System.Drawing;
using System.Drawing.Drawing2D;
using ClaudeTray.Core;
using ClaudeTray.Icons;
using ClaudeTray.Model;

namespace ClaudeTray;

/// <summary>
/// Izbornik na klik (spec 6.4): stavka u dva retka po sesiji (projekt i stanje, ispod naslov i oznaka terminala),
/// pa razdjelnik i Izlaz.
/// </summary>
public static class MenuBuilder
{
    public static readonly Font MenuFont = new("Segoe UI", 10f);
    const int DotSize = 14;
    static readonly Dictionary<Color, Bitmap> Dots = [];

    /// <param name="light">Tema trake; izbornik se otvara iznad nje (Theme.TaskbarIsLight).</param>
    public static ContextMenuStrip Build(IReadOnlyList<SessionView> visible, long nowMs, Action<SessionView> onClick, Action onQuit,
                                         bool light = false)
    {
        var palette = MenuPalette.For(light);
        var menu = new ContextMenuStrip
        {
            Font = MenuFont, ShowImageMargin = true, BackColor = palette.Background, ForeColor = palette.Text,
            Renderer = new SessionMenuRenderer(new MenuColorTable(palette)),
        };
        if (visible.Count == 0)
            menu.Items.Add(new ToolStripMenuItem(TooltipFormatter.Empty) { Enabled = false, ForeColor = palette.Text });

        foreach (var v in visible)
        {
            var item = new SessionMenuItem(TooltipFormatter.RowText(v, nowMs, withTitle: false), Subtitle(v), Dot(StateColor(v)))
            {
                ForeColor = palette.Text,
            };
            item.Click += (_, _) => onClick(v);
            menu.Items.Add(item);
            ReserveSubtitleWidth(item);
        }

        menu.Items.Add(new ToolStripSeparator());
        var quit = new ToolStripMenuItem("Izlaz")
        {
            Padding = new Padding(0, SessionMenuItem.VerticalPadding, 0, SessionMenuItem.VerticalPadding), ForeColor = palette.Text,
        };
        quit.Click += (_, _) => onQuit();
        menu.Items.Add(quit);

        // Prozor se stvara odmah: tek tada ga WinForms (PMv2) skalira na trenutni DPI. Inače NotifyIcon računa položaj
        // iznad ikone iz veličine na DPI-ju pokretanja, pa nakon promjene skaliranja izbornik promaši ikonu.
        _ = menu.Handle;
        return menu;
    }

    /// <summary>
    /// WinForms širinu izbornika računa iz glavnog retka i stupca prečaca, a ne iz GetPreferredSize stavke. Kad je
    /// podnaslov širi, razlika se rezervira u stupcu prečaca neprelomivim razmacima (renderer ih ne crta). Oba fonta
    /// skaliraju se zajedno, pa broj razmaka vrijedi na svakom DPI-ju.
    /// </summary>
    static void ReserveSubtitleWidth(SessionMenuItem item)
    {
        if (item.Subtitle.Length == 0) return;
        int missing = TextRenderer.MeasureText(item.Subtitle, item.SubtitleFont).Width - TextRenderer.MeasureText(item.Text, item.Font).Width;
        if (missing <= 0) return;
        float space = TextRenderer.MeasureText(new string(' ', 20), item.Font).Width / 20f;
        item.ShortcutKeyDisplayString = new string(' ', (int)Math.Ceiling(missing / space) + 1);
        item.ShowShortcutKeys = true;
    }

    static string Subtitle(SessionView v)
    {
        string sub = v.Record.Title;
        bool terminal = v.Record.Entrypoint.Length > 0 && v.Record.Entrypoint != "claude-vscode";
        if (terminal) sub = sub.Length > 0 ? sub + " · terminal" : "terminal";
        return sub;
    }

    static Color StateColor(SessionView v) => v.Effective switch
    {
        SessionStatus.Waiting => Palette.Yellow,
        SessionStatus.Error => Palette.Red,
        SessionStatus.Working => Palette.Orange,
        _ => Color.Gray,
    };

    static Bitmap Dot(Color c)
    {
        if (Dots.TryGetValue(c, out var bmp)) return bmp;
        bmp = new Bitmap(DotSize, DotSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(c);
            g.FillEllipse(b, 1, 1, DotSize - 2, DotSize - 2);
        }
        return Dots[c] = bmp;
    }
}
