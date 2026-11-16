using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClaudeTray.Icons;

/// <summary>
/// Generička zvjezdica (nije Claude logo), nacrtana kao u simulaciji (docs/mockup/claude-tray.html): 8 zraka
/// naizmjenične duljine R i 0,62·R, debljine 12 % veličine, sa zaobljenim krajevima (spec 6.2).
/// </summary>
public static class StarRenderer
{
    public const int Rays = 8;
    public const float StrokeRatio = 0.12f;
    public const float ShortRatio = 0.62f;
    public const float InnerRatio = 0.07f;
    public const float DotRadiusRatio = 0.21f;
    public const float DotRingRatio = 0.07f;

    /// <param name="longScale">Skraćenje dugih zraka pri pulsiranju (1 = puna duljina).</param>
    /// <param name="shortScale">Skraćenje kratkih zraka pri pulsiranju.</param>
    public static Bitmap Render(int sizePx, Color fill, float angleDeg, Color? dot, float longScale = 1f, float shortScale = 1f)
    {
        var bmp = new Bitmap(sizePx, sizePx, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float c = sizePx / 2f;
        float stroke = Math.Max(1.3f, sizePx * StrokeRatio);
        float longLen = c - stroke / 2 - 0.2f;
        float shortLen = longLen * ShortRatio;
        float inner = sizePx * InnerRatio;
        using (var pen = new Pen(fill, stroke) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            for (int i = 0; i < Rays; i++)
            {
                double a = i * Math.PI / 4 + angleDeg * Math.PI / 180;
                float len = i % 2 == 0 ? longLen * longScale : shortLen * shortScale;
                float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
                g.DrawLine(pen, c + cos * inner, c + sin * inner, c + cos * len, c + sin * len);
            }
        }

        if (dot is Color dotColor)
        {
            // Varijanta C: prozirni prsten odvaja točku od zvjezdice.
            float r = sizePx * DotRadiusRatio, ring = DotRingPx(sizePx);
            float center = sizePx - r - 0.4f;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var clear = new SolidBrush(Color.Transparent))
                g.FillEllipse(clear, center - r - ring, center - r - ring, 2 * (r + ring), 2 * (r + ring));
            g.CompositingMode = CompositingMode.SourceOver;
            using var brush = new SolidBrush(dotColor);
            g.FillEllipse(brush, center - r, center - r, 2 * r, 2 * r);
        }
        return bmp;
    }

    /// <summary>Širina prozirnog prstena oko točke: 7 % veličine, najmanje 1 px.</summary>
    public static float DotRingPx(int sizePx) => Math.Max(1f, sizePx * DotRingRatio);
}
