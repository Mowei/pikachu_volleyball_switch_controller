namespace SwitchMotionBridge.Tray;

using SwitchMotionBridge.Config;
using SwitchMotionBridge.Enums;
using System.Diagnostics;

// 系統匣應用程式生命周期：連接系統匣圖示 UI 與控制器偵測執行緒。
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly TrayIconManager _trayIconManager;
    private readonly ControllerWorker _controllerWorker;
    private readonly ConfigWatcher _keyBindingsWatcher;
    private readonly ConfigWatcher _motionSettingsWatcher;

    public TrayApplicationContext(PlayerMode mode)
    {
        _trayIconManager = new TrayIconManager(Exit_Click, Calibrate_Click, EditKeyBindings_Click, EditMotionSettings_Click, SensorSettings_Click, mode, PlayerMode_Changed);
        _controllerWorker = new ControllerWorker(mode, _trayIconManager.UpdateStatus);
        _controllerWorker.Start();

        // 監控設定檔異動，使用者編輯 keybindings.json / motionsettings.json 後可即時套用而無需重啟程式
        _keyBindingsWatcher = new ConfigWatcher(KeyBindings.FilePath, () =>
        {
            _controllerWorker.ReloadKeyBindings();
            NotificationService.Notify("按鍵設定已重新載入");
        });
        _motionSettingsWatcher = new ConfigWatcher(MotionSettings.FilePath, () =>
        {
            AppConfig.ReloadMotionSettings();
            _controllerWorker.ReloadMotionThresholds();
            NotificationService.Notify("體感參數設定已重新載入");
        });
    }

    // 點選選單中的「開始校正」時觸發一次體感零點校正
    private void Calibrate_Click(object? sender, EventArgs e)
    {
        _controllerWorker.StartCalibration();
    }

    // 使用者於選單切換玩家模式：重建裝置偵測較耗時，於背景執行緒進行以免卡住系統匣選單
    private void PlayerMode_Changed(PlayerMode mode)
    {
        Task.Run(() => _controllerWorker.SetMode(mode));
        NotificationService.Notify($"玩家模式已切換為：{mode}");
    }

    // 點選選單中的「編輯按鍵設定」時，以預設程式開啟設定檔
    private void EditKeyBindings_Click(object? sender, EventArgs e)
    {
        KeyBindings.EnsureFileExists();
        Process.Start(new ProcessStartInfo(KeyBindings.FilePath) { UseShellExecute = true });
    }

    // 點選選單中的「編輯體感參數設定」時，以預設程式開啟設定檔
    private void EditMotionSettings_Click(object? sender, EventArgs e)
    {
        MotionSettings.EnsureFileExists();
        Process.Start(new ProcessStartInfo(MotionSettings.FilePath) { UseShellExecute = true });
    }

    // 點選選單中的「感測器參數設定」時，開啟 1P/2P 體感上下限門檻設定表單
    private void SensorSettings_Click(object? sender, EventArgs e)
    {
        using var form = new SensorSettingsForm(_controllerWorker);
        form.ShowDialog();
    }

    // 點選選單中的「Exit」時結束應用程式
    private void Exit_Click(object? sender, EventArgs e)
    {
        ExitThread();
    }

    // 應用程式結束前，停止偵測執行緒並釋放圖示資源
    protected override void ExitThreadCore()
    {
        _keyBindingsWatcher.Dispose();
        _motionSettingsWatcher.Dispose();
        _controllerWorker.Stop();
        _trayIconManager.Dispose();

        base.ExitThreadCore();
    }
}
