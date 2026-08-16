using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using HidSharp;

internal static class Program
{
    private static readonly ushort NintendoVendorId = 0x057E;
    private static readonly ushort[] SupportedProductIds =
    {
        0x2006, // Joy-Con (L)
        0x2007, // Joy-Con (R)
        0x2009, // Pro Controller
        0x200E, // Common Bluetooth HID variant seen on Switch controllers
        0x2017, // Charging Grip / paired combo
        0x2019  // Common Bluetooth HID variant seen on Switch controllers
    };
    private static readonly TimeSpan MotionCooldown = TimeSpan.FromMilliseconds(250);
    private static readonly double MoveThreshold = 0.6;
    private static readonly double JumpThreshold = 1.7;
    private static readonly double DownThreshold = -1.0;
    private static readonly double HitThreshold = 1800.0;

    private static DateTime _lastJump = DateTime.MinValue;
    private static DateTime _lastHit = DateTime.MinValue;
    private static bool _leftHeld;
    private static bool _rightHeld;
    private static bool _downHeld;
    private static readonly bool DebugMode = IsDebugMode(Environment.GetCommandLineArgs());
    private static readonly PlayerMode Mode = DetermineMode();
    private static NotifyIcon? _notifyIcon;
    private static ToolStripMenuItem? _statusMenuItem;
    private static Icon? _redIcon;
    private static Icon? _yellowIcon;
    private static Icon? _greenIcon;
    private static CancellationTokenSource? _cts;
    private static Thread? _workerThread;

    private enum PlayerMode
    {
        LeftPlayer,
        SinglePlayer
    }

