using System.Drawing;

namespace ClaudeTray;

/// <summary>
/// Stavka izbornika u dva retka: gore projekt i stanje, ispod sivi naslov. Cijela stavka je jedan cilj za hover i klik.
/// Tekst crta <see cref="SessionMenuRenderer"/>.
/// </summary>
public sealed class SessionMenuItem : ToolStripMenuItem
{
    public const int VerticalPadding = 4;
    public const int LineGap = 1;
    /// <summary>Podnaslov je 90 % fonta stavke (9 pt uz 10 pt izbornika).</summary>
    public const float SubtitleScale = 0.9f;

    Font? _subtitleFont, _subtitleFontBase;

    public string Subtitle { get; }

    /// <summary>
    /// Izveden iz fonta stavke, koji WinForms skalira kad se promijeni DPI (Per-Monitor V2). Zaseban statički font
    /// ostao bi na DPI-ju pokretanja: nakon prelaska sa 175 % na 100 % podnaslov je bio veći od glavnog retka.
    /// </summary>
    public Font SubtitleFont
    {
        get
        {
            var font = Font;
            if (_subtitleFont is null || !ReferenceEquals(_subtitleFontBase, font))
            {
                _subtitleFont?.Dispose();
                _subtitleFont = new Font(font.FontFamily, font.Size * SubtitleScale, font.Style, font.Unit);
                _subtitleFontBase = font;
            }
            return _subtitleFont;
        }
    }

    public SessionMenuItem(string text, string subtitle, Image? image) : base(text, image)
    {
        Subtitle = subtitle;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _subtitleFont?.Dispose();
        base.Dispose(disposing);
    }

    public override Size GetPreferredSize(Size constrainingSize)
    {
        var size = base.GetPreferredSize(constrainingSize);
        int height = size.Height + 2 * VerticalPadding;
        int width = size.Width;
        if (Subtitle.Length > 0)
        {
            var main = TextRenderer.MeasureText(Text, Font);
            var sub = TextRenderer.MeasureText(Subtitle, SubtitleFont);
            height += LineGap + sub.Height;
            width = Math.Max(width, width - main.Width + sub.Width);
        }
        return new Size(width, height);
    }
}

/// <summary>Izgled izbornika iz <see cref="MenuColorTable"/>, uz dva retka teksta za <see cref="SessionMenuItem"/>.</summary>
public sealed class SessionMenuRenderer(ProfessionalColorTable colors) : ToolStripProfessionalRenderer(colors)
{
    const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item is not SessionMenuItem item)
        {
            base.OnRenderItemText(e);
            return;
        }
        if (e.Text != item.Text) return;   // stupac prečaca: samo rezerva širine za podnaslov (MenuBuilder)
        var main = TextRenderer.MeasureText(e.Graphics, item.Text, e.TextFont, Size.Empty, Flags);
        var sub = item.Subtitle.Length > 0 ? TextRenderer.MeasureText(e.Graphics, item.Subtitle, item.SubtitleFont, Size.Empty, Flags) : Size.Empty;
        int total = main.Height + (sub.Height > 0 ? SessionMenuItem.LineGap + sub.Height : 0);
        int top = (item.Height - total) / 2;
        int left = e.TextRectangle.Left;

        // Kao standardni renderer za označenu stavku: boja teksta je ForeColor stavke, a ne sistemska boja
        // označenog teksta (u tamnom načinu bi podnaslov ispao taman na tamnom, u svijetlom bijel na svijetloplavom).
        Color color = item.ForeColor;
        TextRenderer.DrawText(e.Graphics, item.Text, e.TextFont, new Point(left, top), color, Flags);
        if (sub.Height > 0)
            TextRenderer.DrawText(e.Graphics, item.Subtitle, item.SubtitleFont,
                new Point(left, top + main.Height + SessionMenuItem.LineGap), Dimmed(color), Flags);
    }

    /// <summary>Boja teksta napola prema sivoj, za podnaslov.</summary>
    static Color Dimmed(Color c) => Color.FromArgb((c.R + 128) / 2, (c.G + 128) / 2, (c.B + 128) / 2);
}
