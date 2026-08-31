namespace SwitchMotionBridge;

// 收集靜置狀態下的感測器讀數，計算零點偏移量以校正加速度計/陀螺儀的量測誤差。
internal sealed class MotionCalibrator
{
    private const int SampleCount = 50; // 校正時取樣的報告數量

    private readonly object _lock = new();
    private bool _isCalibrating;
    private int _collected;
    private double _accelXSum, _accelYSum, _accelZSum;
    private double _gyroXSum, _gyroYSum, _gyroZSum;

    private double _accelXOffset;
    private double _accelYOffset;
    private double _accelZOffset;
    private double _gyroXOffset;
    private double _gyroYOffset;
    private double _gyroZOffset;

    // 開始一次新的校正流程，期間手把應靜置於水平桌面
    public void StartCalibration()
    {
        lock (_lock)
        {
            _isCalibrating = true;
            _collected = 0;
            _accelXSum = _accelYSum = _accelZSum = 0;
            _gyroXSum = _gyroYSum = _gyroZSum = 0;
        }

        Console.WriteLine("開始體感校正，請將手把靜置於水平桌面...");
    }

    // 套用目前的零點偏移量；若正在校正中則累積樣本，滿額後自動計算偏移量
    public ((double x, double y, double z) accel, (double x, double y, double z) gyro) Apply(
        (double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        lock (_lock)
        {
            if (_isCalibrating)
            {
                _accelXSum += accel.x;
                _accelYSum += accel.y;
                _accelZSum += accel.z;
                _gyroXSum += gyro.x;
                _gyroYSum += gyro.y;
                _gyroZSum += gyro.z;
                _collected++;

                if (_collected >= SampleCount)
                {
                    _accelXOffset = _accelXSum / _collected;
                    _accelYOffset = _accelYSum / _collected;
                    _accelZOffset = _accelZSum / _collected;
                    _gyroXOffset = _gyroXSum / _collected;
                    _gyroYOffset = _gyroYSum / _collected;
                    _gyroZOffset = _gyroZSum / _collected;
                    _isCalibrating = false;
                    Console.WriteLine("體感校正完成");
                }
            }

            var calibratedAccel = (accel.x - _accelXOffset, accel.y - _accelYOffset, accel.z - _accelZOffset);
            var calibratedGyro = (gyro.x - _gyroXOffset, gyro.y - _gyroYOffset, gyro.z - _gyroZOffset);
            return (calibratedAccel, calibratedGyro);
        }
    }
}
