using System.Runtime.InteropServices;

namespace SwitchMotionBridge;

// 虛擬鍵盤代碼（Windows Virtual-Key Codes）
internal enum VirtualKeyShort : short
{
    KEY_D = 0x44,
    KEY_G = 0x47,
    KEY_R = 0x52,
    KEY_V = 0x56,
    KEY_Z = 0x5A,
    UP_ARROW = 0x26,
    DOWN_ARROW = 0x28,
    LEFT_ARROW = 0x25,
    RIGHT_ARROW = 0x27,
    RETURN = 0x0D
}

// 透過 Windows 的 SendInput API 模擬鍵盤輸入。
internal static class KeyboardSender
{
    private enum KEYEVENTF : uint
    {
        KEYUP = 0x0002 // 標示為鍵盤抬起事件
    }

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

    // 模擬快速按一下鍵盤（按下後立即抬起）
    public static void SendKeyPress(VirtualKeyShort key)
    {
        SendKey(key, true);
        SendKey(key, false);
    }

    // 模擬鍵盤按下或抬起事件，可用於長按不放的場合
    public static void SendKey(VirtualKeyShort key, bool keyDown)
    {
        var input = new INPUT
        {
            type = 1,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    dwFlags = keyDown ? 0u : (uint)KEYEVENTF.KEYUP,
                    dwExtraInfo = GetMessageExtraInfo()
                }
            }
        };

        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}
