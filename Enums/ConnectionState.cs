namespace SwitchMotionBridge.Enums;

// 控制器連線狀態，用於決定系統匣圖示顏色與提示文字。
internal enum ConnectionState
{
    Disconnected, // 左右搖桿皆未連線
    SingleConnected, // 僅其中一支搖桿連線
    DualConnected // 左右搖桿（或 Pro 控制器）皆已連線
}
