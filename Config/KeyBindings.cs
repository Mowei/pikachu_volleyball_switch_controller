using SwitchMotionBridge.Enums;
using System.Text.Json;

namespace SwitchMotionBridge.Config;

// 單一模式（單人／雙人）下，各動作對應的按鍵名稱（對應 VirtualKeyShort 成員名稱）。
internal sealed class KeyBindingSet
{
    public string Jump { get; set; } = "";
    public string Down { get; set; } = "";
    public string Left { get; set; } = "";
    public string Right { get; set; } = "";
    public string Hit { get; set; } = "";

    // 實體按鈕對鍵盤按鍵的對應（非體感），鍵名例如 A、B、X、Y、L、R、ZL、ZR、Plus、Minus、Home、Capture、LStick、RStick、SL、SR、DPadUp/Down/Left/Right
    public Dictionary<string, string> Buttons { get; set; } = new();
}

// 按鍵設定檔的完整資料結構，分別對應單人模式與雙人模式（左搖桿玩家）。
internal sealed class KeyBindingsData
{
    public KeyBindingSet SinglePlayer { get; set; } = new()
    {
        Jump = nameof(VirtualKeyShort.UP_ARROW),
        Down = nameof(VirtualKeyShort.DOWN_ARROW),
        Left = nameof(VirtualKeyShort.LEFT_ARROW),
        Right = nameof(VirtualKeyShort.RIGHT_ARROW),
        Hit = nameof(VirtualKeyShort.RETURN)
    };

    public KeyBindingSet LeftPlayer { get; set; } = new()
    {
        Jump = nameof(VirtualKeyShort.KEY_R),
        Down = nameof(VirtualKeyShort.KEY_F),
        Left = nameof(VirtualKeyShort.KEY_D),
        Right = nameof(VirtualKeyShort.KEY_G),
        Hit = nameof(VirtualKeyShort.KEY_Z)
    };

    public KeyBindingSet RightPlayer { get; set; } = new()
    {
        Jump = nameof(VirtualKeyShort.UP_ARROW),
        Down = nameof(VirtualKeyShort.DOWN_ARROW),
        Left = nameof(VirtualKeyShort.LEFT_ARROW),
        Right = nameof(VirtualKeyShort.RIGHT_ARROW),
        Hit = nameof(VirtualKeyShort.RETURN)
    };
}

// 負責讀取、建立與解析按鍵設定檔（keybindings.json）。
internal static class KeyBindings
{
    public static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "keybindings.json");

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions DeserializerOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }; // 允許設定檔內含 // 說明註解

    // 若設定檔不存在，寫入一份包含預設按鍵綁定的檔案
    public static void EnsureFileExists()
    {
        if (!File.Exists(FilePath))
        {
            Save(new KeyBindingsData());
        }
    }

    // 讀取設定檔，檔案不存在或解析失敗時回退為預設按鍵綁定
    public static KeyBindingsData Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new KeyBindingsData();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<KeyBindingsData>(json, DeserializerOptions) ?? new KeyBindingsData();
        }
        catch (Exception ex)
        {
            NotificationService.Notify($"讀取按鍵設定檔失敗，改用預設值：{ex.Message}");
            return new KeyBindingsData();
        }
    }

    private static void Save(KeyBindingsData data)
    {
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        File.WriteAllText(FilePath, json);
    }

    // 將設定檔中的按鍵名稱字串解析為 VirtualKeyShort，解析失敗時使用備援按鍵
    public static VirtualKeyShort Parse(string keyName, VirtualKeyShort fallback)
    {
        return Enum.TryParse<VirtualKeyShort>(keyName, ignoreCase: true, out var key) ? key : fallback;
    }
}
