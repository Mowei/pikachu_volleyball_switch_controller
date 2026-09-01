using System.Collections.Concurrent;
using System.ComponentModel;
using HidSharp;
using SwitchMotionBridge.Enums;
using SwitchMotionBridge.KeyMapping;
using SwitchMotionBridge.Motion;

namespace SwitchMotionBridge;

// 在背景執行執行緒，負責偵測 Joy-Con/Pro 控制器、讀取 HID 報告並轉發體感資料。
internal sealed class ControllerWorker
{
    private readonly Action<ConnectionState, string> _onStatusChanged; // 連線狀態變更時的回調（更新系統匣圖示）
    private readonly ConcurrentDictionary<string, DeviceReaderState> _deviceReaders = new(); // 依裝置路徑追蹤各自的讀取執行緒與按鍵映射
    private readonly object _processLock = new(); // 多裝置可能同時回報動作，序列化按鍵狀態機的存取
    private readonly object _modeLock = new(); // 序列化玩家模式切換，避免重覆呼叫 Stop/Start 互相干擾
    private PlayerMode _defaultMode;
    private CancellationTokenSource? _cts;
    private Thread? _monitorThread;

    private sealed class DeviceReaderState
    {
        public Thread Thread { get; }
        public MotionKeyMapper Mapper { get; }
        public ButtonKeyMapper ButtonMapper { get; }
        public MotionCalibrator Calibrator { get; }

        public DeviceReaderState(Thread thread, MotionKeyMapper mapper, ButtonKeyMapper buttonMapper, MotionCalibrator calibrator)
        {
            Thread = thread;
            Mapper = mapper;
            ButtonMapper = buttonMapper;
            Calibrator = calibrator;
        }
    }

    public ControllerWorker(PlayerMode mode, Action<ConnectionState, string> onStatusChanged)
    {
        _defaultMode = mode;
        _onStatusChanged = onStatusChanged;
    }

    // 目前套用的玩家模式，供系統匣選單顯示目前選取狀態
    public PlayerMode CurrentMode => _defaultMode;

    // 切換玩家模式：停止目前所有裝置讀取執行緒，改用新模式重新偵測與建立
    public void SetMode(PlayerMode mode)
    {
        lock (_modeLock)
        {
            if (_defaultMode == mode)
            {
                return;
            }

            _defaultMode = mode;
            Stop();
            Start();
        }
    }

    // 觸發一次新的體感零點校正
    public void StartCalibration()
    {
        foreach (var reader in _deviceReaders.Values)
        {
            reader.Calibrator.StartCalibration();
        }
    }

    // 通知所有目前作用中的裝置重新讀取 keybindings.json，供設定檔熱重載使用
    public void ReloadKeyBindings()
    {
        foreach (var reader in _deviceReaders.Values)
        {
            reader.Mapper.ReloadBindings();
            reader.ButtonMapper.ReloadBindings();
        }
    }

    // 通知所有目前作用中的裝置重新讀取 motionsettings.json 的 1P/2P 門檻覆寫，供設定檔熱重載使用
    public void ReloadMotionThresholds()
    {
        foreach (var reader in _deviceReaders.Values)
        {
            reader.Mapper.ReloadThresholds();
        }
    }

