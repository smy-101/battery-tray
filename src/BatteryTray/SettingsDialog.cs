using BatteryTray.Core;

namespace BatteryTray;

/// <summary>设置对话框:轮询间隔 / 提醒阈值 / 提醒开关 / 开机自启。</summary>
internal sealed class SettingsDialog : Form
{
    private readonly NumericUpDown _poll = new();
    private readonly NumericUpDown _threshold = new();
    private readonly CheckBox _toast = new() { Text = "低电量时弹出通知", AutoSize = true };
    private readonly CheckBox _autoStart = new() { Text = "开机自启动(当前用户)", AutoSize = true };

    /// <summary>用户点保存后生效的新设置;取消时为 null。</summary>
    public AppSettings? Result { get; private set; }

    public SettingsDialog(AppSettings current)
    {
        Text = "BatteryTray 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        ClientSize = new Size(360, 240);

        var pollLabel = new Label { Text = "轮询间隔(秒)", AutoSize = true, Left = 20, Top = 24 };
        _poll.SetBounds(220, 20, 110, 28);
        _poll.Minimum = SettingsRules.MinPollSeconds;
        _poll.Maximum = SettingsRules.MaxPollSeconds;
        _poll.Value = current.PollSeconds;

        var thresholdLabel = new Label { Text = "低电量提醒阈值(%)", AutoSize = true, Left = 20, Top = 60 };
        _threshold.SetBounds(220, 56, 110, 28);
        _threshold.Minimum = SettingsRules.MinThreshold;
        _threshold.Maximum = SettingsRules.MaxThreshold;
        _threshold.Value = current.LowThreshold;

        _toast.SetBounds(20, 100, 300, 26);
        _toast.Checked = current.ToastEnabled;
        _autoStart.SetBounds(20, 132, 300, 26);
        _autoStart.Checked = current.StartWithWindows;

        var ok = new Button { Text = "保存", DialogResult = DialogResult.None, Left = 168, Top = 180, Width = 85 };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Left = 260, Top = 180, Width = 85 };
        ok.Click += (_, _) =>
        {
            Result = new AppSettings
            {
                PollSeconds = SettingsRules.ClampPollSeconds((int)_poll.Value),
                LowThreshold = SettingsRules.ClampThreshold((int)_threshold.Value),
                ToastEnabled = _toast.Checked,
                StartWithWindows = _autoStart.Checked,
            };
            DialogResult = DialogResult.OK;
        };

        Controls.AddRange(new Control[] { pollLabel, _poll, thresholdLabel, _threshold, _toast, _autoStart, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
