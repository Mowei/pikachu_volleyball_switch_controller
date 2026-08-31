using System.Runtime.InteropServices;

namespace SwitchMotionBridge;

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

// Simulates keyboard input via the Windows SendInput API.
internal static class KeyboardSender
{
    private enum KEYEVENTF : uint
    {
        KEYUP = 0x0002
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

    public static void SendKeyPress(VirtualKeyShort key)
    {
        SendKey(key, true);
        SendKey(key, false);
    }

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
