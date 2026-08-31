namespace SwitchMotionBridge;

// Tray application lifecycle: wires the tray icon UI to the controller worker.
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly TrayIconManager _trayIconManager;
    private readonly ControllerWorker _controllerWorker;

    public TrayApplicationContext(PlayerMode mode)
    {
        _trayIconManager = new TrayIconManager(Exit_Click);
        _controllerWorker = new ControllerWorker(mode, _trayIconManager.UpdateStatus);
        _controllerWorker.Start();
    }

    private void Exit_Click(object? sender, EventArgs e)
    {
        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _controllerWorker.Stop();
        _trayIconManager.Dispose();

        base.ExitThreadCore();
    }
}
