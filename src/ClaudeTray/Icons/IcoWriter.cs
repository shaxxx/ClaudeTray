using System.Drawing;
using System.Drawing.Imaging;

namespace ClaudeTray.Icons;

/// <summary>Format .ico: zaglavlje, imenik od 16 bajtova po slici, pa slike kao PNG (Windows Vista i noviji).</summary>
public static class IcoWriter
{
    public static void Write(Stream output, IReadOnlyList<(int Size, Bitmap Image)> images)
    {
        var pngs = images.Select(i =>
        {
            using var ms = new MemoryStream();
            i.Image.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }).ToList();

        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write((ushort)0);              // rezervirano
        w.Write((ushort)1);              // vrsta: ikona
        w.Write((ushort)images.Count);
        int offset = 6 + 16 * images.Count;
        for (int i = 0; i < images.Count; i++)
        {
            int size = images[i].Size;
            w.Write((byte)(size >= 256 ? 0 : size));   // širina (0 znači 256)
            w.Write((byte)(size >= 256 ? 0 : size));   // visina
            w.Write((byte)0);                          // broj boja u paleti
            w.Write((byte)0);                          // rezervirano
            w.Write((ushort)1);                        // ravnine
            w.Write((ushort)32);                       // bitova po pikselu
            w.Write(pngs[i].Length);
            w.Write(offset);
            offset += pngs[i].Length;
        }
        foreach (var png in pngs) w.Write(png);
    }
}
