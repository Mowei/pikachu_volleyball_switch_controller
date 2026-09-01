using System.Text.Json;

namespace SwitchMotionBridge;

// 體感判定用的可調參數，對應 motionsettings.json，可不重新編譯即可調整靈敏度。
internal sealed class MotionSettingsData
{
    public double MoveThreshold { get; set; } = 0.6;
    public double MoveReleaseThreshold { get; set; } = 0.36;
    public double JumpThreshold { get; set; } = 1.7;
    public double DownThreshold { get; set; } = -1.0;
    public double DownReleaseThreshold { get; set; } = -0.6;
    public double HitThreshold { get; set; } = 1800.0;
    public int MotionCooldownMs { get; set; } = 250;
}

// 負責讀取、建立與解析體感參數設定檔（motionsettings.json）。
internal static class MotionSettings
{
    public static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "motionsettings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions DeserializerOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }; // 允許設定檔內含 // 說明註解

    // 若設定檔不存在，寫入一份包含預設參數的檔案
    public static void EnsureFileExists()
    {
        if (!File.Exists(FilePath))
        {
            Save(new MotionSettingsData());
        }
    }

    // 讀取設定檔，檔案不存在或解析失敗時回退為預設參數
    public static MotionSettingsData Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new MotionSettingsData();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<MotionSettingsData>(json, DeserializerOptions) ?? new MotionSettingsData();
        }
        catch (Exception ex)
        {
            NotificationService.Notify($"讀取體感參數設定檔失敗，改用預設值：{ex.Message}");
            return new MotionSettingsData();
        }
    }

    private static void Save(MotionSettingsData data)
    {
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(FilePath, json);
    }
}
