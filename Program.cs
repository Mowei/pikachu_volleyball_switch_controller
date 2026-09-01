using SwitchMotionBridge.Tray;

namespace SwitchMotionBridge;

// 程式進入點：啟動系統匣應用程式。
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // 玩家模式預設為雙人，可於執行期間透過系統匣選單切換
        Application.Run(new TrayApplicationContext(AppConfig.DefaultPlayerMode));
    }
}
