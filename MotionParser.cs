namespace SwitchMotionBridge;

// 控制器實體按鈕的目前按下狀態（對應標準輸入報告 byte[3]/[4]/[5]）。
internal readonly record struct ControllerButtons(
    bool Y, bool X, bool B, bool A, bool R, bool ZR,
    bool Minus, bool Plus, bool RStick, bool LStick, bool Home, bool Capture,
    bool Down, bool Up, bool Right, bool Left, bool L, bool ZL,
    bool SL, bool SR);

// 將 Joy-Con / Pro 控制器的 IMU 報告位元組純粹解析為加速度計與陀螺儀數值。
internal static class MotionParser
{
    // 從標準輸入報告的 byte[3]/[4]/[5] 解析出各實體按鈕的按下狀態
    public static ControllerButtons ParseButtons(byte[] data, int length)
    {
        if (length < 6)
        {
            return default;
        }

        var b3 = data[3]; // 右 Joy-Con 按鈕
        var b4 = data[4]; // 共用按鈕
        var b5 = data[5]; // 左 Joy-Con 按鈕

        return new ControllerButtons(
            Y: (b3 & 0x01) != 0,
            X: (b3 & 0x02) != 0,
            B: (b3 & 0x04) != 0,
            A: (b3 & 0x08) != 0,
            R: (b3 & 0x40) != 0,
            ZR: (b3 & 0x80) != 0,
            Minus: (b4 & 0x01) != 0,
            Plus: (b4 & 0x02) != 0,
            RStick: (b4 & 0x04) != 0,
            LStick: (b4 & 0x08) != 0,
            Home: (b4 & 0x10) != 0,
            Capture: (b4 & 0x20) != 0,
            Down: (b5 & 0x01) != 0,
            Up: (b5 & 0x02) != 0,
            Right: (b5 & 0x04) != 0,
            Left: (b5 & 0x08) != 0,
            L: (b5 & 0x40) != 0,
            ZL: (b5 & 0x80) != 0,
            SL: (b3 & 0x20) != 0 || (b5 & 0x20) != 0,
            SR: (b3 & 0x10) != 0 || (b5 & 0x10) != 0);
    }


    // 從報告位元組的固定偏移量解析出 X/Y/Z 軸加速度（單位：重力加速度 g）
    public static (double x, double y, double z) ParseAccelerometer(byte[] data, int length)
    {
        if (length < 24)
        {
            return (0.0, 0.0, 0.0);
        }

        var x = ReadInt16(data, 13) / 2048.0;
        var y = ReadInt16(data, 15) / 2048.0;
        var z = ReadInt16(data, 17) / 2048.0;
        return (x, y, z);
    }

    // 從報告位元組的固定偏移量解析出 X/Y/Z 軸陀螺儀角速度（單位：度/秒）
    public static (double x, double y, double z) ParseGyroscope(byte[] data, int length)
    {
        if (length < 28)
        {
            return (0.0, 0.0, 0.0);
        }

        var x = ReadInt16(data, 19) / 16.0;
        var y = ReadInt16(data, 21) / 16.0;
        var z = ReadInt16(data, 23) / 16.0;
        return (x, y, z);
    }

    // 從位元組中以小端序讀取一個 16 位有符號整數
    private static short ReadInt16(byte[] data, int offset)
    {
        if (offset + 1 >= data.Length)
        {
            return 0;
        }

        return (short)(data[offset] | (data[offset + 1] << 8));
    }
}
