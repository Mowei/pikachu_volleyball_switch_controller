using System.Runtime.InteropServices;

namespace SwitchMotionBridge;

// 虛擬鍵盤代碼（Windows Virtual-Key Codes），完整定義常用鍵位供 MotionKeyMapper 綁定
internal enum VirtualKeyShort : short
{
    // 滑鼠鍵（部分 API 場合會用到，鍵盤模擬通常不使用）
    LBUTTON = 0x01,
    RBUTTON = 0x02,
    MBUTTON = 0x04,

    // 控制／編輯鍵
    BACK = 0x08,
    TAB = 0x09,
    CLEAR = 0x0C,
    RETURN = 0x0D,
    SHIFT = 0x10,
    CONTROL = 0x11,
    MENU = 0x12, // Alt
    PAUSE = 0x13,
    CAPITAL = 0x14, // Caps Lock
    ESCAPE = 0x1B,
    SPACE = 0x20,
    PRIOR = 0x21, // Page Up
    NEXT = 0x22, // Page Down
    END = 0x23,
    HOME = 0x24,

    // 方向鍵
    LEFT_ARROW = 0x25,
    UP_ARROW = 0x26,
    RIGHT_ARROW = 0x27,
    DOWN_ARROW = 0x28,

    SELECT = 0x29,
    PRINT = 0x2A,
    EXECUTE = 0x2B,
    SNAPSHOT = 0x2C, // Print Screen
    INSERT = 0x2D,
    DELETE = 0x2E,
    HELP = 0x2F,

    // 數字列 0-9
    KEY_0 = 0x30,
    KEY_1 = 0x31,
    KEY_2 = 0x32,
    KEY_3 = 0x33,
    KEY_4 = 0x34,
    KEY_5 = 0x35,
    KEY_6 = 0x36,
    KEY_7 = 0x37,
    KEY_8 = 0x38,
    KEY_9 = 0x39,

    // 英文字母 A-Z
    KEY_A = 0x41,
    KEY_B = 0x42,
    KEY_C = 0x43,
    KEY_D = 0x44,
    KEY_E = 0x45,
    KEY_F = 0x46,
    KEY_G = 0x47,
    KEY_H = 0x48,
    KEY_I = 0x49,
    KEY_J = 0x4A,
    KEY_K = 0x4B,
    KEY_L = 0x4C,
    KEY_M = 0x4D,
    KEY_N = 0x4E,
    KEY_O = 0x4F,
    KEY_P = 0x50,
    KEY_Q = 0x51,
    KEY_R = 0x52,
    KEY_S = 0x53,
    KEY_T = 0x54,
    KEY_U = 0x55,
    KEY_V = 0x56,
    KEY_W = 0x57,
    KEY_X = 0x58,
    KEY_Y = 0x59,
    KEY_Z = 0x5A,

    LWIN = 0x5B,
    RWIN = 0x5C,
    APPS = 0x5D,

    // 數字鍵盤（Numpad）
    NUMPAD0 = 0x60,
    NUMPAD1 = 0x61,
    NUMPAD2 = 0x62,
    NUMPAD3 = 0x63,
    NUMPAD4 = 0x64,
    NUMPAD5 = 0x65,
    NUMPAD6 = 0x66,
    NUMPAD7 = 0x67,
    NUMPAD8 = 0x68,
    NUMPAD9 = 0x69,
    MULTIPLY = 0x6A,
    ADD = 0x6B,
    SEPARATOR = 0x6C,
    SUBTRACT = 0x6D,
    DECIMAL = 0x6E,
    DIVIDE = 0x6F,

    // 功能鍵 F1-F24
    F1 = 0x70,
    F2 = 0x71,
    F3 = 0x72,
    F4 = 0x73,
    F5 = 0x74,
    F6 = 0x75,
    F7 = 0x76,
    F8 = 0x77,
    F9 = 0x78,
    F10 = 0x79,
    F11 = 0x7A,
    F12 = 0x7B,
    F13 = 0x7C,
    F14 = 0x7D,
    F15 = 0x7E,
    F16 = 0x7F,
    F17 = 0x80,
    F18 = 0x81,
    F19 = 0x82,
    F20 = 0x83,
    F21 = 0x84,
    F22 = 0x85,
    F23 = 0x86,
    F24 = 0x87,

    NUMLOCK = 0x90,
    SCROLL = 0x91,

    // 左右分開的修飾鍵
    LSHIFT = 0xA0,
    RSHIFT = 0xA1,
    LCONTROL = 0xA2,
    RCONTROL = 0xA3,
    LMENU = 0xA4,
    RMENU = 0xA5,

    // OEM 符號鍵（美式鍵盤配置）
    OEM_1 = 0xBA, // ; :
    OEM_PLUS = 0xBB, // = +
    OEM_COMMA = 0xBC, // , <
    OEM_MINUS = 0xBD, // - _
    OEM_PERIOD = 0xBE, // . >
    OEM_2 = 0xBF, // / ?
    OEM_3 = 0xC0, // ` ~
    OEM_4 = 0xDB, // [ {
    OEM_5 = 0xDC, // \ |
    OEM_6 = 0xDD, // ] }
    OEM_7 = 0xDE // ' "
}


// 透過 Windows 的 SendInput API 模擬鍵盤輸入。
internal static class KeyboardSender
{
    private enum KEYEVENTF : uint
    {
        KEYUP = 0x0002, // 標示為鍵盤抬起事件
        SCANCODE = 0x0008 // 以硬體掃描碼送出，避免被輸入法（如注音）攔截轉換
    }

    private const uint MAPVK_VK_TO_VSC = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public VirtualKeyShort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetMessageExtraInfo();

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // 模擬快速按一下鍵盤（按下後立即抬起）
    public static void SendKeyPress(VirtualKeyShort key)
    {
        SendKey(key, true);
        SendKey(key, false);
    }

    // 模擬鍵盤按下或抬起事件，可用於長按不放的場合
    public static void SendKey(VirtualKeyShort key, bool keyDown)
    {
        // 改用掃描碼送出，讓遊戲/輸入法將其視為實體按鍵，而非被注音等 IME 轉換
        var scanCode = (ushort)MapVirtualKey((uint)key, MAPVK_VK_TO_VSC);
        var flags = (uint)KEYEVENTF.SCANCODE | (keyDown ? 0u : (uint)KEYEVENTF.KEYUP);

        var input = new INPUT
        {
            type = 1,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = scanCode,
                    dwFlags = flags,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}
