using BatteryTray.Core;

namespace BatteryTray;

/// <summary>左键托盘弹出的设备列表面板:无边框、置顶、点击别处关闭。</summary>
internal sealed class DevicePanel : Form
{
    private readonly ListView _list = new();

    public DevicePanel()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(400, 220);
        BackColor = Color.White;
        KeyPreview = true;

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _list.BorderStyle = BorderStyle.None;
        _list.Font = new Font("Microsoft YaHei UI", 9.5f);
        _list.Columns.Add("设备", 200);
        _list.Columns.Add("连接", 80);
        _list.Columns.Add("电量", 70);
        Controls.Add(_list);
    }

    public void ShowNear(Point screenPoint, IReadOnlyList<DeviceEntry> entries)
    {
        UpdateEntries(entries);
        var height = Math.Clamp(_list.Items.Count * 28 + 42, 120, 560);
        Height = height;
        var area = Screen.FromPoint(screenPoint).WorkingArea;
        int x = Math.Min(screenPoint.X - Width / 2, area.Right - Width - 8);
        int y = screenPoint.Y >= area.Top + area.Height / 2 ? screenPoint.Y - Height - 12 : screenPoint.Y + 12;
        Location = new Point(Math.Max(area.Left + 8, x), Math.Max(area.Top + 8, y));

        Show();
        Activate();
    }

    public void UpdateEntries(IReadOnlyList<DeviceEntry> entries)
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        if (entries.Count == 0)
        {
            _list.Items.Add(MakeItem("(未发现已配对蓝牙设备)", "", "", gray: true));
        }
        else
        {
            foreach (var d in entries)
            {
                var battery = d.BatteryPercent is byte b ? $"{b}%" : "—";
                _list.Items.Add(MakeItem(d.Name,
                    d.IsConnected ? "已连接" : "断连",
                    battery,
                    gray: !d.IsConnected));
            }
        }
        _list.EndUpdate();
    }

    private static ListViewItem MakeItem(string name, string transport, string battery, bool gray)
    {
        var item = new ListViewItem(name) { UseItemStyleForSubItems = true };
        item.SubItems.Add(transport);
        item.SubItems.Add(battery);
        if (gray)
        {
            var c = Color.FromArgb(150, 150, 150);
            item.ForeColor = c;
            item.SubItems[1].ForeColor = c;
            item.SubItems[2].ForeColor = c;
        }
        return item;
    }

    protected override void OnDeactivate(EventArgs e)
    {
        Hide();
        base.OnDeactivate(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Hide();
        base.OnKeyDown(e);
    }
}
