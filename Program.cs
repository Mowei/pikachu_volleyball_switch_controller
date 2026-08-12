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
    private static readonly ushort[] SupportedProductIds = { 0x2006, 0x2007, 0x2009, 0x2017 };
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
    private static readonly PlayerMode Mode = DetermineMode();
    private static NotifyIcon? _notifyIcon;
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
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Switch Motion Bridge",
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
            };

            _notifyIcon.ContextMenuStrip.Items.Add("Show status", null, ShowStatus_Click);
            _notifyIcon.ContextMenuStrip.Items.Add("Exit", null, Exit_Click);
            _notifyIcon.DoubleClick += (_, _) => ShowStatusBalloon();

            StartWorker();
            ShowStatusBalloon("啟動完成", "Switch Motion Bridge 已啟動，右鍵選單可退出。", ToolTipIcon.Info);
        }

        private void ShowStatus_Click(object? sender, EventArgs e)
        {
            ShowStatusBalloon();
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
            var device = FindSwitchController();
            if (device is null)
            {
                ShowStatusBalloon("未找到控制器", "請連接 Joy-Con 或 Pro Controller。將在 5 秒後重試。", ToolTipIcon.Warning);
                Thread.Sleep(5000);
                continue;
            }

            ShowStatusBalloon("已連接控制器", $"找到控制器，請開始體感操作。", ToolTipIcon.Info);

            try
            {
                using var stream = device.Open();
                stream.ReadTimeout = 2000;
                EnableImu(stream, device);

                var reportBuffer = new byte[device.GetMaxInputReportLength()];
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!TryReadReport(stream, reportBuffer, out var bytesRead))
                    {
                        continue;
                    }

                    ProcessReport(reportBuffer, bytesRead);
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                ShowStatusBalloon("操作已取消", "存取控制器時遭到取消。", ToolTipIcon.Error);
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                ShowStatusBalloon("讀取失敗", ex.Message, ToolTipIcon.Error);
                Thread.Sleep(5000);
            }
        }
    }

    private static HidDevice? FindSwitchController()
    {
        var list = DeviceList.Local;

        foreach (var productId in SupportedProductIds)
        {
            foreach (var device in list.GetHidDevices(NintendoVendorId, productId))
            {
                return device;
            }
        }

        return null;
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

    private static void ProcessReport(byte[] report, int length)
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

        var accel = ParseAccelerometer(report, length);
        var gyro = ParseGyroscope(report, length);

        MapMotionToKeys(accel, gyro);
    }

    private static (double x, double y, double z) ParseAccelerometer(byte[] data, int length)
    {
        if (length < 24)
        {
            return (0.0, 0.0, 0.0);
        }

        var x = ReadInt16(data, 13) / 2048.0;
        var y = ReadInt16(data, 15) / 2048.0;
        var z = ReadInt16(data, 17) / 2048.0;
        return (x, y, z);
    }

    private static (double x, double y, double z) ParseGyroscope(byte[] data, int length)
    {
        if (length < 28)
        {
            return (0.0, 0.0, 0.0);
        }

        var x = ReadInt16(data, 19) / 16.0;
        var y = ReadInt16(data, 21) / 16.0;
        var z = ReadInt16(data, 23) / 16.0;
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

    private static void MapMotionToKeys((double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        var jumpKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.UP_ARROW : VirtualKeyShort.KEY_R;
        var downKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.DOWN_ARROW : VirtualKeyShort.KEY_V;
        var leftKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.LEFT_ARROW : VirtualKeyShort.KEY_D;
        var rightKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RIGHT_ARROW : VirtualKeyShort.KEY_G;
        var hitKey = Mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RETURN : VirtualKeyShort.KEY_Z;

        if (DateTime.UtcNow - _lastJump > MotionCooldown && accel.y > JumpThreshold)
        {
            SendKeyPress(jumpKey);
            _lastJump = DateTime.UtcNow;
        }

        if (DateTime.UtcNow - _lastHit > MotionCooldown && (Math.Abs(gyro.x) > HitThreshold || Math.Abs(gyro.y) > HitThreshold || Math.Abs(gyro.z) > HitThreshold))
        {
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
        var input = new INPUT
        {
            type = 1,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    dwFlags = keyDown ? 0u : (uint)KEYEVENTF.KEYUP,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static void ShowStatusBalloon(string title = "Switch Motion Bridge", string text = "運行中，右鍵選單可退出。", ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(2000);
        _notifyIcon.Text = text.Length <= 63 ? text : text.Substring(0, 63);
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    private enum KEYEVENTF : uint
    {
        KEYUP = 0x0002
    }

    private enum VirtualKeyShort : short
    {
        KEY_D = 0x44,
        KEY_G = 0x47,
        KEY_R = 0x52,
        KEY_V = 0x56,
        KEY_Z = 0x5A,
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
