namespace SwitchMotionBridge;

// 程式進入點：啟動系統匣應用程式。
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // 依命令列參數決定玩家模式，並啟動系統匣主程式
        Application.Run(new TrayApplicationContext(AppConfig.DetermineMode()));
    }
}
