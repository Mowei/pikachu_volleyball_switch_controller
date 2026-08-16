using System.Reflection;

namespace SwitchMotionBridge.Tests;

public class ControllerDetectionTests
{
    [Fact]
    public void SupportedProductIds_should_include_common_bluetooth_switch_controllers()
    {
        var field = typeof(Program).GetField("SupportedProductIds", BindingFlags.NonPublic | BindingFlags.Static);
        var productIds = Assert.IsType<ushort[]>(field?.GetValue(null));

        Assert.Contains((ushort)0x2006, productIds);
        Assert.Contains((ushort)0x2007, productIds);
        Assert.Contains((ushort)0x2009, productIds);
        Assert.Contains((ushort)0x200E, productIds);
        Assert.Contains((ushort)0x2017, productIds);
        Assert.Contains((ushort)0x2019, productIds);
    }

    [Fact]
    public void Debug_mode_should_be_detected_from_command_line_args()
    {
        var method = typeof(Program).GetMethod("IsDebugMode", BindingFlags.NonPublic | BindingFlags.Static);
        var result = (bool?)method?.Invoke(null, new object[] { new[] { "debug" } });

        Assert.True(result);
    }

    [Fact]
    public void Left_and_right_joycon_pair_should_be_detected_as_dual_connection()
    {
        var result = Program.GetConnectionStatusForProductIds(new[] { (ushort)0x2006, (ushort)0x2007 });

        Assert.Equal("已連接左及右搖桿", result.statusText);
        Assert.Equal("DualConnected", result.state.ToString());
    }

    [Fact]
    public void Joy_con_imu_report_should_parse_from_real_switch_offsets()
    {
        var sample = new byte[20];
        sample[0] = 0x30;
        sample[3] = 0x00;
        sample[4] = 0x10; // 4096 => 2.0 g
        sample[5] = 0x00;
        sample[6] = 0x00;
        sample[7] = 0x00;
        sample[8] = 0x08; // 2048 => 1.0 g
        sample[9] = 0x40;
        sample[10] = 0x01; // 320 => 20 deg/s
        sample[11] = 0x00;
        sample[12] = 0x00;
        sample[13] = 0x00;
        sample[14] = 0x00;

        var accelMethod = typeof(Program).GetMethod("ParseAccelerometer", BindingFlags.NonPublic | BindingFlags.Static);
        var gyroMethod = typeof(Program).GetMethod("ParseGyroscope", BindingFlags.NonPublic | BindingFlags.Static);

        var accelValue = ((ValueTuple<double, double, double>)accelMethod!.Invoke(null, new object[] { sample, sample.Length })!);
        var gyroValue = ((ValueTuple<double, double, double>)gyroMethod!.Invoke(null, new object[] { sample, sample.Length })!);

        Assert.Equal(2.0, accelValue.Item1, 3);
        Assert.Equal(0.0, accelValue.Item2, 3);
        Assert.Equal(1.0, accelValue.Item3, 3);
        Assert.Equal(20.0, gyroValue.Item1, 3);
        Assert.Equal(0.0, gyroValue.Item2, 3);
        Assert.Equal(0.0, gyroValue.Item3, 3);
    }

    [Fact]
    public void Joy_con_button_report_should_parse_button_bits()
    {
        var sample = new byte[20];
        sample[0] = 0x30;
        sample[3] = 0x01;
        sample[4] = 0x20;
        sample[5] = 0x00;
        sample[6] = 0x00;
        sample[7] = 0x00;

        var method = typeof(Program).GetMethod("ParseButtons", BindingFlags.NonPublic | BindingFlags.Static);
        var buttons = (ushort)method!.Invoke(null, new object[] { sample, sample.Length })!;

        Assert.Equal((ushort)0x0001, buttons & 0x0001);
        Assert.Equal((ushort)0x2000, buttons & 0x2000);
    }
}