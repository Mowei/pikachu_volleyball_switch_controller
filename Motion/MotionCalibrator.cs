namespace SwitchMotionBridge.Motion;

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

    private DateTime? _stillSince; // 目前這段連續靜止狀態的起始時間，變動時歸零
    private bool _autoCalibratedForCurrentStillness; // 避免同一段靜止期間重複觸發自動校正

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

        NotificationService.Notify("開始體感校正，請將手把靜置於水平桌面...");
    }

    // 套用目前的零點偏移量；若正在校正中則累積樣本，滿額後自動計算偏移量；未校正時偵測水平靜止並自動觸發校正
    public ((double x, double y, double z) accel, (double x, double y, double z) gyro) Apply(
        (double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        lock (_lock)
        {
            if (!_isCalibrating)
            {
                DetectStillnessAndAutoCalibrate(accel, gyro);
            }

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
                    NotificationService.Notify("體感校正完成");
                }
            }

            var calibratedAccel = (accel.x - _accelXOffset, accel.y - _accelYOffset, accel.z - _accelZOffset);
            var calibratedGyro = (gyro.x - _gyroXOffset, gyro.y - _gyroYOffset, gyro.z - _gyroZOffset);
            return (calibratedAccel, calibratedGyro);
        }
    }

    // 依原始（未校正）讀數判斷手把是否已水平靜置一段時間，符合條件時自動觸發一次校正
    private void DetectStillnessAndAutoCalibrate((double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        if (!AppConfig.AutoCalibrationEnabled)
        {
            _stillSince = null;
            return;
        }

        var accelMagnitude = Math.Sqrt(accel.x * accel.x + accel.y * accel.y + accel.z * accel.z);
        var gyroMagnitude = Math.Max(Math.Abs(gyro.x), Math.Max(Math.Abs(gyro.y), Math.Abs(gyro.z)));
        var isStill = Math.Abs(accelMagnitude - 1.0) <= AppConfig.StillAccelTolerance && gyroMagnitude <= AppConfig.StillGyroTolerance;

        if (!isStill)
        {
            _stillSince = null;
            _autoCalibratedForCurrentStillness = false;
            return;
        }

        _stillSince ??= DateTime.UtcNow;

        if (!_autoCalibratedForCurrentStillness && DateTime.UtcNow - _stillSince.Value >= AppConfig.StillDuration)
        {
            _autoCalibratedForCurrentStillness = true;
            StartCalibration(); // 與呼叫端同執行緒重入同一把鎖，C# lock 允許同執行緒重入
        }
    }
}
