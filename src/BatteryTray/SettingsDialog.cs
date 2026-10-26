using BatteryTray.Core;

namespace BatteryTray;

/// <summary>
/// 设置对话框:轮询间隔 / 提醒阈值 / 提醒开关 / 开机自启。
/// TableLayoutPanel 布局(抗 DPI/文字加宽),跟随系统深浅色。
/// </summary>
internal sealed class SettingsDialog : Form
{
    private readonly NumericUpDown _poll = new();
    private readonly NumericUpDown _threshold = new();
    private readonly CheckBox _toast = new() { Text = "低电量时弹出通知", AutoSize = true };
    private readonly CheckBox _autoStart = new() { Text = "开机自启动(当前用户)", AutoSize = true };
    private readonly Button _ok = new() { Text = "保存", AutoSize = true, MinimumSize = new Size(88, 34) };
    private readonly Button _cancel = new()
    {
        Text = "取消",
        AutoSize = true,
        MinimumSize = new Size(88, 34),
        DialogResult = DialogResult.Cancel,
    };

    /// <summary>用户点保存后生效的新设置;取消时为 null。</summary>
    public AppSettings? Result { get; private set; }

    public SettingsDialog(AppSettings current)
    {
        Text = "BatteryTray 设置";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Microsoft YaHei UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _poll.Minimum = SettingsRules.MinPollSeconds;
        _poll.Maximum = SettingsRules.MaxPollSeconds;
        _poll.Value = SettingsRules.ClampPollSeconds(current.PollSeconds);
        _poll.BorderStyle = BorderStyle.FixedSingle;
        _threshold.Minimum = SettingsRules.MinThreshold;
        _threshold.Maximum = SettingsRules.MaxThreshold;
        _threshold.Value = SettingsRules.ClampThreshold(current.LowThreshold);
        _threshold.BorderStyle = BorderStyle.FixedSingle;
        _toast.Checked = current.ToastEnabled;
        _autoStart.Checked = current.StartWithWindows;

        _ok.Click += (_, _) =>
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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(24, 20, 24, 20),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        int row = 0;
        root.Controls.Add(FieldLabel("轮询间隔(10–600 秒)"), 0, row);
        root.Controls.Add(_poll, 1, row);
        row++;
        root.Controls.Add(FieldLabel("低电量提醒阈值(5–50%)"), 0, row);
        root.Controls.Add(_threshold, 1, row);
        row++;
        _toast.Margin = new Padding(0, 14, 0, 0);
        root.SetColumnSpan(_toast, 2);
        root.Controls.Add(_toast, 0, row);
        row++;
        _autoStart.Margin = new Padding(0, 8, 0, 0);
        root.SetColumnSpan(_autoStart, 2);
        root.Controls.Add(_autoStart, 0, row);
        row++;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 18, 0, 0),
        };
        buttons.Controls.Add(_ok);
        _cancel.Margin = new Padding(8, 0, 0, 0);
        buttons.Controls.Add(_cancel);
        root.SetColumnSpan(buttons, 2);
        root.Controls.Add(buttons, 0, row);

        Controls.Add(root);
        AcceptButton = _ok;
        CancelButton = _cancel;
        ApplyTheme();
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, 12, 0),
    };

    private void ApplyTheme()
    {
        var t = UiTheme.Current();
        BackColor = t.Back;
        if (UiTheme.IsSystemDark()) UiTheme.TryDarkTitleBar(this);

        foreach (var box in new Control[] { _poll, _threshold })
        {
            box.BackColor = t.ButtonBack;
            box.ForeColor = t.Text;
        }

        foreach (var box in new Control[] { _toast, _autoStart })
        {
            box.BackColor = t.Back;
            box.ForeColor = t.Text;
        }

        // 字段标签跟随主题(按钮单独处理)
        foreach (var label in FindLabels(this))
        {
            label.ForeColor = t.Text;
            label.BackColor = t.Back;
        }

        // 主按钮:品牌绿;次按钮:跟随主题的中性扁平样式
        var accent = UiTheme.BandColor(BatteryBand.Good);
        _ok.FlatStyle = FlatStyle.Flat;
        _ok.BackColor = accent;
        _ok.ForeColor = Color.White;
        _ok.FlatAppearance.BorderColor = accent;
        _ok.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 210, 120);
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.BackColor = t.ButtonBack;
        _cancel.ForeColor = t.ButtonText;
        _cancel.FlatAppearance.BorderColor = t.ButtonBorder;
        _cancel.FlatAppearance.MouseOverBackColor = t.Hover;
    }

    private static IEnumerable<Label> FindLabels(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Label label) yield return label;
            foreach (var nested in FindLabels(c)) yield return nested;
        }
    }
}
