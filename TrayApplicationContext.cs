namespace SwitchMotionBridge;

// 系統匣應用程式生命周期：連接系統匣圖示 UI 與控制器偵測執行緒。
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly TrayIconManager _trayIconManager;
    private readonly ControllerWorker _controllerWorker;

    public TrayApplicationContext(PlayerMode mode)
    {
        _trayIconManager = new TrayIconManager(Exit_Click, Calibrate_Click);
        _controllerWorker = new ControllerWorker(mode, _trayIconManager.UpdateStatus);
        _controllerWorker.Start();
    }

    // 點選選單中的「開始校正」時觸發一次體感零點校正
    private void Calibrate_Click(object? sender, EventArgs e)
    {
        _controllerWorker.StartCalibration();
    }

    // 點選選單中的「Exit」時結束應用程式
    private void Exit_Click(object? sender, EventArgs e)
    {
        ExitThread();
    }

    // 應用程式結束前，停止偵測執行緒並釋放圖示資源
    protected override void ExitThreadCore()
    {
        _controllerWorker.Stop();
        _trayIconManager.Dispose();

        base.ExitThreadCore();
    }
}
