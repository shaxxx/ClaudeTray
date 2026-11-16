using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClaudeTray.Icons;

/// <summary>--render-icons: sve sličice kao PNG i list za pregled na bojama trake (spec 10.5).</summary>
public static class IconExporter
{
    static readonly int[] Sizes = [16, 20, 24, 32];

    static IEnumerable<(string Name, Color Fill, float Angle, Color? Dot, float Long, float Short)> States(bool light)
    {
        yield return ("rest", light ? Palette.RestOnLight : Palette.RestOnDark, 0, null, 1f, 1f);
        yield return ("waiting", Palette.Orange, 0, Palette.Yellow, 1f, 1f);
        yield return ("error", Palette.Red, 0, null, 1f, 1f);
        for (int i = 0; i < IconSet.WorkingFrameCount; i++)
        {
            var (l, sh) = IconSet.PulseScales(i);
            yield return ($"working-{i:00}", Palette.Orange, i * IconSet.WorkingStepDeg, null, l, sh);
        }
    }

    public static void Export(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (bool light in new[] { true, false })
            foreach (int size in Sizes)
                foreach (var s in States(light))
                {
                    using var bmp = StarRenderer.Render(size, s.Fill, s.Angle, s.Dot, s.Long, s.Short);
                    bmp.Save(Path.Combine(dir, $"{(light ? "light" : "dark")}-{size}-{s.Name}.png"), ImageFormat.Png);
                }
    }

    /// <summary>
    /// pregled.png: za svaku boju trake jedan red po veličini; stanja mirovanje, čeka te, greška i radi (prva sličica),
    /// u pravoj veličini i uvećana 6 puta bez zaglađivanja.
    /// </summary>
    public static void ExportPreview(string dir)
    {
        Directory.CreateDirectory(dir);
        const int zoom = 6, pad = 8;
        int cell = 32 * zoom + pad;
        int width = pad + 4 * (32 + pad + cell);
        int bandHeight = Sizes.Length * (cell + pad) + pad;
        using var sheet = new Bitmap(width, 2 * bandHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(sheet);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        int y0 = 0;
        foreach (bool light in new[] { true, false })
        {
            using (var bg = new SolidBrush(light ? Palette.TaskbarLight : Palette.TaskbarDark))
                g.FillRectangle(bg, 0, y0, width, bandHeight);
            int y = y0 + pad;
            foreach (int size in Sizes)
            {
                int x = pad;
                foreach (var s in States(light).Take(4))
                {
                    using var bmp = StarRenderer.Render(size, s.Fill, s.Angle, s.Dot, s.Long, s.Short);
                    g.DrawImageUnscaled(bmp, x, y);
                    g.DrawImage(bmp, new Rectangle(x + 32 + pad, y, size * zoom, size * zoom));
                    x += 32 + pad + cell;
                }
                y += cell + pad;
            }
            y0 += bandHeight;
        }
        sheet.Save(Path.Combine(dir, "pregled.png"), ImageFormat.Png);
    }
}
