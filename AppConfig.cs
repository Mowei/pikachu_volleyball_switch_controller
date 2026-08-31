namespace SwitchMotionBridge;

// 集中管理應用程式的設定常數與啟動參數判斷。
internal static class AppConfig
{
    public static readonly ushort NintendoVendorId = 0x057E; // 任天堂廠商 ID
    public static readonly ushort[] SupportedProductIds = { 0x2006, 0x2007, 0x2009, 0x2017 }; // 支援的控制器產品 ID（左搖桿/右搖桿/Pro控制器）
    public static readonly TimeSpan MotionCooldown = TimeSpan.FromMilliseconds(250); // 跳躍/攻擊觸發後的冷卻時間，避免連續誤觸
    public static readonly double MoveThreshold = 0.6; // 判定左右移動的加速度門檻
    public static readonly double JumpThreshold = 1.7; // 判定跳躍的加速度門檻
    public static readonly double DownThreshold = -1.0; // 判定下蹲的加速度門檻
    public static readonly double HitThreshold = 1800.0; // 判定揮擊動作的陀螺儀角速度門檻
    public static bool MotionKeyMappingEnabled = false; // 是否啟用「體感轉按鍵」功能

    // 依命令列參數判斷目前為單人模式或雙人模式（左搖桿玩家）
    public static PlayerMode DetermineMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1].Equals("single", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerMode.SinglePlayer;
        }

        return PlayerMode.LeftPlayer;
    }
}
