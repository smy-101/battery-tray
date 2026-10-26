using System.Drawing;
using BatteryTray.Core;

namespace BatteryTray;

/// <summary>运行时绘制托盘电池图标(避免内嵌二进制资源)。配色与面板共用 TrayRules.BandOf。</summary>
internal static class BatteryIcon
{
    /// <summary>level ∈ [0,1];known=false 时画空心(无任何可读电量的设备)。</summary>
    public static Icon Draw(float level, bool known = true, int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var band = known
            ? TrayRules.BandOf((byte)Math.Clamp((int)Math.Ceiling(level * 100), 0, 100))
            : BatteryBand.None;
        Color fill = UiTheme.BandColor(band);

        // 电池主体(留 1px 边距),右侧小凸头;以 32px 基准等比缩放
        float s = size / 32f;
        var body = new RectangleF(2 * s, 9 * s, 24 * s, 14 * s);
        var cap = new RectangleF(27 * s, 13 * s, 3 * s, 6 * s);
        g.FillRectangle(Brushes.White, body);
        g.FillRectangle(Brushes.White, cap);
        var inset = new RectangleF(body.X + 2 * s, body.Y + 2 * s, body.Width - 4 * s, body.Height - 4 * s);
        if (known)
        {
            float w = inset.Width * Math.Clamp(level, 0f, 1f);
            g.FillRectangle(new SolidBrush(fill), inset.X, inset.Y, w, inset.Height);
        }
        else
        {
            using var pen = new Pen(Color.FromArgb(200, 200, 200), 1.5f * s);
            g.DrawRectangle(pen, Rectangle.Round(inset));
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}
