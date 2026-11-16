using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClaudeTray.Icons;

/// <summary>
/// Ikona aplikacije (ClaudeTray.exe), varijanta C koju je korisnik izabrao 16. 11.: svijetla zvjezdica na narančastoj
/// zaobljenoj pločici (docs/mockup/app-ikona-varijante.png). Ikone u traci su i dalje <see cref="StarRenderer"/>.
/// </summary>
public static class AppIconRenderer
{
    public static readonly Color StarColor = Color.FromArgb(0xFA, 0xF9, 0xF5);
    public static IReadOnlyList<int> Sizes { get; } = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    const float CornerRatio = 0.22f, StarRatio = 0.68f, InsetRatio = 0.04f, CenterRatio = 0.1f;

    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float inset = size >= 32 ? size * InsetRatio : 0f, w = size - 2 * inset, d = 2 * size * CornerRatio;
        using (var tile = new GraphicsPath())
        {
            tile.AddArc(inset, inset, d, d, 180, 90);
            tile.AddArc(inset + w - d, inset, d, d, 270, 90);
            tile.AddArc(inset + w - d, inset + w - d, d, d, 0, 90);
            tile.AddArc(inset, inset + w - d, d, d, 90, 90);
            tile.CloseFigure();
            using var orange = new SolidBrush(Palette.Orange);
            g.FillPath(orange, tile);
        }

        int inner = Math.Max(10, (int)Math.Round(size * StarRatio));
        float offset = (size - inner) / 2f;
        using (var star = StarRenderer.Render(inner, StarColor, 0, null))
            g.DrawImage(star, offset, offset, inner, inner);

        // Zrake počinju malo od središta; u velikim veličinama to bi bila vidljiva rupica.
        float r = inner * CenterRatio, c = size / 2f;
        using (var fill = new SolidBrush(StarColor))
            g.FillEllipse(fill, c - r, c - r, 2 * r, 2 * r);
        return bmp;
    }

    /// <summary>Zapisuje viševeličinsku .ico datoteku (<c>--render-app-icon</c>).</summary>
    public static void WriteIco(string path)
    {
        var images = Sizes.Select(s => (s, Render(s))).ToList();
        try
        {
            using var file = File.Create(path);
            IcoWriter.Write(file, images);
        }
        finally
        {
            foreach (var (_, bmp) in images) bmp.Dispose();
        }
    }
}
