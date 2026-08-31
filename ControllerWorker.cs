using System.ComponentModel;
using HidSharp;

namespace SwitchMotionBridge;

// 在背景執行執行緒，負責偵測 Joy-Con/Pro 控制器、讀取 HID 報告並轉發體感資料。
internal sealed class ControllerWorker
{
    private readonly MotionKeyMapper _keyMapper;
    private readonly Action<ConnectionState, string> _onStatusChanged; // 連線狀態變更時的回調（更新系統匣圖示）
    private CancellationTokenSource? _cts;
    private Thread? _workerThread;

    public ControllerWorker(PlayerMode mode, Action<ConnectionState, string> onStatusChanged)
    {
        _keyMapper = new MotionKeyMapper(mode);
        _onStatusChanged = onStatusChanged;
    }

    // 建立並啟動背景執行緒
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _workerThread = new Thread(() => WorkerLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "SwitchMotionBridgeWorker"
        };
        _workerThread.Start();
    }

    // 取消並等待執行緒結束
    public void Stop()
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

    // 主迴圈：持續偵測控制器、開啟設備並不斷讀取報告，連線中斷時自動重試
    private void WorkerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var devices = GetConnectedSwitchControllers();
            if (devices.Length == 0)
            {
                _onStatusChanged(ConnectionState.Disconnected, "左/右搖桿皆未連線");
                Thread.Sleep(5000);
                continue;
            }

            var (state, statusText) = GetConnectionStatus(devices);
            _onStatusChanged(state, statusText);

            try
            {
                // 目前只選用第一個偵測到的設備進行讀取
                var (productId, device) = devices[0];
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
                // 錯誤碼 1223：使用者取消了操作（例如拔除設備）
                _onStatusChanged(ConnectionState.Disconnected, "存取控制器遭到取消");
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                _onStatusChanged(ConnectionState.Disconnected, ex.Message);
                Thread.Sleep(5000);
            }
        }
    }

    // 枚舉目前已連接的所有支援控制器（USB/藍苽）
    private static (ushort productId, HidDevice device)[] GetConnectedSwitchControllers()
    {
        var list = DeviceList.Local;
        var connected = new List<(ushort productId, HidDevice device)>();

        foreach (var productId in AppConfig.SupportedProductIds)
        {
            foreach (var device in list.GetHidDevices(AppConfig.NintendoVendorId, productId))
            {
                connected.Add((productId, device));
            }
        }

        return connected.ToArray();
    }

    // 根據已連接的產品 ID判斷左/右搖桿或 Pro 控制器的連線狀態與顯示訊息
    private static (ConnectionState state, string statusText) GetConnectionStatus((ushort productId, HidDevice device)[] devices)
    {
        var leftConnected = false;
        var rightConnected = false;
        var proConnected = false;

        foreach (var (productId, _) in devices)
        {
            if (productId == 0x2006)
            {
                leftConnected = true;
            }
            else if (productId == 0x2007)
            {
                rightConnected = true;
            }
            else if (productId == 0x2009 || productId == 0x2017)
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

    // 發送子命令要求控制器啟用 IMU（陀螺儀/加速度計）偵測
    private static void EnableImu(HidStream stream, HidDevice device)
    {
        var outputReportLength = device.GetMaxOutputReportLength();
        var command = new byte[outputReportLength];

        command[0] = 0x01; // 輸出報告 ID
        command[1] = 0x00; // 標頭 / 封包編號
        command[2] = 0x40; // 子命令：啟用 IMU
        command[3] = 0x01; // 啟用

        stream.Write(command, 0, command.Length);
    }

    // 嘗試讀取一筆 HID 報告，超時則視為讀取失敗（不中斷迴圈）
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

    // 解析報告內容，列印除錯訊息，並在啟用體感轉按鍵時進一步處理
    private void ProcessReport(byte[] report, int length)
    {
        if (length < 1)
        {
            return;
        }

        var reportId = report[0];
        if (reportId != 0x30 && reportId != 0x31 && reportId != 0x21 && reportId != 0x22)
        {
            return; // 非包含 IMU 資料的報告類型，略過
        }

        var accel = MotionParser.ParseAccelerometer(report, length);
        var gyro = MotionParser.ParseGyroscope(report, length);

        Console.WriteLine(
            $"Report: 0x{reportId:X2} | " +
            $"Accel X: {accel.x:F3}, Y: {accel.y:F3}, Z: {accel.z:F3} | " +
            $"Gyro X: {gyro.x:F2}, Y: {gyro.y:F2}, Z: {gyro.z:F2}");

        if (AppConfig.MotionKeyMappingEnabled)
        {
            _keyMapper.MapMotionToKeys(accel, gyro);
        }
    }
}
