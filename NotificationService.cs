using Microsoft.Toolkit.Uwp.Notifications;

namespace SwitchMotionBridge;

// 依 AppConfig.UseToastNotifications 切換以主控台或 Windows Toast 顯示提示訊息。
internal static class NotificationService
{
    public static void Notify(string message)
    {
        if (!AppConfig.UseToastNotifications)
        {
            Console.WriteLine(message);
            return;
        }

        try
        {
            new ToastContentBuilder()
                .AddText(message)
                .Show();
        }
        catch (Exception ex)
        {
            // 未封裝的桌面應用程式在部分系統上可能無法顯示 Toast，回退為主控台輸出
            Console.WriteLine($"顯示 Toast 通知失敗，改用主控台輸出：{ex.Message}");
            Console.WriteLine(message);
        }
    }
}
