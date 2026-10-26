using System.Drawing;
using System.Windows.Forms;
using BatteryTray.Core;
using BatteryTray.Scanning;

namespace BatteryTray;

/// <summary>托盘应用主体:扫描 → 快照仓 → 托盘图标/面板/tooltip。</summary>
internal sealed class TrayApp : IDisposable
{
    private const int PollMs = 60_000;
    private const int DebounceMs = 2_000;
    private const int TooltipMax = 63; // NotifyIcon.Text 上限

    private readonly NotifyIcon _notifyIcon;
    private readonly DevicePanel _panel = new();
    private readonly DeviceCatalog _catalog = new();
    private readonly BluetoothScanner _scanner = new();
    private readonly SynchronizationContext _ui;
    private readonly System.Windows.Forms.Timer _poll;
    private readonly System.Windows.Forms.Timer _debounce;
    private Icon? _icon;
    private bool _disposed;

    public TrayApp()
    {
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("需要在 UI 线程创建");

        var menu = new ContextMenuStrip();
        menu.Items.Add("立即刷新", null, async (_, _) => await ScanSafe());
        menu.Items.Add("退出", null, (_, _) => Application.Exit());

        _icon = BatteryIcon.Draw(1.0f);
        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "BatteryTray — 扫描中…",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                TogglePanel();
        };

        _poll = new System.Windows.Forms.Timer { Interval = PollMs };
        _poll.Tick += async (_, _) => await ScanSafe();
        _poll.Start();

        _debounce = new System.Windows.Forms.Timer { Interval = DebounceMs };
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await ScanSafe(); };

        _scanner.Changed += OnScannerChanged;
        _ = ScanSafe();
        _scanner.StartWatchers();
    }

    /// <summary>watcher 事件来自任意线程:Post 回 UI 线程重启去抖定时器。</summary>
    private void OnScannerChanged() => _ui.Post(_ =>
    {
        if (_disposed) return;
        _debounce.Stop();
        _debounce.Start();
    }, null);

    private async Task ScanSafe()
    {
        try
        {
            var raw = await _scanner.ScanAsync();
            var list = _catalog.ReplaceAll(raw);
            UpdateTooltip(list);
            if (_panel.Visible) _panel.UpdateEntries(list);
        }
        catch (Exception)
        {
            _notifyIcon.Text = "BatteryTray — 扫描失败";
        }
    }

    private void UpdateTooltip(IReadOnlyList<DeviceEntry> list)
    {
        string text = list.Count == 0
            ? "BatteryTray — 无设备"
            : string.Join(" | ", list.Select(d =>
                $"{d.Name} {(d.BatteryPercent is byte b ? b + "%" : "—")}"));
        if (text.Length > TooltipMax) text = text[..TooltipMax];
        _notifyIcon.Text = text;
    }

    private void TogglePanel()
    {
        if (_panel.Visible)
        {
            _panel.Hide();
        }
        else
        {
            _panel.ShowNear(Control.MousePosition, _catalog.Snapshot);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _poll.Dispose();
        _debounce.Dispose();
        _scanner.Dispose();
        _panel.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
        _icon = null;
    }
}
