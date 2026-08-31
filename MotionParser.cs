namespace SwitchMotionBridge;

// Pure parsing of Joy-Con / Pro Controller IMU report bytes into accelerometer and gyroscope values.
internal static class MotionParser
{
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

    private static short ReadInt16(byte[] data, int offset)
    {
        if (offset + 1 >= data.Length)
        {
            return 0;
        }

        return (short)(data[offset] | (data[offset + 1] << 8));
    }
}
