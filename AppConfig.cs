namespace SwitchMotionBridge;

internal static class AppConfig
{
    public static readonly ushort NintendoVendorId = 0x057E;
    public static readonly ushort[] SupportedProductIds = { 0x2006, 0x2007, 0x2009, 0x2017 };
    public static readonly TimeSpan MotionCooldown = TimeSpan.FromMilliseconds(250);
    public static readonly double MoveThreshold = 0.6;
    public static readonly double JumpThreshold = 1.7;
    public static readonly double DownThreshold = -1.0;
    public static readonly double HitThreshold = 1800.0;
    public static bool MotionKeyMappingEnabled = false;

    public static PlayerMode DetermineMode()
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length > 1 && args[1].Equals("single", StringComparison.OrdinalIgnoreCase))
        {
            return PlayerMode.SinglePlayer;
        }

        return PlayerMode.LeftPlayer;
    }
}
