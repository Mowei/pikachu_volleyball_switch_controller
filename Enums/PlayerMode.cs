namespace SwitchMotionBridge.Enums;

// 玩家模式：決定體感動作對應到哪一組按鍵。
internal enum PlayerMode
{
    SinglePlayer, // 單人模式，使用方向鍵操作
    DualPlayer, // 雙人模式：1P 左控制器 + 2P 右控制器同時啟用
    LeftPlayer, // 1P：左控制器（單獨指定）
    RightPlayer // 2P：右控制器（單獨指定）
}
