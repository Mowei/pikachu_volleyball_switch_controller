using SwitchMotionBridge.Enums;

namespace SwitchMotionBridge;

// 集中管理應用程式的設定常數與啟動參數判斷。
internal static class AppConfig
{
    public static readonly ushort NintendoVendorId = 0x057E; // 任天堂廠商 ID
    public static readonly ushort[] SupportedProductIds = { 0x2006, 0x2007, 0x2009, 0x2017 }; // 支援的控制器產品 ID（左搖桿/右搖桿/Pro控制器）

    private static MotionSettingsData _motionSettingsData = MotionSettings.Load(); // 體感判定參數，讀自 motionsettings.json

    public static TimeSpan MotionCooldown = TimeSpan.FromMilliseconds(_motionSettingsData.MotionCooldownMs); // 跳躍/攻擊觸發後的冷卻時間，避免連續誤觸
    public static double MoveThreshold = _motionSettingsData.MoveThreshold; // 判定左右移動的加速度門檻
    public static double MoveReleaseThreshold = _motionSettingsData.MoveReleaseThreshold; // 已按住方向鍵時的鬆開門檻（低於觸發門檻，避免抖動連點）
    public static double JumpThreshold = _motionSettingsData.JumpThreshold; // 判定跳躍的加速度門檻
    public static double DownThreshold = _motionSettingsData.DownThreshold; // 判定下蹲的加速度門檻
    public static double DownReleaseThreshold = _motionSettingsData.DownReleaseThreshold; // 已按住下蹲鍵時的鬆開門檻（避免抖動連點）
    public static double HitThreshold = _motionSettingsData.HitThreshold; // 判定揮擊動作的陀螺儀角速度門檻
    public static volatile bool AutoCalibrationEnabled; // 是否偵測手把水平靜止並自動觸發校正
    public static double StillAccelTolerance = _motionSettingsData.StillAccelTolerance; // 判定靜止的加速度向量長度與 1g 的容許誤差
    public static double StillGyroTolerance = _motionSettingsData.StillGyroTolerance; // 判定靜止的陀螺儀角速度容許值（度/秒）
    public static TimeSpan StillDuration = TimeSpan.FromMilliseconds(_motionSettingsData.StillDurationMs); // 需持續靜止多久才自動觸發校正
    public static volatile bool MotionKeyMappingEnabled = true; // 是否啟用「體感轉按鍵」功能
    public static volatile bool ButtonKeyMappingEnabled = true; // 是否啟用「按鈕轉按鍵」功能（非體感，依 keybindings.json 的 Buttons 設定）
    public static volatile bool VerboseLogging = false; // 是否於主控台列印每筆 IMU 報告，預設關閉以免洗版拖慢效能
    public static volatile bool UseToastNotifications = true; // 訊息通知方式：true 使用 Windows Toast，false 使用主控台輸出

    static AppConfig()
    {
        AutoCalibrationEnabled = _motionSettingsData.AutoCalibrationEnabled;
    }

    // 依命令列參數判斷目前為單人模式或雙人模式；未帶參數時預設為雙人模式
    public static PlayerMode DetermineMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1].Equals("single", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerMode.SinglePlayer;
        }

        return PlayerMode.DualPlayer;
    }

    // 重新讀取 motionsettings.json 並套用新的門檻值，供設定檔熱重載使用
    public static void ReloadMotionSettings()
    {
        _motionSettingsData = MotionSettings.Load();
        MotionCooldown = TimeSpan.FromMilliseconds(_motionSettingsData.MotionCooldownMs);
        MoveThreshold = _motionSettingsData.MoveThreshold;
        MoveReleaseThreshold = _motionSettingsData.MoveReleaseThreshold;
        JumpThreshold = _motionSettingsData.JumpThreshold;
        DownThreshold = _motionSettingsData.DownThreshold;
        DownReleaseThreshold = _motionSettingsData.DownReleaseThreshold;
        HitThreshold = _motionSettingsData.HitThreshold;
        AutoCalibrationEnabled = _motionSettingsData.AutoCalibrationEnabled;
        StillAccelTolerance = _motionSettingsData.StillAccelTolerance;
        StillGyroTolerance = _motionSettingsData.StillGyroTolerance;
        StillDuration = TimeSpan.FromMilliseconds(_motionSettingsData.StillDurationMs);
    }
}
