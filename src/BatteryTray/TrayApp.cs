using System.Drawing;
using System.Windows.Forms;

namespace BatteryTray;

/// <summary>托盘应用主体:持有托盘图标与消息循环期间的全部状态。</summary>
internal sealed class TrayApp : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayApp()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("退出", null, (_, _) => Application.Exit());

        _notifyIcon = new NotifyIcon
        {
            Icon = BatteryIcon.Draw(level: 1.0f),
            Text = "BatteryTray — 骨架运行中",
            Visible = true,
            ContextMenuStrip = menu,
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

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

        // 电池主体(留 1px 边距),右侧小凸头
        float s = size / 32f; // 以 32px 为基准等比缩放
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
