namespace SwitchMotionBridge;

// 將加速度計/陀螺儀資料轉換為方向鍵按住與跳躍/攻擊鍵採作。
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

    // 根據目前加速度/陀螺儀讀數比對門檻值，模擬鍵盤按下/抬起
    public void MapMotionToKeys((double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        // 依玩家模式選擇對應的方向鍵/動作鍵
        var jumpKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.UP_ARROW : VirtualKeyShort.KEY_R;
        var downKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.DOWN_ARROW : VirtualKeyShort.KEY_V;
        var leftKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.LEFT_ARROW : VirtualKeyShort.KEY_D;
        var rightKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RIGHT_ARROW : VirtualKeyShort.KEY_G;
        var hitKey = _mode == PlayerMode.SinglePlayer ? VirtualKeyShort.RETURN : VirtualKeyShort.KEY_Z;

        // 向上加速度超過門檻且已過冷卻時間，視為一次跳躍
        if (DateTime.UtcNow - _lastJump > AppConfig.MotionCooldown && accel.y > AppConfig.JumpThreshold)
        {
            KeyboardSender.SendKeyPress(jumpKey);
            _lastJump = DateTime.UtcNow;
        }

        // 任一軸向陀螺儀角速度超過門檻且已過冷卻時間，視為一次揮擊
        if (DateTime.UtcNow - _lastHit > AppConfig.MotionCooldown &&
            (Math.Abs(gyro.x) > AppConfig.HitThreshold || Math.Abs(gyro.y) > AppConfig.HitThreshold || Math.Abs(gyro.z) > AppConfig.HitThreshold))
        {
            KeyboardSender.SendKeyPress(hitKey);
            _lastHit = DateTime.UtcNow;
        }

        var moveRight = accel.x > AppConfig.MoveThreshold;
        var moveLeft = accel.x < -AppConfig.MoveThreshold;
        var moveDown = accel.y < AppConfig.DownThreshold;

        // 左右方向鍵採用互斥按住邏輯：切換方向前先鬆開另一邊
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
            // 回到當陣狀態，鬆開目前按住的方向鍵
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

        // 下蹲鍵獨立於左右移動，可同時按住
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
