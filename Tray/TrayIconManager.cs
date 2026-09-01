using SwitchMotionBridge.Enums;
using System.Runtime.InteropServices;

namespace SwitchMotionBridge.Tray;

// 管理系統匣通知圖示、狀態選單項，以及紅/黃/綠三種連線狀態圖示。
internal sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly Icon _redIcon;
    private readonly Icon _yellowIcon;
    private readonly Icon _greenIcon;
    private readonly nint _redIconHandle;
    private readonly nint _yellowIconHandle;
    private readonly nint _greenIconHandle;
    private readonly Dictionary<PlayerMode, ToolStripMenuItem> _playerModeMenuItems = new();

    public TrayIconManager(EventHandler onExitClick, EventHandler onCalibrateClick, EventHandler onEditKeyBindingsClick, EventHandler onEditMotionSettingsClick, EventHandler onSensorSettingsClick, PlayerMode initialPlayerMode, Action<PlayerMode> onPlayerModeChanged)
    {
        // 預先產生三種顏色圖示，對應三種連線狀態
        (_redIcon, _redIconHandle) = CreateColorIcon(Color.Red);
        (_yellowIcon, _yellowIconHandle) = CreateColorIcon(Color.Yellow);
        (_greenIcon, _greenIconHandle) = CreateColorIcon(Color.LimeGreen);

        _statusMenuItem = new ToolStripMenuItem("狀態：未連線")
        {
            Enabled = false // 僅作為顯示用途，不可點擊
        };

        var motionKeyMappingMenuItem = new ToolStripMenuItem("啟用體感")
        {
            CheckOnClick = true,
            Checked = AppConfig.MotionKeyMappingEnabled
        };
        motionKeyMappingMenuItem.CheckedChanged += (_, _) => AppConfig.MotionKeyMappingEnabled = motionKeyMappingMenuItem.Checked;

        var buttonKeyMappingMenuItem = new ToolStripMenuItem("啟用按鍵")
        {
            CheckOnClick = true,
            Checked = AppConfig.ButtonKeyMappingEnabled
        };
        buttonKeyMappingMenuItem.CheckedChanged += (_, _) => AppConfig.ButtonKeyMappingEnabled = buttonKeyMappingMenuItem.Checked;

        var verboseLoggingMenuItem = new ToolStripMenuItem("詳細記錄")
        {
            CheckOnClick = true,
            Checked = AppConfig.VerboseLogging
        };
        verboseLoggingMenuItem.CheckedChanged += (_, _) => AppConfig.VerboseLogging = verboseLoggingMenuItem.Checked;

        var calibrateMenuItem = new ToolStripMenuItem("開始校正");
        calibrateMenuItem.Click += onCalibrateClick;

        var autoCalibrationMenuItem = new ToolStripMenuItem("啟用自動校正")
        {
            CheckOnClick = true,
            Checked = AppConfig.AutoCalibrationEnabled
        };
        autoCalibrationMenuItem.CheckedChanged += (_, _) => AppConfig.AutoCalibrationEnabled = autoCalibrationMenuItem.Checked;

        var editKeyBindingsMenuItem = new ToolStripMenuItem("編輯按鍵設定");
        editKeyBindingsMenuItem.Click += onEditKeyBindingsClick;

        var editMotionSettingsMenuItem = new ToolStripMenuItem("編輯體感參數設定");
        editMotionSettingsMenuItem.Click += onEditMotionSettingsClick;

        var sensorSettingsMenuItem = new ToolStripMenuItem("感測器參數設定");
        sensorSettingsMenuItem.Click += onSensorSettingsClick;

        var playerModeMenuItem = new ToolStripMenuItem("玩家模式");
        AddPlayerModeItem(playerModeMenuItem, PlayerMode.SinglePlayer, "單人模式", initialPlayerMode, onPlayerModeChanged);
        AddPlayerModeItem(playerModeMenuItem, PlayerMode.DualPlayer, "雙人模式（1P+2P）", initialPlayerMode, onPlayerModeChanged);
        AddPlayerModeItem(playerModeMenuItem, PlayerMode.LeftPlayer, "1P 專屬（左搖桿）", initialPlayerMode, onPlayerModeChanged);
        AddPlayerModeItem(playerModeMenuItem, PlayerMode.RightPlayer, "2P 專屬（右搖桿）", initialPlayerMode, onPlayerModeChanged);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(playerModeMenuItem);
        contextMenu.Items.Add(motionKeyMappingMenuItem);
        contextMenu.Items.Add(buttonKeyMappingMenuItem);
        contextMenu.Items.Add(verboseLoggingMenuItem);
        contextMenu.Items.Add(calibrateMenuItem);
        contextMenu.Items.Add(autoCalibrationMenuItem);
        contextMenu.Items.Add(editKeyBindingsMenuItem);
        contextMenu.Items.Add(editMotionSettingsMenuItem);
        contextMenu.Items.Add(sensorSettingsMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, onExitClick);

        _notifyIcon = new NotifyIcon
        {
            Icon = _redIcon,
            Text = "Switch Motion Bridge - 未連線",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    // 根據連線狀態更新圖示顏色、鼠標悬停文字與選單狀態文字
    public void UpdateStatus(ConnectionState state, string text)
    {
        _notifyIcon.Icon = state switch
        {
            ConnectionState.Disconnected => _redIcon,
            ConnectionState.SingleConnected => _yellowIcon,
            ConnectionState.DualConnected => _greenIcon,
            _ => _redIcon
        };

        _notifyIcon.Text = text.Length <= 63 ? text : text.Substring(0, 63); // NotifyIcon.Text 限制最多 63 字元
        _statusMenuItem.Text = $"狀態：{text}";
    }

    // 建立單一玩家模式選單項，並以手動互斥的方式模擬單選（ToolStripMenuItem 本身無原生單選群組）
    private void AddPlayerModeItem(ToolStripMenuItem parent, PlayerMode mode, string text, PlayerMode initialPlayerMode, Action<PlayerMode> onPlayerModeChanged)
    {
        var item = new ToolStripMenuItem(text)
        {
            CheckOnClick = false,
            Checked = mode == initialPlayerMode
        };
        item.Click += (_, _) =>
        {
            if (item.Checked)
            {
                return;
            }

            foreach (var other in _playerModeMenuItems.Values)
            {
                other.Checked = false;
            }

            item.Checked = true;
            onPlayerModeChanged(mode);
        };

        _playerModeMenuItems[mode] = item;
        parent.DropDownItems.Add(item);
    }

    // 繪製一個 16x16 圓形圖標作為系統匣狀態指示，同時回傳原始 HICON 句柄以便日後釋放
    private static (Icon icon, nint handle) CreateColorIcon(Color color)
    {
        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(brush, 1, 1, 14, 14);
        }

        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    // Icon.FromHandle 不擁有句柄所有權，需自行以 DestroyIcon 釋放 GetHicon() 產生的 HICON，否則會導致句柄洩漏
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    // 釋放通知圖示與所有自建的圖標資源
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _redIcon.Dispose();
        _yellowIcon.Dispose();
        _greenIcon.Dispose();
        DestroyIcon(_redIconHandle);
        DestroyIcon(_yellowIconHandle);
        DestroyIcon(_greenIconHandle);
    }
}
