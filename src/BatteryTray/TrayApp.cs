using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BatteryTray.Core;
using BatteryTray.Scanning;

namespace BatteryTray;

/// <summary>托盘应用主体:扫描 → 快照仓 → 托盘图标/面板/tooltip。</summary>
internal sealed class TrayApp : IDisposable
{
    private const int DebounceMs = 2_000;
    private const int TooltipMax = 63; // NotifyIcon.Text 上限

    private readonly NotifyIcon _notifyIcon;
    private readonly DevicePanel _panel = new();
    private readonly DeviceCatalog _catalog = new();
    private readonly BluetoothScanner _scanner = new();
    private readonly SynchronizationContext _ui;
    private AppSettings _settings = SettingsStore.Load();
    private readonly System.Windows.Forms.Timer _poll;
    private readonly System.Windows.Forms.Timer _debounce;
    private Icon? _icon;
    private nint _lastHicon;
    private List<DeviceEntry> _lastEntries = new();
    private bool _disposed;

    public TrayApp()
    {
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("需要在 UI 线程创建");

        var menu = new ContextMenuStrip();
        menu.Items.Add("立即刷新", null, async (_, _) => await ScanSafe());
        menu.Items.Add("设置…", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Application.Exit());
        menu.Items.Add(new ToolStripSeparator());
        var build = Environment.ProcessPath is { } p && File.Exists(p)
            ? File.GetLastWriteTime(p).ToString("HH:mm:ss") : "?";
        var versionItem = new ToolStripMenuItem($"v{GetType().Assembly.GetName().Version?.ToString(3)} · 构建 {build}")
        { Enabled = false };
        menu.Items.Add(versionItem);

        _icon = BatteryIcon.Draw(1.0f, known: false);
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

        _poll = new System.Windows.Forms.Timer { Interval = _settings.PollSeconds * 1000 };
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

            // 低电量提醒:首读即低或跌破阈值时弹一次;持续低位不重复
            var newlyLow = TrayRules.NewlyLow(_lastEntries, list, _settings.LowThreshold);
            _lastEntries = list.ToList();
            if (newlyLow.Count > 0 && _settings.ToastEnabled)
                _notifyIcon.ShowBalloonTip(5000, "BatteryTray 低电量",
                    string.Join("\n", newlyLow.Select(d => $"{d.Name}:{d.BatteryPercent}%")),
                    ToolTipIcon.Warning);

            UpdateIcon(list);
            UpdateTooltip(list);
            if (_panel.Visible) _panel.UpdateEntries(list);
        }
        catch (Exception)
        {
            _notifyIcon.Text = "BatteryTray — 扫描失败";
        }
    }

    private void UpdateIcon(IReadOnlyList<DeviceEntry> list)
    {
        var min = TrayRules.MinBatteryPercent(list);
        var icon = BatteryIcon.Draw((min ?? 100) / 100f, known: min is not null);
        var old = _notifyIcon.Icon;
        _notifyIcon.Icon = icon;
        old?.Dispose();
        if (_lastHicon != 0) DestroyIcon(_lastHicon);
        _lastHicon = icon.Handle;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    private void ShowSettings()
    {
        using var dialog = new SettingsDialog(_settings);
        if (dialog.ShowDialog() == DialogResult.OK && dialog.Result is { } saved)
        {
            _settings = saved;
            SettingsStore.Save(saved);
            _poll.Interval = saved.PollSeconds * 1000;
            _ = ScanSafe(); // 阈值变化可能立即触发提醒
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
        if (_lastHicon != 0) DestroyIcon(_lastHicon);
        _lastHicon = 0;
        _icon?.Dispose();
        _icon = null;
    }
}
