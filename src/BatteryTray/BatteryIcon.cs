using System.Drawing;

namespace BatteryTray;

/// <summary>运行时绘制托盘电池图标(避免内嵌二进制资源)。</summary>
internal static class BatteryIcon
{
    public static Icon Draw(float level, int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        Color fill = level switch
        {
            > 0.40f => Color.FromArgb(46, 204, 113),
            > 0.20f => Color.FromArgb(241, 196, 15),
            _       => Color.FromArgb(231, 76, 60),
        };

        // 电池主体(留 1px 边距),右侧小凸头;以 32px 基准等比缩放
        float s = size / 32f;
        var body = new RectangleF(2 * s, 9 * s, 24 * s, 14 * s);
        var cap = new RectangleF(27 * s, 13 * s, 3 * s, 6 * s);
        g.FillRectangle(Brushes.White, body);
        g.FillRectangle(Brushes.White, cap);
        var inset = new RectangleF(body.X + 2 * s, body.Y + 2 * s, body.Width - 4 * s, body.Height - 4 * s);
        float w = inset.Width * Math.Clamp(level, 0f, 1f);
        g.FillRectangle(new SolidBrush(fill), inset.X, inset.Y, w, inset.Height);

        return Icon.FromHandle(bmp.GetHicon());
    }
}
