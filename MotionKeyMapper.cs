namespace SwitchMotionBridge;

// Translates accelerometer/gyroscope motion into directional key holds and jump/hit key presses.
internal sealed class MotionKeyMapper
{
    private readonly PlayerMode _mode;
    private DateTime _lastJump = DateTime.MinValue;
    private DateTime _lastHit = DateTime.MinValue;
    private bool _leftHeld;
    private bool _rightHeld;
    private bool _downHeld;

    public MotionKeyMapper(PlayerMode mode)
    {
        _mode = mode;
    }

    public void MapMotionToKeys((double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        var jumpKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.UP_ARROW : VirtualKeyShort.KEY_R;
        var downKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.DOWN_ARROW : VirtualKeyShort.KEY_V;
        var leftKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.LEFT_ARROW : VirtualKeyShort.KEY_D;
        var rightKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RIGHT_ARROW : VirtualKeyShort.KEY_G;
        var hitKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RETURN : VirtualKeyShort.KEY_Z;

        if (DateTime.UtcNow - _lastJump > AppConfig.MotionCooldown && accel.y > AppConfig.JumpThreshold)
        {
            KeyboardSender.SendKeyPress(jumpKey);
            _lastJump = DateTime.UtcNow;
        }

        if (DateTime.UtcNow - _lastHit > AppConfig.MotionCooldown &&
            (Math.Abs(gyro.x) > AppConfig.HitThreshold || Math.Abs(gyro.y) > AppConfig.HitThreshold || Math.Abs(gyro.z) > AppConfig.HitThreshold))
        {
            KeyboardSender.SendKeyPress(hitKey);
            _lastHit = DateTime.UtcNow;
        }

        var moveRight = accel.x > AppConfig.MoveThreshold;
        var moveLeft = accel.x < -AppConfig.MoveThreshold;
        var moveDown = accel.y < AppConfig.DownThreshold;

        if (moveRight && !_rightHeld)
        {
            if (_leftHeld)
            {
                KeyboardSender.SendKey(leftKey, false);
            }

            KeyboardSender.SendKey(rightKey, true);
            _rightHeld = true;
            _leftHeld = false;
        }
        else if (moveLeft && !_leftHeld)
        {
            if (_rightHeld)
            {
                KeyboardSender.SendKey(rightKey, false);
            }

            KeyboardSender.SendKey(leftKey, true);
            _leftHeld = true;
            _rightHeld = false;
        }
        else if (!moveRight && !moveLeft)
        {
            if (_rightHeld)
            {
                KeyboardSender.SendKey(rightKey, false);
                _rightHeld = false;
            }
            if (_leftHeld)
            {
                KeyboardSender.SendKey(leftKey, false);
                _leftHeld = false;
            }
        }

        if (moveDown && !_downHeld)
        {
            KeyboardSender.SendKey(downKey, true);
            _downHeld = true;
        }
        else if (!moveDown && _downHeld)
        {
            KeyboardSender.SendKey(downKey, false);
            _downHeld = false;
        }
    }
}