    // 建立並啟動監控執行緒
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _monitorThread = new Thread(() => MonitorLoop(_cts.Token))
        {
            IsBackground = true,
            Name = "SwitchMotionBridgeMonitor"
        };
        _monitorThread.Start();
    }

    // 取消並等待監控執行緒與所有裝置讀取執行緒結束
    public void Stop()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        // 逾時需大於裝置讀取的 HID ReadTimeout（2000ms），避免執行緒仍在阻塞讀取時就被視為逾時
        var joinTimeout = TimeSpan.FromSeconds(3);

        if (_monitorThread is not null && !_monitorThread.Join(joinTimeout))
        {
            NotificationService.Notify("監控執行緒未能於逾時內結束，可能仍在背景執行");
        }

        foreach (var reader in _deviceReaders.Values)
        {
            if (!reader.Thread.Join(joinTimeout))
            {
                NotificationService.Notify("裝置讀取執行緒未能於逾時內結束，可能仍在背景執行");
            }
        }

        _deviceReaders.Clear();
        _cts.Dispose();
        _cts = null;
    }

    // 監控迴圈：持續偵測已連接的控制器，為每個尚未讀取的裝置各自建立讀取執行緒
    private void MonitorLoop(CancellationToken cancellationToken)
    {
        var firstCheck = true;
        while (!cancellationToken.IsCancellationRequested)
        {
            var devices = GetConnectedSwitchControllers();
            if (firstCheck)
            {
                Console.WriteLine($"[診斷] 初始檢測到 {devices.Length} 個控制器");
                foreach (var (productId, device) in devices)
                {
                    Console.WriteLine($"[診斷]   - 產品 ID: 0x{productId:X4}, 路徑: {device.DevicePath}");
                }
                firstCheck = false;
            }
            
            if (devices.Length == 0)
            {
                _onStatusChanged(
                    ConnectionState.Disconnected,
                    "未偵測到 Nintendo Switch 控制器。若搖桿燈持續閃爍，表示尚未完成 Windows 藍牙配對或已脫離連線，請先完成配對後再啟動程式。");
            }
            else
            {
                var (state, statusText) = GetConnectionStatus(devices);
                _onStatusChanged(state, statusText);

                foreach (var (_, device) in devices)
                {
                    var playerMode = ResolveDevicePlayerMode(device, _defaultMode);
                    _deviceReaders.AddOrUpdate(
                        device.DevicePath,
                        _ => StartDeviceReader(device, playerMode, cancellationToken),
                        (_, existingReader) => existingReader.Thread.IsAlive ? existingReader : StartDeviceReader(device, playerMode, cancellationToken));
                }
            }

            Thread.Sleep(2000);
        }
    }

    // 建立並啟動單一裝置的讀取執行緒
    private DeviceReaderState StartDeviceReader(HidDevice device, PlayerMode playerMode, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[診斷] 為控制器 0x{device.ProductID:X4} 建立讀取執行緒...");
        var mapper = new MotionKeyMapper(playerMode);
        var buttonMapper = new ButtonKeyMapper(playerMode);
        var calibrator = new MotionCalibrator();
        var thread = new Thread(() => DeviceReadLoop(device, mapper, buttonMapper, calibrator, cancellationToken))
        {
            IsBackground = true,
            Name = $"SwitchMotionBridgeDevice-{device.DevicePath}"
        };
        thread.Start();
        return new DeviceReaderState(thread, mapper, buttonMapper, calibrator);
    }

    private static PlayerMode ResolveDevicePlayerMode(HidDevice device, PlayerMode defaultMode)
    {
        if (defaultMode == PlayerMode.SinglePlayer)
        {
            return PlayerMode.SinglePlayer;
        }

        var productId = device.ProductID;
        if (defaultMode == PlayerMode.DualPlayer)
        {
            return productId == 0x2007 ? PlayerMode.RightPlayer : PlayerMode.LeftPlayer;
        }

        return defaultMode;
    }

    // 單一裝置的讀取迴圈：開啟串流、啟用 IMU 並持續讀取報告，裝置中斷或取消時結束
    private void DeviceReadLoop(HidDevice device, MotionKeyMapper mapper, ButtonKeyMapper buttonMapper, MotionCalibrator calibrator, CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"[診斷] 開啟設備流 0x{device.ProductID:X4}...");
            using var stream = device.Open();
            stream.ReadTimeout = 2000;
            Console.WriteLine($"[診斷] 流已開啟，初始化 HID...");

            if (!TryInitializeHid(stream, device))
            {
                // 初始化失敗，交由監控迴圈於下次偵測時重試
                Console.WriteLine($"[診斷] 初始化失敗，放棄本設備");
                return;
            }
            Console.WriteLine($"[診斷] 初始化成功，開始讀取報告...");

            var reportBuffer = new byte[device.GetMaxInputReportLength()];
            var shortReportWarned = false;
            var readCount = 0;
            var timeoutCount = 0;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!TryReadReport(stream, reportBuffer, out var bytesRead))
                {
                    timeoutCount++;
                    if (timeoutCount % 30 == 0)
                    {
                        Console.WriteLine($"[診斷] 0x{device.ProductID:X4} 已超時 {timeoutCount} 次（成功讀取 {readCount} 筆報告）");
                    }
                    continue;
                }

                readCount++;
                
                if (bytesRead < MotionParser.RequiredMotionReportLength && !shortReportWarned)
                {
                    shortReportWarned = true;
                    NotificationService.Notify($"裝置回報的資料長度過短（{bytesRead} bytes），本次連線期間體感資料可能不完整（{device.DevicePath}）");
                }

                ProcessReport(reportBuffer, bytesRead, mapper, buttonMapper, calibrator);
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 錯誤碼 1223：使用者取消了操作（例如拔除設備），交由監控迴圈於下次偵測時重建執行緒
        }
        catch (Exception ex)
        {
            NotificationService.Notify($"裝置讀取中斷（{device.DevicePath}）：{ex.Message}");
        }
        finally
        {
            _deviceReaders.TryRemove(device.DevicePath, out _);
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

    // 完成 Joy-Con / Pro Controller 的 HID 初始化：先切換到可讀的輸入報告模式，再啟用 IMU。
    private static bool TryInitializeHid(HidStream stream, HidDevice device)
    {
        // 首先查詢搖桿信息以喚醒連接
        try
        {
            Console.WriteLine($"[診斷] 查詢搖桿信息...");
            var outputReportLength = device.GetMaxOutputReportLength();
            var command = new byte[outputReportLength];

            command[0] = 0x01; // 輸出報告 ID
            command[1] = 0x00; // 標頭
            command[2] = 0x02; // 子命令：要求搖桿信息

            stream.Write(command, 0, command.Length);
            stream.Flush();
            System.Threading.Thread.Sleep(100);
            
            // 嘗試讀取回應
            stream.ReadTimeout = 500;
            var reportBuffer = new byte[device.GetMaxInputReportLength()];
            try
            {
                var bytesRead = stream.Read(reportBuffer, 0, reportBuffer.Length);
                Console.WriteLine($"[診斷] 收到搖桿信息回應：{bytesRead} 字元");
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"[診斷] 搖桿信息查詢超時");
            }
            stream.ReadTimeout = 2000;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[診斷] 搖桿信息查詢異常：{ex.Message}");
        }

        if (!TrySetInputReportMode(stream, device, 0x30))
        {
            return false;
        }

        if (!TryEnableImu(stream, device))
        {
            return false;
        }

        // 啟用振動功能（部分情況下需要此命令喚醒搖桿報告輸出）
        if (!TryEnableVibration(stream, device))
        {
            Console.WriteLine($"[診斷] 啟用振動失敗，但繼續初始化");
        }

        return true;
    }

    private static bool TryEnableVibration(HidStream stream, HidDevice device)
    {
        try
        {
            Console.WriteLine($"[診斷] 啟用振動...");
            var outputReportLength = device.GetMaxOutputReportLength();
            var command = new byte[outputReportLength];

            command[0] = 0x01; // 輸出報告 ID
            command[1] = 0x00; // 標頭
            command[2] = 0x48; // 子命令：啟用振動
            command[3] = 0x01; // 啟用

            stream.Write(command, 0, command.Length);
            stream.Flush();
            System.Threading.Thread.Sleep(100);
            Console.WriteLine($"[診斷] 振動啟用成功");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[診斷] 啟用振動失敗：{ex.Message}");
            return false;
        }
    }

    // 設定控制器的輸入報告模式，讓它開始發送 0x30/0x31 類型的 HID 資料報告。
    private static bool TrySetInputReportMode(HidStream stream, HidDevice device, byte reportMode)
    {
        try
        {
            Console.WriteLine($"[診斷] 設定輸入報告模式為 0x{reportMode:X2}...");
            var outputReportLength = device.GetMaxOutputReportLength();
            var command = new byte[outputReportLength];

            command[0] = 0x01; // 輸出報告 ID
            command[1] = 0x00; // 標頭 / 封包編號
            command[2] = 0x03; // 子命令：設定輸入報告模式
            command[3] = reportMode; // 0x30 = 標準全資料模式

            stream.Write(command, 0, command.Length);
            stream.Flush();
            System.Threading.Thread.Sleep(50);
            Console.WriteLine($"[診斷] 輸入報告模式設定成功");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[診斷] 設定輸入報告模式失敗：{ex.Message}");
            NotificationService.Notify($"設定 HID 輸入報告模式失敗（{device.DevicePath}）：{ex.Message}");
            return false;
        }
    }

    // 發送子命令要求控制器啟用 IMU（陀螺儀/加速度計）偵測，並回報是否成功送出
    private static bool TryEnableImu(HidStream stream, HidDevice device)
    {
        try
        {
            Console.WriteLine($"[診斷] 啟用 IMU...");
            var outputReportLength = device.GetMaxOutputReportLength();
            var command = new byte[outputReportLength];

            command[0] = 0x01; // 輸出報告 ID
            command[1] = 0x00; // 標頭 / 封包編號
            command[2] = 0x40; // 子命令：啟用 IMU
            command[3] = 0x01; // 啟用

            stream.Write(command, 0, command.Length);
            stream.Flush();
            System.Threading.Thread.Sleep(50);
            Console.WriteLine($"[診斷] IMU 啟用成功");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[診斷] 啟用 IMU 失敗：{ex.Message}");
            NotificationService.Notify($"啟用體感偵測失敗（{device.DevicePath}）：{ex.Message}。若搖桿燈持續閃爍，表示控制器尚未完成 Windows 配對，請先完成藍牙配對後再重啟程式。");
            return false;
        }
    }

    // 嘗試讀取一筆 HID 報告，超時則視為讀取失敗（不中斷迴圈）
    private static bool TryReadReport(HidStream stream, byte[] buffer, out int bytesRead)
    {
        try
        {
            bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0 && AppConfig.VerboseLogging)
            {
                Console.WriteLine($"[診斷] 讀取 {bytesRead} 字節");
            }
            return bytesRead > 0;
        }
        catch (TimeoutException)
        {
            bytesRead = 0;
            return false;
        }
    }

    // 解析報告內容，於啟用詳細記錄時列印除錯訊息，並在啟用體感轉按鍵/按鈕轉按鍵時進一步處理
    private void ProcessReport(byte[] report, int length, MotionKeyMapper mapper, ButtonKeyMapper buttonMapper, MotionCalibrator calibrator)
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

        var buttons = MotionParser.ParseButtons(report, length);

        // 資料長度不足時體感數值不完整，略過本次體感解析以免以錯誤的零值覆蓋按鍵狀態
        var hasFullMotionData = length >= MotionParser.RequiredMotionReportLength;
        var accel = hasFullMotionData ? MotionParser.ParseAccelerometer(report, length) : default;
        var gyro = hasFullMotionData ? MotionParser.ParseGyroscope(report, length) : default;

        // 多裝置可能同時解析並更新按鍵狀態機，需序列化避免資料競爭
        lock (_processLock)
        {
            if (hasFullMotionData)
            {
                (accel, gyro) = calibrator.Apply(accel, gyro);
            }

            if (AppConfig.VerboseLogging)
            {
                var buttonStr = $"Btn[3]=0x{report[3]:X2} Btn[4]=0x{report[4]:X2} Btn[5]=0x{report[5]:X2}";
                Console.WriteLine(
                    $"Report: 0x{reportId:X2} | " +
                    (hasFullMotionData
                        ? $"Accel X: {accel.x:F3}, Y: {accel.y:F3}, Z: {accel.z:F3} | Gyro X: {gyro.x:F2}, Y: {gyro.y:F2}, Z: {gyro.z:F2} | {buttonStr}"
                        : $"體感資料不完整 | {buttonStr}"));
            }

            if (hasFullMotionData && AppConfig.MotionKeyMappingEnabled)
            {
                mapper.MapMotionToKeys(accel, gyro);
            }

            if (AppConfig.ButtonKeyMappingEnabled)
            {
                buttonMapper.MapButtonsToKeys(buttons);
            }
        }
    }
}
