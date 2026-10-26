using System.Drawing.Drawing2D;
using BatteryTray.Core;

namespace BatteryTray;

/// <summary>
/// 左键托盘弹出的设备面板:无边框圆角(Win11)+ 投影、跟随系统深浅色、
/// 自绘行(状态点 + 连接类型 + 按档位着色的电量条),点击别处关闭。
/// </summary>
internal sealed class DevicePanel : Form
{
    private const int ColTransport = 160;
    // 电量列宽需容下最宽文本"100%"(~40px)+ 电量条 56 + 左右留白,
    // 尺寸不足时右对齐百分比会被裁掉左侧(只看到"00%")
    private const int ColBattery = 144;

    /// <summary>双缓冲 ListView,自绘不闪烁。</summary>
    private sealed class PanelListView : ListView
    {
        public PanelListView() => DoubleBuffered = true;
    }

    private readonly PanelListView _list = new();
    private readonly Panel _header = new();
    private UiTheme.Palette _theme = UiTheme.Current();
    private DateTime? _lastScan;

    /// <summary>预览/截图模式:停用“失焦自动关闭”。</summary>
    public bool PreviewMode { get; set; }

    private int RowHeight => Font.Height + 13;

    public DevicePanel()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        Font = new Font("Microsoft YaHei UI", 9.5f);

        _header.Dock = DockStyle.Top;
        _header.Height = Font.Height + 20;
        _header.Paint += DrawHeader;

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HeaderStyle = ColumnHeaderStyle.None;
        _list.BorderStyle = BorderStyle.None;
        _list.OwnerDraw = true;
        _list.AccessibleName = "设备电量列表";
        _list.DrawColumnHeader += delegate { };
        _list.DrawItem += DrawRow;      // Details 视图 owner-draw 必须处理 DrawItem,否则整行不绘制
        _list.DrawSubItem += DrawCell;
        _list.Columns.Add("设备");
        _list.Columns.Add("连接");
        _list.Columns.Add("电量");

        // 用哑元 ImageList 抬高行高,给电量条留出空间
        using var spacer = new Bitmap(1, RowHeight);
        using (var g = Graphics.FromImage(spacer)) g.Clear(Color.Transparent);
        var rows = new ImageList { ImageSize = new Size(1, RowHeight) };
        rows.Images.Add(spacer);
        _ = rows.Handle; // 立即落成原生句柄(拷贝像素),此后释放 spacer 才安全
        _list.SmallImageList = rows;

