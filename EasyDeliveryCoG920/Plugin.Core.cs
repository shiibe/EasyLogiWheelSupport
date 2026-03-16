using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace EasyDeliveryCoG920
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

        internal enum PedalKind
        {
            Throttle = 0,
            Brake = 1
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

        private static bool _logiInitAttempted;
        private static bool _logiAvailable;
        private static bool _logiConnected;
        private static int _logiIndex;

        private static int _logiInitAttemptCount;
        private static float _logiNextInitAttemptTime;
        private static bool _logiIgnoreXInputUsed;

        private static bool _isInWalkingMode;
        private static float _currentSpeedKmh;
        private static bool _isOffRoad;
        private static bool _isSliding;

        private static int _wheelLastUpdateFrame;
        private static float _wheelLastSteer;
        private static float _wheelLastAccel;

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
            return Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKeyFfbOverall, 0.75f));
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
            return Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyWheelRange, 900), 180, 900);
        }

        internal static void SetWheelRange(int degrees)
        {
            degrees = Mathf.Clamp(degrees, 180, 900);
            PlayerPrefs.SetInt(PrefKeyWheelRange, degrees);
            ApplyWheelRangeIfReady();
        }

        internal static float GetSteeringGain()
        {
            return Mathf.Clamp(PlayerPrefs.GetFloat(PrefKeySteeringGain, 1.0f), 0.5f, 2.5f);
        }

        internal static void SetSteeringGain(float value)
        {
            value = Mathf.Clamp(value, 0.5f, 2.5f);
            PlayerPrefs.SetFloat(PrefKeySteeringGain, value);
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

        internal static AxisId GetThrottleAxis()
        {
            // G920 commonly reports throttle on lY.
            return (AxisId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyThrottleAxis, (int)AxisId.lY), 0, (int)AxisId.slider1);
        }

        internal static AxisId GetBrakeAxis()
        {
            return (AxisId)Mathf.Clamp(PlayerPrefs.GetInt(PrefKeyBrakeAxis, (int)AxisId.lRz), 0, (int)AxisId.slider1);
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
                    _log.LogWarning("Logitech SDK init failed. Make sure Logitech G HUB/LGS is installed, and the wheel is connected/powered.");
                }
                else
                {
                    LogDebug($"Logitech SDK init still failing (attempt {_logiInitAttemptCount}).");
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
            _log.LogInfo($"Logitech SDK initialized (ignoreXInputControllers={ignoreXInputControllers}).");
            ApplyControllerPropertiesIfReady();
            ApplyWheelRangeIfReady();
            return true;
        }

        internal static void ForceReinitLogitech()
        {
            _logiAvailable = false;
            _logiConnected = false;
            _logiInitAttempted = false;
            _logiInitAttemptCount = 0;
            _logiNextInitAttemptTime = 0f;
            try
            {
                LogitechGSDK.LogiSteeringShutdown();
            }
            catch
            {
            }
        }

        internal static string GetLogitechStatus()
        {
            if (!_logiInitAttempted)
            {
                return "Not initialized";
            }
            if (!_logiAvailable)
            {
                return "Init failed";
            }
            return _logiConnected ? "Connected" : "No wheel";
        }

        internal static void SetWheelLastInput(float steer, float accel)
        {
            _wheelLastUpdateFrame = Time.frameCount;
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
                return false;
            }

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
            string releasedKey = kind == PedalKind.Throttle ? PrefKeyCalThrottleReleased : PrefKeyCalBrakeReleased;
            string pressedKey = kind == PedalKind.Throttle ? PrefKeyCalThrottlePressed : PrefKeyCalBrakePressed;

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

            bool enabled = GetFfbEnabled();
            if (!enabled)
            {
                return;
            }

            // Disable FFB while paused / input locked (menus, cutscenes, building UIs).
            if (PauseSystem.paused || IsInputLocked())
            {
                StopAllForces();
                return;
            }

            bool inCar = !_isInWalkingMode;
            if (!inCar)
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
                return;
            }

            if (!LogitechGSDK.LogiHasForceFeedback(_logiIndex))
            {
                return;
            }

            float speed = Mathf.Clamp(_currentSpeedKmh, 0f, 200f);

            int damperCoeff = Mathf.RoundToInt(Mathf.Lerp(5f, 35f, Mathf.Clamp01(speed / 120f)) * GetFfbDamperGain());
            damperCoeff = Mathf.Clamp(damperCoeff, 0, 100);
            LogitechGSDK.LogiPlayDamperForce(_logiIndex, damperCoeff);

            // Spring is the main "centering" force; allow a stronger top-end.
            int springCoeff = Mathf.RoundToInt(Mathf.Lerp(20f, 95f, Mathf.Clamp01(speed / 100f)) * GetFfbSpringGain());
            springCoeff = Mathf.Clamp(springCoeff, 0, 100);
            LogitechGSDK.LogiPlaySpringForce(_logiIndex, 0, springCoeff, springCoeff);

            if (_isOffRoad && speed > 5f)
            {
                int dirt = Mathf.RoundToInt(Mathf.Lerp(10f, 35f, Mathf.Clamp01(speed / 70f)));
                dirt = Mathf.Clamp(dirt, 0, 100);
                LogitechGSDK.LogiPlayDirtRoadEffect(_logiIndex, dirt);
            }
            else if (_isSliding)
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

        private static void ApplyControllerPropertiesIfReady()
        {
            if (!_logiAvailable)
            {
                return;
            }

            try
            {
                var props = new LogitechGSDK.LogiControllerPropertiesData
                {
                    forceEnable = GetFfbEnabled(),
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
