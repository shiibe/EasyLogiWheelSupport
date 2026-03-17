using System;
using System.Text;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace EasyLogiWheelSupport
{
    public partial class Plugin
    {
        private static ManualLogSource _log;

        private static ConfigEntry<bool> _enableMod;
        private static ConfigEntry<bool> _debugLogging;
        private static ConfigEntry<bool> _logDetectedDevices;

        private static ConfigEntry<bool> _desktopMenuIconVisible;
        private static ConfigEntry<string> _desktopMenuIconX;
        private static ConfigEntry<string> _desktopMenuIconY;

        private static ConfigEntry<bool> _ignoreXInputControllers;

        internal const string PrefKeyFfbEnabled = "G920FfbEnabled";
        internal const string PrefKeyFfbOverall = "G920FfbOverall";
        internal const string PrefKeyFfbSpring = "G920FfbSpring";
        internal const string PrefKeyFfbDamper = "G920FfbDamper";
        internal const string PrefKeyWheelRange = "G920WheelRange";

        internal const string PrefKeyCalSteerCenter = "G920Cal_SteerCenter";
        internal const string PrefKeyCalSteerLeft = "G920Cal_SteerLeft";
        internal const string PrefKeyCalSteerRight = "G920Cal_SteerRight";

        internal const string PrefKeyCalThrottleReleased = "G920Cal_ThrottleReleased";
        internal const string PrefKeyCalThrottlePressed = "G920Cal_ThrottlePressed";
        internal const string PrefKeyCalBrakeReleased = "G920Cal_BrakeReleased";
        internal const string PrefKeyCalBrakePressed = "G920Cal_BrakePressed";

        internal const string PrefKeyCalClutchReleased = "G920Cal_ClutchReleased";
        internal const string PrefKeyCalClutchPressed = "G920Cal_ClutchPressed";

        internal enum PedalKind
        {
            Throttle = 0,
            Brake = 1,
            Clutch = 2
        }

        internal enum AxisId
        {
            lX = 0,
            lY = 1,
            lZ = 2,
            lRx = 3,
            lRy = 4,
            lRz = 5,
            slider0 = 6,
            slider1 = 7
        }

        internal const string PrefKeySteeringGain = "G920SteerGain";
        internal const string PrefKeySteeringDeadzone = "G920SteerDeadzone";
        internal const string PrefKeySteeringAxis = "G920Axis_Steer";
        internal const string PrefKeyThrottleAxis = "G920Axis_Throttle";
        internal const string PrefKeyBrakeAxis = "G920Axis_Brake";
        internal const string PrefKeyClutchAxis = "G920Axis_Clutch";

        internal enum ButtonBindAction
        {
            InteractOk = 0,
            Back = 1,
            MapItems = 2,
            Pause = 3,
            Camera = 5,
            ResetVehicle = 6,
            Headlights = 7,

            Horn = 8,

            RadioPower = 9,
            RadioScanToggle = 10,
            RadioScanLeft = 11,
            RadioScanRight = 12
        }

        internal enum BindingLayer
        {
            Normal = 0,
            Modified = 1
        }

        internal enum BindingKind
        {
            None = 0,
            Button = 1,
            Pov = 2
        }

        internal struct BindingInput
        {
            public BindingKind Kind;
            public int Code;
        }

        private const int BindingPovOffset = 1000;
        private const string PrefKeyBindModifier = "G920Bind_Modifier";

        private static string GetBindPrefKey(BindingLayer layer, ButtonBindAction action)
        {
            // Stable key: do not depend on enum ToString().
            return "G920Bind_" + (int)layer + "_" + (int)action;
        }

        private static string GetLegacyBindPrefKey(BindingLayer layer, ButtonBindAction action)
        {
            // v0.1 pre-release used enum names.
            return "G920Bind_" + layer + "_" + action;
        }

        internal static BindingInput GetBinding(BindingLayer layer, ButtonBindAction action)
        {
            string key = GetBindPrefKey(layer, action);
            int raw = PlayerPrefs.GetInt(key, -1);
            if (raw < 0)
            {
                // Back-compat with earlier key format.
                raw = PlayerPrefs.GetInt(GetLegacyBindPrefKey(layer, action), -1);
            }
            if (raw < 0)
            {
                return new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (raw >= BindingPovOffset)
            {
                return new BindingInput { Kind = BindingKind.Pov, Code = Mathf.Clamp(raw - BindingPovOffset, 0, 3) };
            }
            return new BindingInput { Kind = BindingKind.Button, Code = Mathf.Clamp(raw, 0, 127) };
        }

        internal static void SetBinding(BindingLayer layer, ButtonBindAction action, BindingInput input)
        {
            int raw;
            if (input.Kind == BindingKind.Button)
            {
                raw = Mathf.Clamp(input.Code, 0, 127);
            }
            else if (input.Kind == BindingKind.Pov)
            {
                raw = BindingPovOffset + Mathf.Clamp(input.Code, 0, 3);
            }
            else
            {
                raw = -1;
            }

            PlayerPrefs.SetInt(GetBindPrefKey(layer, action), raw);
        }

        internal static bool TryGetPovDir(out int dir)
        {
            dir = -1;
            if (!TryGetCachedWheelState(out var state))
            {
                return false;
            }

            uint pov = 0xFFFFFFFF;
            if (state.rgdwPOV != null && state.rgdwPOV.Length > 0)
            {
                pov = state.rgdwPOV[0];
            }
            dir = PovToDir(pov);
            return true;
        }

        internal static bool TryGetPov8Vector(out Vector2 v)
        {
            v = Vector2.zero;
            if (!TryGetCachedWheelState(out var state))
            {
                return false;
            }

            uint pov = 0xFFFFFFFF;
            if (state.rgdwPOV != null && state.rgdwPOV.Length > 0)
            {
                pov = state.rgdwPOV[0];
            }

            if (pov == 0xFFFFFFFF)
            {
                return true;
            }

            int angle = (int)(pov % 36000u);
            int dir8 = Mathf.RoundToInt(angle / 4500f) % 8;

            // POV convention: 0=Up, 90=Right, 180=Down, 270=Left.
            // Match the prior 4-dir mapping (up => y=-1) then invert in caller if needed.
            switch (dir8)
            {
                case 0:
                    v = new Vector2(0f, -1f);
                    break;
                case 1:
                    v = new Vector2(-1f, -1f);
                    break;
                case 2:
                    v = new Vector2(-1f, 0f);
                    break;
                case 3:
                    v = new Vector2(-1f, 1f);
                    break;
                case 4:
                    v = new Vector2(0f, 1f);
                    break;
                case 5:
                    v = new Vector2(1f, 1f);
                    break;
                case 6:
                    v = new Vector2(1f, 0f);
                    break;
                default:
                    v = new Vector2(1f, -1f);
                    break;
            }

            return true;
        }

        internal static BindingInput GetModifierBinding()
        {
            int raw = PlayerPrefs.GetInt(PrefKeyBindModifier, -1);
            if (raw < 0)
            {
                return new BindingInput { Kind = BindingKind.None, Code = 0 };
            }
            if (raw >= BindingPovOffset)
            {
                return new BindingInput { Kind = BindingKind.Pov, Code = Mathf.Clamp(raw - BindingPovOffset, 0, 3) };
            }
            return new BindingInput { Kind = BindingKind.Button, Code = Mathf.Clamp(raw, 0, 127) };
        }

        internal static void SetModifierBinding(BindingInput input)
        {
            int raw;
            if (input.Kind == BindingKind.Button)
            {
                raw = Mathf.Clamp(input.Code, 0, 127);
            }
            else if (input.Kind == BindingKind.Pov)
            {
                raw = BindingPovOffset + Mathf.Clamp(input.Code, 0, 3);
            }
            else
            {
                raw = -1;
            }

            PlayerPrefs.SetInt(PrefKeyBindModifier, raw);
        }

        internal static string GetBindingLabel(BindingInput input)
        {
            if (input.Kind == BindingKind.Button)
            {
                return "But. " + (Mathf.Clamp(input.Code, 0, 127) + 1);
            }
            if (input.Kind == BindingKind.Pov)
            {
                switch (Mathf.Clamp(input.Code, 0, 3))
                {
                    case 0:
                        return "DP Up";
                    case 1:
                        return "DP Right";
                    case 2:
                        return "DP Down";
                    default:
                        return "DP Left";
                }
            }
            return "None";
        }

        internal static string GetChordLabel(BindingInput input, bool modified)
        {
            string baseLabel = GetBindingLabel(input);
            if (!modified || input.Kind == BindingKind.None)
            {
                return baseLabel;
            }
            return "M+" + baseLabel;
        }

        internal static string GetActionLabel(ButtonBindAction action)
        {
            switch (action)
            {
                case ButtonBindAction.InteractOk:
                    return "Interact";
                case ButtonBindAction.Back:
                    return "Back";
                case ButtonBindAction.MapItems:
                    return "Map/Items";
                case ButtonBindAction.Pause:
                    return "Pause";
                case ButtonBindAction.Camera:
                    return "Camera";
                case ButtonBindAction.ResetVehicle:
                    return "Reset";
                case ButtonBindAction.Headlights:
                    return "Lights";
                case ButtonBindAction.Horn:
                    return "Horn";
                case ButtonBindAction.RadioPower:
                    return "Radio Pwr";
                case ButtonBindAction.RadioScanToggle:
                    return "Scan";
                case ButtonBindAction.RadioScanLeft:
                    return "Prev Ch";
                case ButtonBindAction.RadioScanRight:
                    return "Next Ch";
                default:
                    return action.ToString();
            }
        }

        private static int _wheelInputCacheFrame = -1;
        private static bool _wheelInputCacheValid;
        private static LogitechGSDK.DIJOYSTATE2ENGINES _wheelInputCacheState;
        private static bool[] _wheelButtonsDown;
        private static bool[] _wheelButtonsPressed;
        private static bool[] _wheelButtonsReleased;
        private static bool[] _wheelPovDown;
        private static bool[] _wheelPovPressed;
        private static bool[] _wheelPovReleased;
        private static int _wheelPovDirDown = -1;

        private static void EnsureWheelInputArrays()
        {
            _wheelButtonsDown ??= new bool[128];
            _wheelButtonsPressed ??= new bool[128];
            _wheelButtonsReleased ??= new bool[128];
            _wheelPovDown ??= new bool[4];
            _wheelPovPressed ??= new bool[4];
            _wheelPovReleased ??= new bool[4];
        }

        private static int PovToDir(uint pov)
        {
            // 0xFFFFFFFF means centered.
            if (pov == 0xFFFFFFFF)
            {
                return -1;
            }

            // POV uses hundredths of degrees (0..35999). Snap to nearest cardinal.
            int angle = (int)(pov % 36000u);
            int dir = ((angle + 4500) / 9000) % 4;
            // 0=Up, 1=Right, 2=Down, 3=Left
            return dir;
        }

        private static void UpdateWheelInputCache()
        {
            EnsureWheelInputArrays();

            int frame = Time.frameCount;
            if (frame == _wheelInputCacheFrame)
            {
                return;
            }
            _wheelInputCacheFrame = frame;

            Array.Clear(_wheelButtonsPressed, 0, _wheelButtonsPressed.Length);
            Array.Clear(_wheelButtonsReleased, 0, _wheelButtonsReleased.Length);
            Array.Clear(_wheelPovPressed, 0, _wheelPovPressed.Length);
            Array.Clear(_wheelPovReleased, 0, _wheelPovReleased.Length);

            _wheelInputCacheValid = TryGetLogiState(out var state);
            if (!_wheelInputCacheValid)
            {
                return;
            }

            _wheelInputCacheState = state;

            if (state.rgbButtons != null)
            {
                int count = Math.Min(state.rgbButtons.Length, _wheelButtonsDown.Length);
                for (int i = 0; i < count; i++)
                {
                    bool downNow = state.rgbButtons[i] >= 128;
                    if (downNow && !_wheelButtonsDown[i])
                    {
                        _wheelButtonsPressed[i] = true;
                    }
                    else if (!downNow && _wheelButtonsDown[i])
                    {
                        _wheelButtonsReleased[i] = true;
                    }
                    _wheelButtonsDown[i] = downNow;
                }
            }

            int povDir = -1;
            if (state.rgdwPOV != null && state.rgdwPOV.Length > 0)
            {
                povDir = PovToDir(state.rgdwPOV[0]);
            }

            if (povDir != _wheelPovDirDown)
            {
                if (_wheelPovDirDown >= 0)
                {
                    _wheelPovReleased[_wheelPovDirDown] = true;
                }
                if (povDir >= 0)
                {
                    _wheelPovPressed[povDir] = true;
                }
            }

            _wheelPovDirDown = povDir;
            for (int i = 0; i < 4; i++)
            {
                _wheelPovDown[i] = (povDir == i);
            }
        }

        internal static bool TryGetCachedWheelState(out LogitechGSDK.DIJOYSTATE2ENGINES state)
        {
            UpdateWheelInputCache();
            state = _wheelInputCacheState;
            return _wheelInputCacheValid;
        }

        internal static bool TryCaptureNextBinding(out BindingInput input)
        {
            input = default;
            UpdateWheelInputCache();
            if (!_wheelInputCacheValid)
            {
                return false;
            }

            for (int i = 0; i < _wheelButtonsPressed.Length; i++)
            {
                if (_wheelButtonsPressed[i])
                {
                    input = new BindingInput { Kind = BindingKind.Button, Code = i };
                    return true;
                }
            }

            for (int d = 0; d < _wheelPovPressed.Length; d++)
            {
                if (_wheelPovPressed[d])
                {
                    input = new BindingInput { Kind = BindingKind.Pov, Code = d };
                    return true;
                }
            }

            return false;
        }

        internal static bool IsBindingDownForCurrentFrame(BindingInput input)
        {
            UpdateWheelInputCache();
            if (!_wheelInputCacheValid)
            {
                return false;
            }
            return IsBindingDown(input);
        }

        internal static bool IsBindingPressedThisFrameForCurrentFrame(BindingInput input)
        {
            UpdateWheelInputCache();
            if (!_wheelInputCacheValid)
            {
                return false;
            }
            return IsBindingPressedThisFrame(input);
        }

        internal static bool IsBindingReleasedThisFrameForCurrentFrame(BindingInput input)
        {
            UpdateWheelInputCache();
            if (!_wheelInputCacheValid)
            {
                return false;
            }
            return IsBindingReleasedThisFrame(input);
        }

        private static bool IsBindingDown(BindingInput input)
        {
            if (input.Kind == BindingKind.Button)
            {
                int i = Mathf.Clamp(input.Code, 0, 127);
                return _wheelButtonsDown != null && i < _wheelButtonsDown.Length && _wheelButtonsDown[i];
            }
            if (input.Kind == BindingKind.Pov)
            {
                int d = Mathf.Clamp(input.Code, 0, 3);
                return _wheelPovDown != null && d < _wheelPovDown.Length && _wheelPovDown[d];
            }
            return false;
        }

        private static bool IsBindingPressedThisFrame(BindingInput input)
        {
            if (input.Kind == BindingKind.Button)
            {
                int i = Mathf.Clamp(input.Code, 0, 127);
                return _wheelButtonsPressed != null && i < _wheelButtonsPressed.Length && _wheelButtonsPressed[i];
            }
            if (input.Kind == BindingKind.Pov)
            {
                int d = Mathf.Clamp(input.Code, 0, 3);
                return _wheelPovPressed != null && d < _wheelPovPressed.Length && _wheelPovPressed[d];
            }
            return false;
        }

        private static bool IsBindingReleasedThisFrame(BindingInput input)
        {
            if (input.Kind == BindingKind.Button)
            {
                int i = Mathf.Clamp(input.Code, 0, 127);
                return _wheelButtonsReleased != null && i < _wheelButtonsReleased.Length && _wheelButtonsReleased[i];
            }
            if (input.Kind == BindingKind.Pov)
            {
                int d = Mathf.Clamp(input.Code, 0, 3);
                return _wheelPovReleased != null && d < _wheelPovReleased.Length && _wheelPovReleased[d];
            }
            return false;
        }

        private static bool _logiInitAttempted;
        private static bool _logiAvailable;
        private static bool _logiConnected;
        private static int _logiIndex;

        private static int _logiInitAttemptCount;
        private static float _logiNextInitAttemptTime;
        private static bool _logiIgnoreXInputUsed;

        private static bool _logiWasConnected;
        private static string _logiLastName;
        private static string _logiLastPath;

        private static bool _isInWalkingMode;
        private static float _currentSpeedKmh;
        private static bool _isOffRoad;
        private static bool _isSliding;

        private static int _wheelLastUpdateFrame;
        private static float _wheelLastUpdateTime;
        private static float _wheelLastSteer;
        private static float _wheelLastAccel;

        private static float _wheelMenuHeartbeatTime;
        private static float _ffbPageHeartbeatTime;
        private static float _bindingCaptureHeartbeatTime;

        private static bool ShouldApply()
        {
            return _enableMod != null && _enableMod.Value;
        }

        internal static bool GetFfbEnabled()
        {
            return PlayerPrefs.GetInt(PrefKeyFfbEnabled, 1) != 0;
        }

        internal static void SetFfbEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(PrefKeyFfbEnabled, enabled ? 1 : 0);
            if (!enabled)
            {
                StopAllForces();
            }
            ApplyControllerPropertiesIfReady();
        }

        internal static float GetFfbOverallGain()
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyFfbOverall, 0.55f));
        }

        internal static void SetFfbOverallGain(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyFfbOverall, value);
            ApplyControllerPropertiesIfReady();
        }

        internal static float GetFfbSpringGain()
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyFfbSpring, 0.60f));
        }

        internal static void SetFfbSpringGain(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyFfbSpring, value);
            ApplyControllerPropertiesIfReady();
        }

        internal static float GetFfbDamperGain()
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyFfbDamper, 0.20f));
        }

        internal static void SetFfbDamperGain(float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefKeyFfbDamper, value);
            ApplyControllerPropertiesIfReady();
        }

        internal static int GetWheelRange()
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyWheelRange, 270), 180, 900);
        }

        internal static void SetWheelRange(int degrees)
        {
            degrees = Mathf.Clamp(degrees, 180, 900);
            PlayerPrefs.SetInt(PrefKeyWheelRange, degrees);
            ApplyWheelRangeIfReady();
        }

        internal static float GetSteeringGain()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeySteeringGain, 1.0f), 0.5f, 3.0f);
        }

        internal static void SetSteeringGain(float value)
        {
            value = Mathf.Clamp(value, 0.5f, 3.0f);
            PlayerPrefs.SetFloat(PrefKeySteeringGain, value);
        }

        internal static void ResetFfbDefaults()
        {
            PlayerPrefs.SetInt(PrefKeyFfbEnabled, 1);
            PlayerPrefs.SetFloat(PrefKeyFfbOverall, 0.55f);
            PlayerPrefs.SetFloat(PrefKeyFfbSpring, 0.60f);
            PlayerPrefs.SetFloat(PrefKeyFfbDamper, 0.20f);

            StopAllForces();
            ApplyControllerPropertiesIfReady();
        }

        internal static void ResetSteeringDefaults()
        {
            PlayerPrefs.SetInt(PrefKeyWheelRange, 270);
            PlayerPrefs.SetFloat(PrefKeySteeringGain, 1.0f);
            PlayerPrefs.SetFloat(PrefKeySteeringDeadzone, 0.01f);

            ApplyWheelRangeIfReady();
        }

        internal static float GetSteeringDeadzone()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeySteeringDeadzone, 0.01f), 0f, 0.12f);
        }

        internal static void SetSteeringDeadzone(float value)
        {
            value = Mathf.Clamp(value, 0f, 0.12f);
            PlayerPrefs.SetFloat(PrefKeySteeringDeadzone, value);
        }

        internal static AxisId GetSteeringAxis()
        {
            return (AxisId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeySteeringAxis, (int)AxisId.lX), 0, (int)AxisId.slider1);
        }

        internal static void SetSteeringAxis(AxisId axis)
        {
            PlayerPrefs.SetInt(PrefKeySteeringAxis, (int)axis);
            PlayerPrefs.DeleteKey(PrefKeyCalSteerCenter);
            PlayerPrefs.DeleteKey(PrefKeyCalSteerLeft);
            PlayerPrefs.DeleteKey(PrefKeyCalSteerRight);
        }

        internal static AxisId GetThrottleAxis()
        {
            // G920 commonly reports throttle on lY.
            return (AxisId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyThrottleAxis, (int)AxisId.lY), 0, (int)AxisId.slider1);
        }

        internal static AxisId GetBrakeAxis()
        {
            return (AxisId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyBrakeAxis, (int)AxisId.lRz), 0, (int)AxisId.slider1);
        }

        internal static AxisId GetClutchAxis()
        {
            // Common for clutch to appear on lRy, but depends on driver settings.
            return (AxisId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyClutchAxis, (int)AxisId.lRy), 0, (int)AxisId.slider1);
        }

        internal static void SetThrottleAxis(AxisId axis)
        {
            PlayerPrefs.SetInt(PrefKeyThrottleAxis, (int)axis);
            PlayerPrefs.DeleteKey(PrefKeyCalThrottleReleased);
            PlayerPrefs.DeleteKey(PrefKeyCalThrottlePressed);
        }

        internal static void SetBrakeAxis(AxisId axis)
        {
            PlayerPrefs.SetInt(PrefKeyBrakeAxis, (int)axis);
            PlayerPrefs.DeleteKey(PrefKeyCalBrakeReleased);
            PlayerPrefs.DeleteKey(PrefKeyCalBrakePressed);
        }

        internal static void SetClutchAxis(AxisId axis)
        {
            PlayerPrefs.SetInt(PrefKeyClutchAxis, (int)axis);
            PlayerPrefs.DeleteKey(PrefKeyCalClutchReleased);
            PlayerPrefs.DeleteKey(PrefKeyCalClutchPressed);
        }

        private static void DetectWheelOnce()
        {
            if (!ShouldApply())
            {
                return;
            }

            string[] names;
            try
            {
                names = Input.GetJoystickNames();
            }
            catch (Exception e)
            {
                _log.LogWarning($"Failed to query joystick names: {e.GetType().Name}: {e.Message}");
                return;
            }

            if (names == null || names.Length == 0)
            {
                LogDebug("No joysticks detected by Unity.");
                return;
            }

            if (_logDetectedDevices != null && _logDetectedDevices.Value)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    _log.LogInfo($"Joystick {i + 1}: '{names[i]}'");
                }
            }
        }

        private static void TryInitLogitech()
        {
            if (_logiAvailable)
            {
                return;
            }

            if (_logiInitAttempted && Time.unscaledTime < _logiNextInitAttemptTime)
            {
                return;
            }

            _logiInitAttempted = true;
            _logiInitAttemptCount++;

            try
            {
                _logiIndex = 0;
                bool preferredIgnoreXInput = _ignoreXInputControllers == null || _ignoreXInputControllers.Value;

                if (TryInitLogitechInternal(preferredIgnoreXInput))
                {
                    return;
                }

                // Fallback: some setups enumerate the wheel through XInput.
                if (TryInitLogitechInternal(!preferredIgnoreXInput))
                {
                    _log.LogInfo($"Logitech SDK initialized with ignoreXInputControllers={!preferredIgnoreXInput} fallback.");
                    return;
                }

                // Throttle log spam; keep retrying quietly.
                if (_logiInitAttemptCount == 1)
                {
                    _log.LogInfo("Logitech SDK not ready yet; retrying...");
                    _log.LogInfo("If this never initializes, make sure Logitech G HUB/LGS is installed and the wheel is connected/powered.");
                }
                else if (_logiInitAttemptCount == 5)
                {
                    _log.LogWarning("Still retrying Logitech SDK init (wheel/G HUB may still be starting). You can also use 'Retry SDK' in wheel.exe.");
                }
                else
                {
                    LogDebug($"Logitech SDK init still not ready (attempt {_logiInitAttemptCount}).");
                }

                _logiNextInitAttemptTime = Time.unscaledTime + 3.0f;
            }
            catch (DllNotFoundException e)
            {
                _logiAvailable = false;
                _log.LogWarning("Logitech SDK DLL not found (LogitechSteeringWheelEnginesWrapper.dll). FFB/input disabled.");
                LogDebug(e.ToString());
                _logiNextInitAttemptTime = Time.unscaledTime + 10.0f;
            }
            catch (BadImageFormatException e)
            {
                _logiAvailable = false;
                _log.LogWarning("Logitech SDK DLL is wrong architecture. FFB/input disabled.");
                LogDebug(e.ToString());
                _logiNextInitAttemptTime = Time.unscaledTime + 10.0f;
            }
            catch (Exception e)
            {
                _logiAvailable = false;
                _log.LogWarning($"Logitech SDK init threw {e.GetType().Name}. FFB/input disabled.");
                LogDebug(e.ToString());
                _logiNextInitAttemptTime = Time.unscaledTime + 10.0f;
            }
        }

        private static bool TryInitLogitechInternal(bool ignoreXInputControllers)
        {
            bool ok = LogitechGSDK.LogiSteeringInitialize(ignoreXInputControllers);
            if (!ok)
            {
                return false;
            }

            _logiIgnoreXInputUsed = ignoreXInputControllers;
            _logiAvailable = true;
            _log.LogMessage($"Logitech SDK initialized (ignoreXInputControllers={ignoreXInputControllers}).");
            ApplyControllerPropertiesIfReady();
            ApplyWheelRangeIfReady();
            return true;
        }

        internal static void ForceReinitLogitech()
        {
            ForceReinitLogitech(false);
        }

        internal static void ForceReinitLogitech(bool forceReconnect)
        {
            // Users hit this button to recover from "Retrying" or "No wheel".
            // If forceReconnect is true, we also tear down when connected.
            try
            {
                if (_logiAvailable)
                {
                    SafeLogiUpdate();
                    _logiConnected = LogitechGSDK.LogiIsConnected(_logiIndex);
                }
            }
            catch
            {
            }

            if (!forceReconnect && _logiAvailable && _logiConnected)
            {
                ApplyControllerPropertiesIfReady();
                ApplyWheelRangeIfReady();
                return;
            }

            if (forceReconnect && _log != null)
            {
                _log.LogInfo("Forcing Logitech SDK reconnect...");
            }

            _logiAvailable = false;
            _logiConnected = false;
            _logiInitAttempted = false;
            _logiInitAttemptCount = 0;
            _logiNextInitAttemptTime = 0f;

            try
            {
                StopAllForces();
                LogitechGSDK.LogiSteeringShutdown();
            }
            catch
            {
            }

            _logiWasConnected = false;
            _logiLastName = null;
            _logiLastPath = null;

            // Attempt immediately.
            TryInitLogitech();
        }

        internal static string GetLogitechStatus()
        {
            if (!_logiInitAttempted)
            {
                return "Not initialized";
            }
            if (!_logiAvailable)
            {
                return "Retrying";
            }
            return _logiConnected ? "Connected" : "No wheel";
        }

        private static void MaybeLogWheelDetected()
        {
            if (_log == null)
            {
                return;
            }

            if (!_logiConnected)
            {
                _logiWasConnected = false;
                return;
            }

            string name = TryGetWheelFriendlyName(_logiIndex);
            string path = TryGetWheelDevicePath(_logiIndex);
            bool shouldLog = !_logiWasConnected
                             || (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, _logiLastName, StringComparison.Ordinal))
                             || (!string.IsNullOrWhiteSpace(path) && !string.Equals(path, _logiLastPath, StringComparison.Ordinal));

            _logiWasConnected = true;
            if (!string.IsNullOrWhiteSpace(name))
            {
                _logiLastName = name;
            }
            if (!string.IsNullOrWhiteSpace(path))
            {
                _logiLastPath = path;
            }

            if (!shouldLog)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
            {
                _log.LogMessage($"Wheel detected: {name} ({path})");
                return;
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                _log.LogMessage($"Wheel detected: {name}");
                return;
            }
            if (!string.IsNullOrWhiteSpace(path))
            {
                _log.LogMessage($"Wheel detected: {path}");
                return;
            }

            try
            {
                if (LogitechGSDK.LogiIsModelConnected(_logiIndex, LogitechGSDK.LOGI_MODEL_G920))
                {
                    _log.LogMessage("Wheel detected: Logitech G920");
                }
                else if (LogitechGSDK.LogiIsModelConnected(_logiIndex, LogitechGSDK.LOGI_MODEL_G29))
                {
                    _log.LogMessage("Wheel detected: Logitech G29");
                }
                else
                {
                    _log.LogMessage("Wheel detected.");
                }
            }
            catch
            {
                _log.LogMessage("Wheel detected.");
            }
        }

        private static string TryGetWheelFriendlyName(int index)
        {
            try
            {
                var sb = new StringBuilder(256);
                if (LogitechGSDK.LogiGetFriendlyProductName(index, sb, sb.Capacity))
                {
                    string s = sb.ToString();
                    return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
                }
            }
            catch
            {
            }
            return null;
        }

        private static string TryGetWheelDevicePath(int index)
        {
            try
            {
                var sb = new StringBuilder(512);
                if (LogitechGSDK.LogiGetDevicePath(index, sb, sb.Capacity))
                {
                    string s = sb.ToString();
                    return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
                }
            }
            catch
            {
            }
            return null;
        }

        internal static void SetWheelLastInput(float steer, float accel)
        {
            _wheelLastUpdateFrame = Time.frameCount;
            _wheelLastUpdateTime = Time.unscaledTime;
            _wheelLastSteer = Mathf.Clamp(steer, -1f, 1f);
            _wheelLastAccel = Mathf.Clamp(accel, -1f, 1f);
        }

        internal static bool TryGetWheelLastInput(out float steer, out float accel)
        {
            steer = 0f;
            accel = 0f;

            if (Time.frameCount != _wheelLastUpdateFrame)
            {
                return false;
            }

            steer = _wheelLastSteer;
            accel = _wheelLastAccel;
            return true;
        }

        internal static bool TryGetWheelLastInputRecent(float maxAgeSeconds, out float steer, out float accel)
        {
            steer = 0f;
            accel = 0f;

            if (Time.unscaledTime - _wheelLastUpdateTime > maxAgeSeconds)
            {
                return false;
            }

            steer = _wheelLastSteer;
            accel = _wheelLastAccel;
            return true;
        }

        internal static void SetWheelMenuActive(bool active)
        {
            if (active)
            {
                _wheelMenuHeartbeatTime = Time.unscaledTime;
            }
        }

        internal static void SetBindingCaptureActive(bool active)
        {
            if (active)
            {
                _bindingCaptureHeartbeatTime = Time.unscaledTime;
            }
        }

        internal static void SetFfbPageActive(bool active)
        {
            if (active)
            {
                _ffbPageHeartbeatTime = Time.unscaledTime;
                // Force-enable so slider tweaks can be felt even if config says "off".
                ApplyControllerPropertiesIfReady(forceEnableOverride: true);
            }
            else
            {
                ApplyControllerPropertiesIfReady(forceEnableOverride: null);
            }
        }

        private static bool IsWheelMenuActive()
        {
            return Time.unscaledTime - _wheelMenuHeartbeatTime < 0.5f;
        }

        private static bool IsFfbPageActive()
        {
            return Time.unscaledTime - _ffbPageHeartbeatTime < 0.5f;
        }

        internal static bool IsBindingCaptureActive()
        {
            return Time.unscaledTime - _bindingCaptureHeartbeatTime < 0.5f;
        }

        internal static bool TryGetLogiState(out LogitechGSDK.DIJOYSTATE2ENGINES state)
        {
            state = default;

            if (!ShouldApply())
            {
                return false;
            }

            if (!_logiInitAttempted)
            {
                TryInitLogitech();
            }

            if (!_logiAvailable)
            {
                return false;
            }

            if (!SafeLogiUpdate())
            {
                return false;
            }

            _logiConnected = LogitechGSDK.LogiIsConnected(_logiIndex);
            if (!_logiConnected)
            {
                _logiWasConnected = false;
                return false;
            }

            MaybeLogWheelDetected();

            state = LogitechGSDK.LogiGetStateCSharp(_logiIndex);
            EnsureDefaultCalibrationFromState(state);
            return true;
        }

        private static void EnsureDefaultCalibrationFromState(LogitechGSDK.DIJOYSTATE2ENGINES state)
        {
            // If the user hasn't calibrated yet, seed "released" pedal values from the current reading.
            // This avoids the common "half throttle" case when the axis rests near 0.
            int throttleRaw = GetAxisValue(state, GetThrottleAxis());
            if (!PlayerPrefs.HasKey(PrefKeyCalThrottleReleased))
            {
                PlayerPrefs.SetInt(PrefKeyCalThrottleReleased, throttleRaw);
            }
            if (!PlayerPrefs.HasKey(PrefKeyCalThrottlePressed))
            {
                PlayerPrefs.SetInt(PrefKeyCalThrottlePressed, GuessPressedFromReleased(PlayerPrefs.GetInt(PrefKeyCalThrottleReleased)));
            }

            int brakeRaw = GetAxisValue(state, GetBrakeAxis());
            if (!PlayerPrefs.HasKey(PrefKeyCalBrakeReleased))
            {
                PlayerPrefs.SetInt(PrefKeyCalBrakeReleased, brakeRaw);
            }
            if (!PlayerPrefs.HasKey(PrefKeyCalBrakePressed))
            {
                PlayerPrefs.SetInt(PrefKeyCalBrakePressed, GuessPressedFromReleased(PlayerPrefs.GetInt(PrefKeyCalBrakeReleased)));
            }

            int clutchRaw = GetAxisValue(state, GetClutchAxis());
            if (!PlayerPrefs.HasKey(PrefKeyCalClutchReleased))
            {
                PlayerPrefs.SetInt(PrefKeyCalClutchReleased, clutchRaw);
            }
            if (!PlayerPrefs.HasKey(PrefKeyCalClutchPressed))
            {
                PlayerPrefs.SetInt(PrefKeyCalClutchPressed, GuessPressedFromReleased(PlayerPrefs.GetInt(PrefKeyCalClutchReleased)));
            }

            if (!PlayerPrefs.HasKey(PrefKeyCalSteerCenter))
            {
                PlayerPrefs.SetInt(PrefKeyCalSteerCenter, state.lX);
            }
            if (!PlayerPrefs.HasKey(PrefKeyCalSteerLeft))
            {
                PlayerPrefs.SetInt(PrefKeyCalSteerLeft, -32768);
            }
            if (!PlayerPrefs.HasKey(PrefKeyCalSteerRight))
            {
                PlayerPrefs.SetInt(PrefKeyCalSteerRight, 32767);
            }
        }

        private static int GuessPressedFromReleased(int released)
        {
            if (released >= 16000)
            {
                return -32768;
            }
            if (released <= -16000)
            {
                return 32767;
            }
            // Axis rests near center.
            return -32768;
        }

        internal static bool HasCalibration()
        {
            return PlayerPrefs.HasKey(PrefKeyCalSteerLeft)
                   && PlayerPrefs.HasKey(PrefKeyCalSteerRight)
                   && PlayerPrefs.HasKey(PrefKeyCalThrottleReleased)
                   && PlayerPrefs.HasKey(PrefKeyCalThrottlePressed)
                   && PlayerPrefs.HasKey(PrefKeyCalBrakeReleased)
                   && PlayerPrefs.HasKey(PrefKeyCalBrakePressed);
        }

        internal static void ClearCalibration()
        {
            PlayerPrefs.DeleteKey(PrefKeyCalSteerCenter);
            PlayerPrefs.DeleteKey(PrefKeyCalSteerLeft);
            PlayerPrefs.DeleteKey(PrefKeyCalSteerRight);
            PlayerPrefs.DeleteKey(PrefKeyCalThrottleReleased);
            PlayerPrefs.DeleteKey(PrefKeyCalThrottlePressed);
            PlayerPrefs.DeleteKey(PrefKeyCalBrakeReleased);
            PlayerPrefs.DeleteKey(PrefKeyCalBrakePressed);

            PlayerPrefs.DeleteKey(PrefKeyCalClutchReleased);
            PlayerPrefs.DeleteKey(PrefKeyCalClutchPressed);
        }

        internal static float NormalizeSteering(int rawX)
        {
            int left = PlayerPrefs.GetInt(PrefKeyCalSteerLeft, -32768);
            int right = PlayerPrefs.GetInt(PrefKeyCalSteerRight, 32767);
            int center = PlayerPrefs.GetInt(PrefKeyCalSteerCenter, (left + right) / 2);

            if (right == left)
            {
                return 0f;
            }

            float value;
            if (rawX >= center)
            {
                float denom = right - center;
                value = denom <= 0.001f ? 0f : (rawX - center) / denom;
            }
            else
            {
                float denom = center - left;
                value = denom <= 0.001f ? 0f : -(center - rawX) / denom;
            }

            value = Mathf.Clamp(value, -1f, 1f);

            float deadzone = GetSteeringDeadzone();
            float av = Mathf.Abs(value);
            if (av <= deadzone)
            {
                value = 0f;
            }
            else
            {
                // Re-scale so the remaining range still maps to [-1, 1].
                value = Mathf.Sign(value) * Mathf.Clamp01((av - deadzone) / Mathf.Max(0.0001f, 1f - deadzone));
            }

            float gain = GetSteeringGain();
            value = Mathf.Clamp(value * gain, -1f, 1f);
            return value;
        }

        internal static float NormalizePedal(int rawAxis, PedalKind kind)
        {
            string releasedKey;
            string pressedKey;
            switch (kind)
            {
                case PedalKind.Throttle:
                    releasedKey = PrefKeyCalThrottleReleased;
                    pressedKey = PrefKeyCalThrottlePressed;
                    break;
                case PedalKind.Brake:
                    releasedKey = PrefKeyCalBrakeReleased;
                    pressedKey = PrefKeyCalBrakePressed;
                    break;
                default:
                    releasedKey = PrefKeyCalClutchReleased;
                    pressedKey = PrefKeyCalClutchPressed;
                    break;
            }

            int released = PlayerPrefs.GetInt(releasedKey, 32767);
            int pressed = PlayerPrefs.GetInt(pressedKey, -32768);

            float t = Mathf.InverseLerp(released, pressed, rawAxis);
            t = Mathf.Clamp01(t);
            if (t < 0.05f)
            {
                t = 0f;
            }
            return t;
        }

        internal static int GetAxisValue(LogitechGSDK.DIJOYSTATE2ENGINES state, AxisId axis)
        {
            switch (axis)
            {
                case AxisId.lX:
                    return state.lX;
                case AxisId.lY:
                    return state.lY;
                case AxisId.lZ:
                    return state.lZ;
                case AxisId.lRx:
                    return state.lRx;
                case AxisId.lRy:
                    return state.lRy;
                case AxisId.lRz:
                    return state.lRz;
                case AxisId.slider0:
                    return state.rglSlider != null && state.rglSlider.Length > 0 ? state.rglSlider[0] : 0;
                case AxisId.slider1:
                    return state.rglSlider != null && state.rglSlider.Length > 1 ? state.rglSlider[1] : 0;
                default:
                    return 0;
            }
        }

        private static void ShutdownLogitech()
        {
            if (!_logiAvailable)
            {
                return;
            }

            try
            {
                StopAllForces();
                LogitechGSDK.LogiSteeringShutdown();
            }
            catch
            {
                // ignore shutdown exceptions
            }
        }

        internal enum FfbTestEffect
        {
            None = 0,
            Shake = 1,
            Bumpy = 2
        }

        private static FfbTestEffect _ffbTestEffect;
        private static float _ffbTestEndTime;

        internal static void StartFfbTest(FfbTestEffect effect, float durationSeconds = 2.5f)
        {
            if (effect == FfbTestEffect.None)
            {
                StopFfbTest();
                return;
            }

            _ffbTestEffect = effect;
            _ffbTestEndTime = Time.unscaledTime + Mathf.Clamp(durationSeconds, 0.25f, 10f);
            LogDebug("FFB test start: " + effect);
        }

        internal static void StopFfbTest()
        {
            if (_ffbTestEffect != FfbTestEffect.None)
            {
                LogDebug("FFB test stop");
            }
            _ffbTestEffect = FfbTestEffect.None;
            _ffbTestEndTime = 0f;
            StopAllForces();
        }

        internal static bool IsFfbTestActive()
        {
            if (_ffbTestEffect == FfbTestEffect.None)
            {
                return false;
            }

            if (Time.unscaledTime < _ffbTestEndTime)
            {
                return true;
            }

            // Auto-clear after timeout so the UI reflects reality.
            _ffbTestEffect = FfbTestEffect.None;
            _ffbTestEndTime = 0f;
            return false;
        }

        private static void UpdateFfb()
        {
            if (!ShouldApply())
            {
                return;
            }

            // Always call; it handles retry throttling.
            TryInitLogitech();

            if (!_logiAvailable)
            {
                return;
            }

            bool wheelMenuActive = IsWheelMenuActive();
            bool ffbPageActive = IsFfbPageActive();
            bool enabled = ffbPageActive || GetFfbEnabled();
            if (!enabled)
            {
                return;
            }

            // Disable FFB while paused / input locked (menus, cutscenes, building UIs).
            if ((PauseSystem.paused || IsInputLocked()) && !(wheelMenuActive && GetFfbEnabled()) && !ffbPageActive)
            {
                StopAllForces();
                return;
            }

            bool inCar = !_isInWalkingMode;
            if (!inCar && !(wheelMenuActive && GetFfbEnabled()) && !ffbPageActive)
            {
                StopAllForces();
                return;
            }

            if (!SafeLogiUpdate())
            {
                return;
            }

            _logiConnected = LogitechGSDK.LogiIsConnected(_logiIndex);
            if (!_logiConnected)
            {
                _logiWasConnected = false;
                return;
            }

            MaybeLogWheelDetected();

            if (!LogitechGSDK.LogiHasForceFeedback(_logiIndex))
            {
                return;
            }

            bool menuMode = ffbPageActive || (wheelMenuActive && GetFfbEnabled());
            float speed = menuMode ? 0f : Mathf.Clamp(_currentSpeedKmh, 0f, 200f);

            int damperCoeff = Mathf.RoundToInt(Mathf.Lerp(5f, 35f, Mathf.Clamp01(speed / 120f)) * GetFfbDamperGain());
            damperCoeff = Mathf.Clamp(damperCoeff, 0, 100);
            LogitechGSDK.LogiPlayDamperForce(_logiIndex, damperCoeff);

            // Spring is the main "centering" force; allow a stronger top-end.
            int springCoeff = Mathf.RoundToInt(Mathf.Lerp(20f, 95f, Mathf.Clamp01(speed / 100f)) * GetFfbSpringGain());
            springCoeff = Mathf.Clamp(springCoeff, 0, 100);
            LogitechGSDK.LogiPlaySpringForce(_logiIndex, 0, springCoeff, springCoeff);

            if (IsFfbTestActive())
            {
                // Tests should reflect current overall/spring/damper settings, so keep the baseline forces above
                // and only override the "road effect" layer.
                LogitechGSDK.LogiStopDirtRoadEffect(_logiIndex);
                LogitechGSDK.LogiStopBumpyRoadEffect(_logiIndex);

                if (_ffbTestEffect == FfbTestEffect.Shake)
                {
                    // Oscillating constant force.
                    int magnitude = Mathf.RoundToInt(Mathf.Sin(Time.unscaledTime * 14f) * 70f);
                    magnitude = Mathf.Clamp(magnitude, -100, 100);
                    LogitechGSDK.LogiPlayConstantForce(_logiIndex, magnitude);
                }
                else if (_ffbTestEffect == FfbTestEffect.Bumpy)
                {
                    LogitechGSDK.LogiStopConstantForce(_logiIndex);
                    LogitechGSDK.LogiPlayBumpyRoadEffect(_logiIndex, 25);
                }

                return;
            }

            if (!menuMode && _isOffRoad && speed > 5f)
            {
                int dirt = Mathf.RoundToInt(Mathf.Lerp(10f, 35f, Mathf.Clamp01(speed / 70f)));
                dirt = Mathf.Clamp(dirt, 0, 100);
                LogitechGSDK.LogiPlayDirtRoadEffect(_logiIndex, dirt);
            }
            else if (!menuMode && _isSliding)
            {
                LogitechGSDK.LogiPlayBumpyRoadEffect(_logiIndex, 8);
            }
            else
            {
                LogitechGSDK.LogiStopDirtRoadEffect(_logiIndex);
                LogitechGSDK.LogiStopBumpyRoadEffect(_logiIndex);
            }
        }

        private static bool IsInputLocked()
        {
            try
            {
                var players = sInputManager.players;
                if (players == null || players.Length == 0 || players[0] == null)
                {
                    return false;
                }

                return players[0].lockInput || players[0].freeCamMode;
            }
            catch
            {
                return false;
            }
        }

        private static bool SafeLogiUpdate()
        {
            try
            {
                return LogitechGSDK.LogiUpdate();
            }
            catch
            {
                return false;
            }
        }

        private static void StopAllForces()
        {
            if (!_logiAvailable)
            {
                return;
            }

            try
            {
                LogitechGSDK.LogiStopSpringForce(_logiIndex);
                LogitechGSDK.LogiStopDamperForce(_logiIndex);
                LogitechGSDK.LogiStopDirtRoadEffect(_logiIndex);
                LogitechGSDK.LogiStopBumpyRoadEffect(_logiIndex);
                LogitechGSDK.LogiStopConstantForce(_logiIndex);
                LogitechGSDK.LogiStopSurfaceEffect(_logiIndex);
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyControllerPropertiesIfReady(bool? forceEnableOverride)
        {
            if (!_logiAvailable)
            {
                return;
            }

            try
            {
                bool forceEnable = forceEnableOverride.HasValue ? forceEnableOverride.Value : GetFfbEnabled();
                var props = new LogitechGSDK.LogiControllerPropertiesData
                {
                    forceEnable = forceEnable,
                    overallGain = Mathf.RoundToInt(GetFfbOverallGain() * 100f),
                    springGain = Mathf.RoundToInt(GetFfbSpringGain() * 100f),
                    damperGain = Mathf.RoundToInt(GetFfbDamperGain() * 100f),
                    defaultSpringEnabled = false,
                    defaultSpringGain = 0,
                    combinePedals = false,
                    wheelRange = GetWheelRange(),
                    gameSettingsEnabled = true,
                    allowGameSettings = true
                };
                LogitechGSDK.LogiSetPreferredControllerProperties(props);
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyControllerPropertiesIfReady()
        {
            ApplyControllerPropertiesIfReady(forceEnableOverride: null);
        }

        private static void ApplyWheelRangeIfReady()
        {
            if (!_logiAvailable)
            {
                return;
            }

            try
            {
                LogitechGSDK.LogiSetOperatingRange(_logiIndex, GetWheelRange());
            }
            catch
            {
                // ignore
            }
        }
    }
}
