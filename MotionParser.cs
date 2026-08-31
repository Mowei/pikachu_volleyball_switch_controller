namespace SwitchMotionBridge;

// 將 Joy-Con / Pro 控制器的 IMU 報告位元組純粹解析為加速度計與陀螺儀數值。
internal static class MotionParser
{
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
