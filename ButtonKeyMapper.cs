using SwitchMotionBridge.Config;
using SwitchMotionBridge.Enums;

namespace SwitchMotionBridge;

// 依 keybindings.json 中 Buttons 設定，將控制器實體按鈕的按下/放開狀態同步為鍵盤按鍵（非體感）。
internal sealed class ButtonKeyMapper
{
    private readonly Dictionary<string, VirtualKeyShort> _bindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _pressedState = new(StringComparer.OrdinalIgnoreCase);

    public ButtonKeyMapper(PlayerMode mode)
    {
        var bindings = KeyBindings.Load();
        var set = mode switch
        {
            PlayerMode.SinglePlayer => bindings.SinglePlayer,
            PlayerMode.RightPlayer => bindings.RightPlayer,
            PlayerMode.DualPlayer => bindings.LeftPlayer,
            _ => bindings.LeftPlayer
        };

        foreach (var (buttonName, keyName) in set.Buttons)
        {
            if (Enum.TryParse<VirtualKeyShort>(keyName, ignoreCase: true, out var key))
            {
                _bindings[buttonName] = key;
            }
        }
    }

    // 依目前按鈕狀態同步鍵盤按下/放開，僅在狀態變化時送出事件
    public void MapButtonsToKeys(ControllerButtons buttons)
    {
        Sync("A", buttons.A);
        Sync("B", buttons.B);
        Sync("X", buttons.X);
        Sync("Y", buttons.Y);
        Sync("L", buttons.L);
        Sync("R", buttons.R);
        Sync("ZL", buttons.ZL);
        Sync("ZR", buttons.ZR);
        Sync("Plus", buttons.Plus);
        Sync("Minus", buttons.Minus);
        Sync("Home", buttons.Home);
        Sync("Capture", buttons.Capture);
        Sync("LStick", buttons.LStick);
        Sync("RStick", buttons.RStick);
        Sync("SL", buttons.SL);
        Sync("SR", buttons.SR);
        Sync("DPadUp", buttons.Up);
        Sync("DPadDown", buttons.Down);
        Sync("DPadLeft", buttons.Left);
        Sync("DPadRight", buttons.Right);
    }

    private void Sync(string buttonName, bool isPressed)
    {
        if (!_bindings.TryGetValue(buttonName, out var key))
        {
            return;
        }

        var wasPressed = _pressedState.TryGetValue(buttonName, out var prev) && prev;
        if (isPressed == wasPressed)
        {
            return;
        }

        KeyboardSender.SendKey(key, isPressed);
        _pressedState[buttonName] = isPressed;
    }
}
