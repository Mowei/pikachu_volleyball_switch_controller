using System.Runtime.InteropServices;

namespace SwitchMotionBridge;

// 管理系統匣通知圖示、狀態選單項，以及紅/黃/綠三種連線狀態圖示。
internal sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly Icon _redIcon;
    private readonly Icon _yellowIcon;
    private readonly Icon _greenIcon;

    public TrayIconManager(EventHandler onExitClick)
    {
        // 預先產生三種顏色圖示，對應三種連線狀態
        _redIcon = CreateColorIcon(Color.Red);
        _yellowIcon = CreateColorIcon(Color.Yellow);
        _greenIcon = CreateColorIcon(Color.LimeGreen);

        _statusMenuItem = new ToolStripMenuItem("狀態：未連線")
        {
            Enabled = false // 僅作為顯示用途，不可點擊
        };

        var motionKeyMappingMenuItem = new ToolStripMenuItem("體感轉按鍵")
        {
            CheckOnClick = true,
            Checked = AppConfig.MotionKeyMappingEnabled
        };
        motionKeyMappingMenuItem.CheckedChanged += (_, _) => AppConfig.MotionKeyMappingEnabled = motionKeyMappingMenuItem.Checked;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_statusMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(motionKeyMappingMenuItem);
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

    // 繪製一個 16x16 圓形圖標作為系統匣狀態指示
    private static Icon CreateColorIcon(Color color)
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
        return Icon.FromHandle(handle);
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    // 釋放通知圖示與所有自建的圖標資源
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _redIcon.Dispose();
        _yellowIcon.Dispose();
        _greenIcon.Dispose();
    }
}
