namespace SwitchMotionBridge.Tray;

// 提供 1P/2P 各自設定體感上下限門檻的操作流程，取代直接編輯 JSON 檔案。
internal sealed class SensorSettingsForm : Form
{
    private readonly ControllerWorker _controllerWorker;
    private readonly MotionSettingsData _data;
    private readonly PlayerThresholdControls _leftControls;
    private readonly PlayerThresholdControls _rightControls;

    public SensorSettingsForm(ControllerWorker controllerWorker)
    {
        _controllerWorker = controllerWorker;
        _data = MotionSettings.Load();

        Text = "感測器參數設定";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 430);

        var tabs = new TabControl { Dock = DockStyle.Top, Height = 340 };
        var tab1P = new TabPage("1P（左搖桿）");
        var tab2P = new TabPage("2P（右搖桿）");
        tabs.TabPages.Add(tab1P);
        tabs.TabPages.Add(tab2P);

        _leftControls = new PlayerThresholdControls(tab1P, _data, _data.LeftPlayer);
        _rightControls = new PlayerThresholdControls(tab2P, _data, _data.RightPlayer);

        var saveButton = new Button { Text = "儲存", DialogResult = DialogResult.None, Location = new Point(190, 360), Size = new Size(80, 30) };
        var cancelButton = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(280, 360), Size = new Size(80, 30) };
        saveButton.Click += SaveButton_Click;

        Controls.Add(tabs);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (!ValidateOrdering(_leftControls, "1P") || !ValidateOrdering(_rightControls, "2P"))
        {
            var proceed = MessageBox.Show(
                "偵測到門檻數值可能不合理（放開門檻應介於觸發門檻與 0 之間），仍要儲存嗎？",
                "數值檢查",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (proceed != DialogResult.Yes)
            {
                return;
            }
        }

        _data.LeftPlayer = _leftControls.ToThresholdSetOrNull();
        _data.RightPlayer = _rightControls.ToThresholdSetOrNull();

        MotionSettings.Save(_data);
        AppConfig.ReloadMotionSettings();
        _controllerWorker.ReloadMotionThresholds();
        NotificationService.Notify("感測器參數設定已儲存並套用");

        DialogResult = DialogResult.OK;
        Close();
    }

    // 確認放開門檻落在合理範圍內（觸發門檻與 0 之間），僅作提醒用途，不阻擋儲存
    private static bool ValidateOrdering(PlayerThresholdControls controls, string label)
    {
        var move = controls.MoveThreshold.Value;
        var moveRelease = controls.MoveReleaseThreshold.Value;
        var down = controls.DownThreshold.Value;
        var downRelease = controls.DownReleaseThreshold.Value;

        var moveOk = moveRelease > 0 && moveRelease < move;
        var downOk = downRelease < 0 && downRelease > down;
        return moveOk && downOk;
    }

    // 單一玩家（1P/2P）的體感上下限門檻輸入區，含「使用自訂數值」切換與 6 組數值輸入
    private sealed class PlayerThresholdControls
    {
        private readonly CheckBox _overrideCheckBox;

        public NumericUpDown MoveThreshold { get; }
        public NumericUpDown MoveReleaseThreshold { get; }
        public NumericUpDown JumpThreshold { get; }
        public NumericUpDown DownThreshold { get; }
        public NumericUpDown DownReleaseThreshold { get; }
        public NumericUpDown HitThreshold { get; }

        public PlayerThresholdControls(TabPage page, MotionSettingsData defaults, MotionThresholdSet? overrideSet)
        {
            _overrideCheckBox = new CheckBox
            {
                Text = "使用專屬體感參數（取消勾選則沿用全域預設值）",
                AutoSize = true,
                Location = new Point(10, 10),
                Checked = overrideSet is not null
            };

            var layout = new TableLayoutPanel
            {
                Location = new Point(10, 40),
                Size = new Size(340, 260),
                ColumnCount = 2,
                RowCount = 6
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

            MoveThreshold = AddRow(layout, "左右移動觸發門檻(g，上限)", overrideSet?.MoveThreshold ?? defaults.MoveThreshold, 0.1m, 5m, 0.05m);
            MoveReleaseThreshold = AddRow(layout, "左右移動放開門檻(g，下限)", overrideSet?.MoveReleaseThreshold ?? defaults.MoveReleaseThreshold, 0.05m, 5m, 0.02m);
            JumpThreshold = AddRow(layout, "跳躍觸發門檻(g，上限)", overrideSet?.JumpThreshold ?? defaults.JumpThreshold, 0.1m, 5m, 0.05m);
            DownThreshold = AddRow(layout, "下蹲觸發門檻(g，下限)", overrideSet?.DownThreshold ?? defaults.DownThreshold, -5m, -0.1m, 0.05m);
            DownReleaseThreshold = AddRow(layout, "下蹲放開門檻(g，上限)", overrideSet?.DownReleaseThreshold ?? defaults.DownReleaseThreshold, -5m, -0.05m, 0.02m);
            HitThreshold = AddRow(layout, "揮擊角速度門檻(°/s，上限)", overrideSet?.HitThreshold ?? defaults.HitThreshold, 100m, 2048m, 10m);

            page.Controls.Add(_overrideCheckBox);
            page.Controls.Add(layout);

            SetEnabled(_overrideCheckBox.Checked);
            _overrideCheckBox.CheckedChanged += (_, _) => SetEnabled(_overrideCheckBox.Checked);
        }

        private void SetEnabled(bool enabled)
        {
            MoveThreshold.Enabled = enabled;
            MoveReleaseThreshold.Enabled = enabled;
            JumpThreshold.Enabled = enabled;
            DownThreshold.Enabled = enabled;
            DownReleaseThreshold.Enabled = enabled;
            HitThreshold.Enabled = enabled;
        }

        // 未勾選「使用專屬體感參數」時回傳 null，儲存時即代表沿用全域預設值
        public MotionThresholdSet? ToThresholdSetOrNull()
        {
            if (!_overrideCheckBox.Checked)
            {
                return null;
            }

            return new MotionThresholdSet
            {
                MoveThreshold = (double)MoveThreshold.Value,
                MoveReleaseThreshold = (double)MoveReleaseThreshold.Value,
                JumpThreshold = (double)JumpThreshold.Value,
                DownThreshold = (double)DownThreshold.Value,
                DownReleaseThreshold = (double)DownReleaseThreshold.Value,
                HitThreshold = (double)HitThreshold.Value
            };
        }

        private static NumericUpDown AddRow(TableLayoutPanel layout, string labelText, double value, decimal min, decimal max, decimal increment)
        {
            var rowIndex = layout.RowStyles.Count;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var label = new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft };
            var input = new NumericUpDown
            {
                DecimalPlaces = 2,
                Minimum = min,
                Maximum = max,
                Increment = increment,
                Value = Math.Clamp((decimal)value, min, max),
                Width = 100,
                Anchor = AnchorStyles.Left
            };

            layout.Controls.Add(label, 0, rowIndex);
            layout.Controls.Add(input, 1, rowIndex);
            return input;
        }
    }
}