    private static PlayerMode DetermineMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1].Equals("single", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerMode.SinglePlayer;
        }

        return PlayerMode.LeftPlayer;
    }

    internal static bool IsDebugMode(string[] args)
    {
        return args.Any(arg => arg.Equals("debug", StringComparison.OrdinalIgnoreCase));
    }

    private static void LogDebug(string message)
    {
        Console.WriteLine($"[DEBUG] {message}");
        if (!DebugMode)
        {
            return;
        }

        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwitchMotionBridge");
        Directory.CreateDirectory(logDirectory);
        var logPath = Path.Combine(logDirectory, "debug.log");
        File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
    }

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new TrayApplicationContext());
    }

    private sealed class TrayApplicationContext : ApplicationContext
    {
        public TrayApplicationContext()
        {
            LoadNotifyIcons();

            _notifyIcon = new NotifyIcon
            {
                Icon = _redIcon ?? SystemIcons.Application,
                Text = "Switch Motion Bridge - 未連線",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            _statusMenuItem = new ToolStripMenuItem("狀態：未連線")
            {
                Enabled = false
            };
            _notifyIcon.ContextMenuStrip.Items.Add(_statusMenuItem);
            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit_Click);

            StartWorker();
        }

        private void Exit_Click(object? sender, EventArgs e)
        {
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            StopWorker();
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            base.ExitThreadCore();
        }
    }

    private static void StartWorker()
    {
        _cts = new CancellationTokenSource();
        _workerThread = new Thread(() => WorkerLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "SwitchMotionBridgeWorker"
        };
        _workerThread.Start();
    }

    private static void StopWorker()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();
        _workerThread?.Join(1000);
        _cts.Dispose();
        _cts = null;
    }

    private static void WorkerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var devices = GetConnectedSwitchControllers();
            if (devices.Length == 0)
            {
                UpdateTrayStatus(ConnectionState.Disconnected, "左/右搖桿皆未連線");
                Thread.Sleep(5000);
                continue;
            }

            var (state, statusText) = GetConnectionStatus(devices);
            UpdateTrayStatus(state, statusText);

            try
            {
                var streams = new List<(ushort productId, HidStream stream, byte[] buffer)>();
                foreach (var (productId, device) in devices)
                {
                    var stream = device.Open();
                    stream.ReadTimeout = 2000;
                    EnableImu(stream, device);
                    streams.Add((productId, stream, new byte[device.GetMaxInputReportLength()]));
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    var anyData = false;
                    foreach (var (productId, stream, buffer) in streams)
                    {
                        if (!TryReadReport(stream, buffer, out var bytesRead))
                        {
                            continue;
                        }

                        anyData = true;
                        ProcessReport(buffer, bytesRead, productId);
                    }

                    if (!anyData)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                UpdateTrayStatus(ConnectionState.Disconnected, "存取控制器遭到取消");
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                UpdateTrayStatus(ConnectionState.Disconnected, ex.Message);
                Thread.Sleep(5000);
            }
        }
    }

    private static HidDevice? FindSwitchController()
    {
        var devices = GetConnectedSwitchControllers();
        return devices.Length > 0 ? devices[0].device : null;
    }

    private static (ushort productId, HidDevice device)[] GetConnectedSwitchControllers()
    {
        var list = DeviceList.Local;
        var connected = new List<(ushort productId, HidDevice device)>();

        foreach (var productId in SupportedProductIds)
        {
            foreach (var device in list.GetHidDevices(NintendoVendorId, productId))
            {
                connected.Add((productId, device));
            }
        }

        return connected.ToArray();
    }

    private static (ConnectionState state, string statusText) GetConnectionStatus((ushort productId, HidDevice device)[] devices)
    {
        return GetConnectionStatusForProductIds(devices.Select(x => x.productId));
    }

    internal static (ConnectionState state, string statusText) GetConnectionStatusForProductIds(IEnumerable<ushort> productIds)
    {
        var leftConnected = false;
        var rightConnected = false;
        var proConnected = false;

        foreach (var productId in productIds)
        {
            if (productId == 0x2006)
            {
                leftConnected = true;
            }
            else if (productId == 0x2007)
            {
                rightConnected = true;
            }
            else if (productId == 0x2009 || productId == 0x200E || productId == 0x2017 || productId == 0x2019)
            {
                leftConnected = true;
                rightConnected = true;
                proConnected = true;
            }
        }

        if (!leftConnected && !rightConnected)
        {
            return (ConnectionState.Disconnected, "左/右搖桿皆未連線");
        }

        if (leftConnected && rightConnected)
        {
            return proConnected
                ? (ConnectionState.DualConnected, "已連接 Pro 控制器")
                : (ConnectionState.DualConnected, "已連接左及右搖桿");
        }

        return leftConnected
            ? (ConnectionState.SingleConnected, "右搖桿未連線")
            : (ConnectionState.SingleConnected, "左搖桿未連線");
    }

    private static void EnableImu(HidStream stream, HidDevice device)
    {
        var outputReportLength = device.GetMaxOutputReportLength();
        var command = new byte[outputReportLength];

        command[0] = 0x01; // Output report ID
        command[1] = 0x00; // Header / packet number
        command[2] = 0x40; // Subcommand: enable IMU
        command[3] = 0x01; // Enable

        stream.Write(command, 0, command.Length);
    }

    private static bool TryReadReport(HidStream stream, byte[] buffer, out int bytesRead)
    {
        try
        {
            bytesRead = stream.Read(buffer, 0, buffer.Length);
            return bytesRead > 0;
        }
        catch (TimeoutException)
        {
            bytesRead = 0;
            return false;
        }
    }

    private static void ProcessReport(byte[] report, int length, ushort productId = 0)
    {
        if (length < 1)
        {
            return;
        }

        var reportId = report[0];
        if (reportId != 0x30 && reportId != 0x31 && reportId != 0x21 && reportId != 0x22)
        {
            return;
        }

        var hex = string.Join(" ", report.Take(length).Select(b => b.ToString("X2")));
        LogDebug($"Controller 0x{productId:X4} report 0x{reportId:X2} length={length} bytes={hex}");

        var buttons = ParseButtons(report, length);
        var accel = ParseAccelerometer(report, length);
        var gyro = ParseGyroscope(report, length);

        LogDebug($"Controller 0x{productId:X4} Buttons=0x{buttons:X4} Accel=({accel.x:F3},{accel.y:F3},{accel.z:F3}) Gyro=({gyro.x:F1},{gyro.y:F1},{gyro.z:F1})");

        MapButtonsToKeys(buttons);
        MapMotionToKeys(accel, gyro);
    }

    private static ushort ParseButtons(byte[] data, int length)
    {
        if (length < 5)
        {
            return 0;
        }

        return (ushort)(data[3] | (data[4] << 8));
    }

    private static void MapButtonsToKeys(ushort buttons)
    {
        var pressed = new Dictionary<ushort, VirtualKeyShort>
        {
            [0x0001] = VirtualKeyShort.KEY_A,
            [0x0002] = VirtualKeyShort.KEY_B,
            [0x0004] = VirtualKeyShort.KEY_X,
            [0x0008] = VirtualKeyShort.KEY_Y,
            [0x0010] = VirtualKeyShort.LSHIFT,
            [0x0020] = VirtualKeyShort.LCONTROL,
            [0x0040] = VirtualKeyShort.SPACE,
            [0x0080] = VirtualKeyShort.LEFT,
            [0x0100] = VirtualKeyShort.UP,
            [0x0200] = VirtualKeyShort.RIGHT,
            [0x0400] = VirtualKeyShort.DOWN,
            [0x0800] = VirtualKeyShort.KEY_Z,
            [0x1000] = VirtualKeyShort.KEY_C,
            [0x2000] = VirtualKeyShort.KEY_V,
            [0x4000] = VirtualKeyShort.KEY_U,
            [0x8000] = VirtualKeyShort.KEY_I,
        };

        foreach (var (bit, key) in pressed)
        {
            var isDown = (buttons & bit) != 0;
            UpdateVirtualKeyState(key, isDown, "button");
        }
    }

    private static (double x, double y, double z) ParseAccelerometer(byte[] data, int length)
    {
        if (length < 9)
        {
            return (0.0, 0.0, 0.0);
        }

        var x = ReadInt16(data, 3) / 2048.0;
        var y = ReadInt16(data, 5) / 2048.0;
        var z = ReadInt16(data, 7) / 2048.0;
        return (x, y, z);
    }

    private static (double x, double y, double z) ParseGyroscope(byte[] data, int length)
    {
        if (length < 15)
        {
            return (0.0, 0.0, 0.0);
        }

        var x = ReadInt16(data, 9) / 16.0;
        var y = ReadInt16(data, 11) / 16.0;
        var z = ReadInt16(data, 13) / 16.0;
        return (x, y, z);
    }

    private static short ReadInt16(byte[] data, int offset)
    {
        if (offset + 1 >= data.Length)
        {
            return 0;
        }

        return (short)(data[offset] | (data[offset + 1] << 8));
    }

    private static readonly HashSet<VirtualKeyShort> _heldKeys = new();

    private static void UpdateVirtualKeyState(VirtualKeyShort key, bool isDown, string source)
    {
        if (isDown && !_heldKeys.Contains(key))
        {
            SendKey(key, true);
            _heldKeys.Add(key);
            LogDebug($"{source} key down: {key}");
            return;
        }

        if (!isDown && _heldKeys.Contains(key))
        {
            SendKey(key, false);
            _heldKeys.Remove(key);
            LogDebug($"{source} key up: {key}");
        }
    }

    private static void MapMotionToKeys((double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        var jumpKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.UP_ARROW : VirtualKeyShort.KEY_R;
        var downKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.DOWN_ARROW : VirtualKeyShort.KEY_V;
        var leftKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.LEFT_ARROW : VirtualKeyShort.KEY_D;
        var rightKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RIGHT_ARROW : VirtualKeyShort.KEY_G;
        var hitKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RETURN : VirtualKeyShort.KEY_Z;

        if (DateTime.UtcNow - _lastJump > MotionCooldown && accel.y > JumpThreshold)
        {
            LogDebug($"Jump trigger: accel.y={accel.y:F3} threshold={JumpThreshold}");
            SendKeyPress(jumpKey);
            _lastJump = DateTime.UtcNow;
        }

        if (DateTime.UtcNow - _lastHit > MotionCooldown && (Math.Abs(gyro.x) > HitThreshold || Math.Abs(gyro.y) > HitThreshold || Math.Abs(gyro.z) > HitThreshold))
        {
            LogDebug($"Hit trigger: gyro=({gyro.x:F1},{gyro.y:F1},{gyro.z:F1})");
            SendKeyPress(hitKey);
            _lastHit = DateTime.UtcNow;
        }

        var moveRight = accel.x > MoveThreshold;
        var moveLeft = accel.x < -MoveThreshold;
        var moveDown = accel.y < DownThreshold;

        if (moveRight && !_rightHeld)
        {
            SendKey(rightKey, true);
            _rightHeld = true;
            _leftHeld = false;
        }
        else if (moveLeft && !_leftHeld)
        {
            SendKey(leftKey, true);
            _leftHeld = true;
            _rightHeld = false;
        }
        else if (!moveRight && !moveLeft)
        {
            if (_rightHeld)
            {
                SendKey(rightKey, false);
                _rightHeld = false;
            }
            if (_leftHeld)
            {
                SendKey(leftKey, false);
                _leftHeld = false;
            }
        }

        if (moveDown && !_downHeld)
        {
            SendKey(downKey, true);
            _downHeld = true;
        }
        else if (!moveDown && _downHeld)
        {
            SendKey(downKey, false);
            _downHeld = false;
        }
    }

    private static void SendKeyPress(VirtualKeyShort key)
    {
        SendKey(key, true);
        SendKey(key, false);
    }

    private static void SendKey(VirtualKeyShort key, bool keyDown)
    {
        var flags = keyDown ? 0u : (uint)KEYEVENTF.KEYUP;
        LogDebug($"SendKey key={key} down={keyDown} vk={((ushort)key):X4} flags={flags:X}");
        keybd_event((byte)key, 0, flags, UIntPtr.Zero);
    }

    private static void UpdateTrayStatus(ConnectionState state, string text)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.Icon = state switch
        {
            ConnectionState.Disconnected => _redIcon ?? SystemIcons.Error,
            ConnectionState.SingleConnected => _yellowIcon ?? SystemIcons.Warning,
            ConnectionState.DualConnected => _greenIcon ?? SystemIcons.Application,
            _ => _redIcon ?? SystemIcons.Error
        };

        _notifyIcon.Text = text.Length <= 63 ? text : text.Substring(0, 63);
        if (_statusMenuItem is not null)
        {
            _statusMenuItem.Text = $"狀態：{text}";
        }
    }

    private static void LoadNotifyIcons()
    {
        _redIcon = CreateColorIcon(Color.Red);
        _yellowIcon = CreateColorIcon(Color.Yellow);
        _greenIcon = CreateColorIcon(Color.LimeGreen);
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

    internal enum ConnectionState
    {
        Disconnected,
        SingleConnected,
        DualConnected
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private enum KEYEVENTF : uint
    {
        KEYUP = 0x0002,
        SCANCODE = 0x0008
    }

    private enum VirtualKeyShort : short
    {
        KEY_A = 0x41,
        KEY_B = 0x42,
        KEY_C = 0x43,
        KEY_D = 0x44,
        KEY_G = 0x47,
        KEY_I = 0x49,
        KEY_R = 0x52,
        KEY_U = 0x55,
        KEY_V = 0x56,
        KEY_X = 0x58,
        KEY_Y = 0x59,
        KEY_Z = 0x5A,
        LSHIFT = 0xA0,
        LCONTROL = 0xA2,
        SPACE = 0x20,
        LEFT = 0x25,
        UP = 0x26,
        RIGHT = 0x27,
        DOWN = 0x28,
        UP_ARROW = 0x26,
        DOWN_ARROW = 0x28,
        LEFT_ARROW = 0x25,
        RIGHT_ARROW = 0x27,
        RETURN = 0x0D
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public VirtualKeyShort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