        Controls.Add(_list);
        Controls.Add(_header);
    }

    public void ShowNear(Point screenPoint, IReadOnlyList<DeviceEntry> entries)
    {
        ApplyTheme();
        UpdateEntries(entries);
        if (_lastScan is null) MarkScanned();

        // 宽度随最长设备名自适应(含省略号余量),高度按行数
        int nameWidth = 0;
        foreach (var d in entries)
            nameWidth = Math.Max(nameWidth, TextRenderer.MeasureText(d.Name, Font).Width);
        Width = Math.Clamp(40 + nameWidth + 30 + ColTransport + ColBattery, 400, 560);
        int rows = Math.Min(Math.Max(_list.Items.Count, 1), 9);
        Height = _header.Height + rows * RowHeight + 8;

        var area = Screen.FromPoint(screenPoint).WorkingArea;
        int x = Math.Min(screenPoint.X - Width / 2, area.Right - Width - 8);
        int y = screenPoint.Y >= area.Top + area.Height / 2 ? screenPoint.Y - Height - 12 : screenPoint.Y + 12;
        Location = new Point(Math.Max(area.Left + 8, x), Math.Max(area.Top + 8, y));

        Show();
        Activate();
    }

    /// <summary>扫描完成后由 TrayApp 调用:刷新面板数据与“上次扫描”时间。</summary>
    public void UpdateEntries(IReadOnlyList<DeviceEntry> entries)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        if (entries.Count == 0)
        {
            _list.Items.Add(new ListViewItem("未发现已配对设备") { Tag = null });
        }
        else
        {
            foreach (var d in entries)
            {
                var item = new ListViewItem(d.Name) { Tag = d };
                item.SubItems.Add(""); // 连接列(自绘占位,无 subitem 则不触发 DrawSubItem)
                item.SubItems.Add(""); // 电量列
                _list.Items.Add(item);
            }
        }
        _list.EndUpdate();
    }

    /// <summary>记录一次成功扫描的时间,显示在头部右侧。</summary>
    public void MarkScanned()
    {
        _lastScan = DateTime.Now;
        _header.Invalidate();
    }

    private void ApplyTheme()
    {
        _theme = UiTheme.Current();
        BackColor = _theme.Back;
        _header.BackColor = _theme.Back;
        _list.BackColor = _theme.Back;
        _list.ForeColor = _theme.Text;
        UiTheme.TryRoundCorners(this);
        Invalidate(true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutColumns();
    }

    private void LayoutColumns()
    {
        // 预留滚动条宽度,避免出现滚动条时列头跳动
        int listWidth = _list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
        _list.Columns[1].Width = ColTransport;
        _list.Columns[2].Width = ColBattery;
        _list.Columns[0].Width = Math.Max(80, listWidth - ColTransport - ColBattery);
    }

    private void DrawHeader(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = new Rectangle(0, 0, _header.Width, _header.Height);
        using var titleFont = new Font(Font, FontStyle.Bold);
        TextRenderer.DrawText(g, "BatteryTray", titleFont,
            Rectangle.Inflate(rect, -14, 0), _theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        string scan = _lastScan is { } t ? $"上次扫描 {t:HH:mm:ss}" : "扫描中…";
        TextRenderer.DrawText(g, scan, Font,
            Rectangle.Inflate(rect, -14, 0), _theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
        using var pen = new Pen(_theme.Border);
        g.DrawLine(pen, 0, _header.Height - 1, _header.Width, _header.Height - 1);
    }

    /// <summary>行背景(选中态);单元格内容在 DrawSubItem 画。</summary>
    private void DrawRow(object? sender, DrawListViewItemEventArgs e)
    {
        if (e.Item is { Selected: true })
        {
            using var brush = new SolidBrush(_theme.Hover);
            e.Graphics.FillRectangle(brush, e.Bounds);
        }
    }

    private void DrawCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = e.Bounds;
        var entry = e.Item?.Tag as DeviceEntry;
        bool gray = entry is not { IsConnected: true };

        if (e.ColumnIndex == 0)
        {
            DrawCellText(g, entry?.Name ?? e.Item?.Text ?? "", gray ? _theme.Muted : _theme.Text, bounds, 14, 6);
        }
        else if (e.ColumnIndex == 1)
        {
            DrawTransport(g, entry, bounds);
        }
        else
        {
            DrawBattery(g, entry, bounds);
        }
    }

    /// <summary>连接列:状态点 + “蓝牙 · 已连接”。</summary>
    private void DrawTransport(Graphics g, DeviceEntry? entry, Rectangle bounds)
    {
        if (entry is null)
        {
            DrawCellText(g, "—", _theme.Muted, bounds, 14, 6);
            return;
        }

        int dot = Math.Max(8, Font.Height / 2);
        int cy = bounds.Top + bounds.Height / 2;
        using var brush = new SolidBrush(
            entry.IsConnected ? UiTheme.BandColor(BatteryBand.Good) : _theme.Muted);
        g.FillEllipse(brush, bounds.X + 14, cy - dot / 2, dot, dot);

        string status = $"{entry.TransportLabel} · {(entry.IsConnected ? "已连接" : "断连")}";
        var rect = Rectangle.FromLTRB(bounds.Left + 14 + dot + 8, bounds.Top, bounds.Right - 6, bounds.Bottom);
        TextRenderer.DrawText(g, status, Font, rect,
            entry.IsConnected ? _theme.Text : _theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    /// <summary>电量列:胶囊电量条(按档位配色)+ 百分比;读不到显示 —。</summary>
    private void DrawBattery(Graphics g, DeviceEntry? entry, Rectangle bounds)
    {
        if (entry?.BatteryPercent is not byte pct)
        {
            TextRenderer.DrawText(g, "—", Font,
                Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right - 14, bounds.Bottom),
                _theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
            return;
        }

        bool gray = !entry.IsConnected;
        int barHeight = Math.Max(8, Font.Height / 2);
        int barWidth = Math.Min(56, bounds.Width - 46 - 14 - 8);
        int cy = bounds.Top + bounds.Height / 2;
        var track = new Rectangle(bounds.Left + 14, cy - barHeight / 2, barWidth, barHeight);

        FillPill(g, track, _theme.Track);
        if (!gray)
        {
            // 哪怕 1% 也要看得见红色:最小填充为一个圆角宽
            int fillWidth = (int)Math.Round(track.Width * pct / 100.0);
            if (pct > 0) fillWidth = Math.Max(fillWidth, barHeight);
            if (fillWidth <= track.Width)
                FillPill(g, new Rectangle(track.X, track.Y, fillWidth, track.Height),
                    UiTheme.BandColor(TrayRules.BandOf(pct)));
        }

        TextRenderer.DrawText(g, $"{pct}%", Font,
            Rectangle.FromLTRB(track.Right + 8, bounds.Top, bounds.Right - 12, bounds.Bottom),
            gray ? _theme.Muted : _theme.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPrefix);
    }

    private void DrawCellText(Graphics g, string text, Color color, Rectangle bounds, int left, int right)
    {
        var rect = Rectangle.FromLTRB(bounds.Left + left, bounds.Top, bounds.Right - right, bounds.Bottom);
        TextRenderer.DrawText(g, text, Font, rect, color,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private static void FillPill(Graphics g, Rectangle r, Color color)
    {
        int d = Math.Min(r.Height, r.Width);
        using var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x0002_0000; // CS_DROPSHADOW:无边框弹层的轻投影
            return cp;
        }
    }

    protected override void OnDeactivate(EventArgs e)
    {
        if (!PreviewMode) Hide();
        base.OnDeactivate(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Hide();
        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _list.SmallImageList?.Dispose();
        base.Dispose(disposing);
    }
}
