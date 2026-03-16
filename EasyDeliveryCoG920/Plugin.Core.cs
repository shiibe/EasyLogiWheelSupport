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

        private static bool _logiInitAttempted;
        private static bool _logiAvailable;
        private static bool _logiConnected;
        private static int _logiIndex;

        private static bool _isInWalkingMode;
        private static float _currentSpeedKmh;
        private static bool _isOffRoad;
        private static bool _isSliding;

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
            if (_logiInitAttempted)
            {
                return;
            }
            _logiInitAttempted = true;

            try
            {
                _logiIndex = 0;
                bool ignoreXInput = _ignoreXInputControllers == null || _ignoreXInputControllers.Value;
                _logiAvailable = LogitechGSDK.LogiSteeringInitialize(ignoreXInput);
                if (!_logiAvailable)
                {
                    _log.LogWarning("Logitech SDK init failed. Make sure Logitech G HUB/LGS is installed and the wheel is connected.");
                    return;
                }

                _log.LogInfo("Logitech SDK initialized.");
                ApplyControllerPropertiesIfReady();
                ApplyWheelRangeIfReady();
            }
            catch (DllNotFoundException e)
            {
                _logiAvailable = false;
                _log.LogWarning("Logitech SDK DLL not found (LogitechSteeringWheelEnginesWrapper.dll). FFB disabled.");
                LogDebug(e.ToString());
            }
            catch (BadImageFormatException e)
            {
                _logiAvailable = false;
                _log.LogWarning("Logitech SDK DLL is wrong architecture. FFB disabled.");
                LogDebug(e.ToString());
            }
            catch (Exception e)
            {
                _logiAvailable = false;
                _log.LogWarning($"Logitech SDK init threw {e.GetType().Name}. FFB disabled.");
                LogDebug(e.ToString());
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

            if (!_logiInitAttempted)
            {
                TryInitLogitech();
            }

            if (!_logiAvailable)
            {
                return;
            }

            bool enabled = GetFfbEnabled();
            if (!enabled)
            {
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

            int springCoeff = Mathf.RoundToInt(Mathf.Lerp(20f, 70f, Mathf.Clamp01(speed / 100f)) * GetFfbSpringGain());
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
