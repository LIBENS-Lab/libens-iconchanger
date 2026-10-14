using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

var destination = Path.GetFullPath(args[0]);
Directory.CreateDirectory(destination);
Color C(string hex) => ColorTranslator.FromHtml(hex);
Bitmap Render(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.ScaleTransform(size / 256f, size / 256f);
    using var document = new GraphicsPath();
    document.AddLine(48, 16, 132, 16);
    document.AddLine(132, 16, 184, 68);
    document.AddLine(184, 68, 184, 196);
    document.AddBezier(184, 196, 184, 209, 173, 220, 160, 220);
    document.AddLine(160, 220, 48, 220);
    document.AddBezier(48, 220, 35, 220, 24, 209, 24, 196);
    document.AddLine(24, 196, 24, 40);
    document.AddBezier(24, 40, 24, 27, 35, 16, 48, 16);
    document.CloseFigure();
    using var paper = new SolidBrush(C("#DCE7E1"));
    g.FillPath(paper, document);
    using var fold = new SolidBrush(C("#829B8D"));
    g.FillPolygon(fold, [new(132,16), new(132,68), new(184,68)]);
    using var tile = new GraphicsPath();
    tile.AddArc(96,100,64,64,180,90);
    tile.AddArc(176,100,64,64,270,90);
    tile.AddArc(176,180,64,64,0,90);
    tile.AddArc(96,180,64,64,90,90);
    tile.CloseFigure();
    using var green = new SolidBrush(C("#54D994"));
    g.FillPath(green, tile);
    using var ink = new SolidBrush(C("#10271B"));
    g.FillPolygon(ink, [new(122,136),new(179,136),new(179,122),new(210,148),new(179,174),new(179,160),new(122,160)]);
    g.FillPolygon(ink, [new(214,204),new(157,204),new(157,218),new(126,192),new(157,166),new(157,180),new(214,180)]);
    return bmp;
}
int[] sizes = [16,20,24,32,48,64,128,256];
using (var png = Render(512)) png.Save(Path.Combine(destination, "app.png"), ImageFormat.Png);
var frames = new List<byte[]>();
foreach (var size in sizes)
{
    using var bitmap = Render(size);
    using var stream = new MemoryStream();
    bitmap.Save(stream, ImageFormat.Png);
    frames.Add(stream.ToArray());
}
using (var output = new BinaryWriter(File.Create(Path.Combine(destination, "app.ico")), Encoding.UTF8))
{
    output.Write((ushort)0); output.Write((ushort)1); output.Write((ushort)sizes.Length);
    int offset = 6 + sizes.Length * 16;
    for (int i=0; i<sizes.Length; i++)
    {
        output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
        output.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
        output.Write((byte)0); output.Write((byte)0);
        output.Write((ushort)1); output.Write((ushort)32);
        output.Write((uint)frames[i].Length); output.Write((uint)offset);
        offset += frames[i].Length;
    }
    foreach (var frame in frames) output.Write(frame);
}
// Decode each embedded image; the ICO directory uses 0 to represent 256.
for (int i = 0; i < sizes.Length; i++)
{
    using var frame = new MemoryStream(frames[i]);
    using var decoded = Image.FromStream(frame);
    if (decoded.Width != sizes[i] || decoded.Height != sizes[i]) throw new Exception($"ICO frame mismatch: {sizes[i]}");
}
using (var sheet = new Bitmap(760, 340))
{
    using var g = Graphics.FromImage(sheet);
    g.Clear(C("#F1F3F5"));
    using var dark = new SolidBrush(C("#0E1014"));
    g.FillRectangle(dark, 0, 170, 760, 170);
    int x = 24;
    foreach (var size in sizes)
    {
        using var icon = Render(size);
        int display = Math.Min(size,128);
        g.DrawImage(icon, x, 20, display, display);
        g.DrawImage(icon, x, 190, display, display);
        x += Math.Max(display, 50) + 16;
    }
    sheet.Save(Path.Combine(Path.GetFullPath(args[1]), "icon-check.png"), ImageFormat.Png);
}
Console.WriteLine("Created SVG-matched PNG / ICO; all 8 ICO sizes decoded successfully.");
