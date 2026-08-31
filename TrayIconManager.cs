using System.Runtime.InteropServices;

namespace SwitchMotionBridge;

// Owns the tray notify icon, status menu item, and the red/yellow/green connection icons.
internal sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusMenuItem;
    private readonly Icon _redIcon;
    private readonly Icon _yellowIcon;
    private readonly Icon _greenIcon;

    public TrayIconManager(EventHandler onExitClick)
    {
        _redIcon = CreateColorIcon(Color.Red);
        _yellowIcon = CreateColorIcon(Color.Yellow);
        _greenIcon = CreateColorIcon(Color.LimeGreen);

        _statusMenuItem = new ToolStripMenuItem("狀態：未連線")
        {
            Enabled = false
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_statusMenuItem);
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

    public void UpdateStatus(ConnectionState state, string text)
    {
        _notifyIcon.Icon = state switch
        {
            ConnectionState.Disconnected => _redIcon,
            ConnectionState.SingleConnected => _yellowIcon,
            ConnectionState.DualConnected => _greenIcon,
            _ => _redIcon
        };

        _notifyIcon.Text = text.Length <= 63 ? text : text.Substring(0, 63);
        _statusMenuItem.Text = $"狀態：{text}";
    }

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

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _redIcon.Dispose();
        _yellowIcon.Dispose();
        _greenIcon.Dispose();
    }
}
