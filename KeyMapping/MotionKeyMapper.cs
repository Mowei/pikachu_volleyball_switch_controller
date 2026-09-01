using SwitchMotionBridge.Config;
using SwitchMotionBridge.Enums;

namespace SwitchMotionBridge.KeyMapping;

// 將加速度計/陀螺儀資料轉換為方向鍵按住與跳躍/攻擊鍵採作。
internal sealed class MotionKeyMapper
{
    private readonly PlayerMode _mode;
    private VirtualKeyShort _jumpKey;
    private VirtualKeyShort _downKey;
    private VirtualKeyShort _leftKey;
    private VirtualKeyShort _rightKey;
    private VirtualKeyShort _hitKey;
    private DateTime _lastJump = DateTime.MinValue;
    private DateTime _lastHit = DateTime.MinValue;
    private bool _leftHeld;
    private bool _rightHeld;
    private bool _downHeld;

    public MotionKeyMapper(PlayerMode mode)
    {
        _mode = mode;
        LoadBindings();
    }

    // 重新讀取 keybindings.json 並套用最新的按鍵綁定，供設定檔熱重載使用
    public void ReloadBindings() => LoadBindings();

    private void LoadBindings()
    {
        var bindings = KeyBindings.Load();
        var set = _mode switch
        {
            PlayerMode.SinglePlayer => bindings.SinglePlayer,
            PlayerMode.RightPlayer => bindings.RightPlayer,
            PlayerMode.DualPlayer => bindings.LeftPlayer,
            _ => bindings.LeftPlayer
        };

        _jumpKey = KeyBindings.Parse(set.Jump, _mode == PlayerMode.RightPlayer ? VirtualKeyShort.KEY_I : VirtualKeyShort.KEY_R);
        _downKey = KeyBindings.Parse(set.Down, _mode == PlayerMode.RightPlayer ? VirtualKeyShort.KEY_K : VirtualKeyShort.KEY_V);
        _leftKey = KeyBindings.Parse(set.Left, _mode == PlayerMode.RightPlayer ? VirtualKeyShort.KEY_J : VirtualKeyShort.KEY_D);
        _rightKey = KeyBindings.Parse(set.Right, _mode == PlayerMode.RightPlayer ? VirtualKeyShort.KEY_L : VirtualKeyShort.KEY_G);
        _hitKey = KeyBindings.Parse(set.Hit, _mode == PlayerMode.RightPlayer ? VirtualKeyShort.KEY_O : VirtualKeyShort.KEY_Z);
    }


    // 根據目前加速度/陀螺儀讀數比對門檻值，模擬鍵盤按下/抬起
    public void MapMotionToKeys((double x, double y, double z) accel, (double x, double y, double z) gyro)
    {
        // 向上加速度超過門檻且已過冷卻時間，視為一次跳躍
        if (DateTime.UtcNow - _lastJump > AppConfig.MotionCooldown && accel.y > AppConfig.JumpThreshold)
        {
            KeyboardSender.SendKeyPress(_jumpKey);
            _lastJump = DateTime.UtcNow;
        }

        // 任一軸向陀螺儀角速度超過門檻且已過冷卻時間，視為一次揮擊
        if (DateTime.UtcNow - _lastHit > AppConfig.MotionCooldown &&
            (Math.Abs(gyro.x) > AppConfig.HitThreshold || Math.Abs(gyro.y) > AppConfig.HitThreshold || Math.Abs(gyro.z) > AppConfig.HitThreshold))
        {
            KeyboardSender.SendKeyPress(_hitKey);
            _lastHit = DateTime.UtcNow;
        }

        var moveRight = _rightHeld ? accel.x > AppConfig.MoveReleaseThreshold : accel.x > AppConfig.MoveThreshold;
        var moveLeft = _leftHeld ? accel.x < -AppConfig.MoveReleaseThreshold : accel.x < -AppConfig.MoveThreshold;
        var moveDown = _downHeld ? accel.y < AppConfig.DownReleaseThreshold : accel.y < AppConfig.DownThreshold;

        // 左右方向鍵採用互斥按住邏輯：切換方向前先鬆開另一邊
        if (moveRight && !_rightHeld)
        {
            if (_leftHeld)
            {
                KeyboardSender.SendKey(_leftKey, false);
            }

            KeyboardSender.SendKey(_rightKey, true);
            _rightHeld = true;
            _leftHeld = false;
        }
        else if (moveLeft && !_leftHeld)
        {
            if (_rightHeld)
            {
                KeyboardSender.SendKey(_rightKey, false);
            }

            KeyboardSender.SendKey(_leftKey, true);
            _leftHeld = true;
            _rightHeld = false;
        }
        else if (!moveRight && !moveLeft)
        {
            // 回到當陣狀態，鬆開目前按住的方向鍵
            if (_rightHeld)
            {
                KeyboardSender.SendKey(_rightKey, false);
                _rightHeld = false;
            }
            if (_leftHeld)
            {
                KeyboardSender.SendKey(_leftKey, false);
                _leftHeld = false;
            }
        }

        // 下蹲鍵獨立於左右移動，可同時按住
        if (moveDown && !_downHeld)
        {
            KeyboardSender.SendKey(_downKey, true);
            _downHeld = true;
        }
        else if (!moveDown && _downHeld)
        {
            KeyboardSender.SendKey(_downKey, false);
            _downHeld = false;
        }
    }
}
