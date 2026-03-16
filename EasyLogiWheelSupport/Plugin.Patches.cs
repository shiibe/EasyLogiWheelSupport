using System;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace EasyLogiWheelSupport
{
    public partial class Plugin
    {
        private void Awake()
        {
            _log = Logger;

            _enableMod = Config.Bind("General", "enable_mod", true, "Enables/disables the mod entirely.");
            _logDetectedDevices = Config.Bind("Debug", "log_detected_devices", true, "Log joystick names detected by Unity on startup.");
            _debugLogging = Config.Bind("Debug", "debug_logging", false, "Log debug information.");

            _desktopMenuIconVisible = Config.Bind("Menu", "show_wheel_menu_icon", true, "Show/hide the Wheel Settings icon on the Main Menu.");
            _desktopMenuIconX = Config.Bind("Menu", "wheel_menu_icon_x", "4", "Main Menu icon X position. Example: 4");
            _desktopMenuIconY = Config.Bind("Menu", "wheel_menu_icon_y", "3.25", "Main Menu icon Y position. Example: 3.25");

            _ignoreXInputControllers = Config.Bind("General", "ignore_xinput_controllers", true, "Pass 'ignoreXInputControllers' to the Logitech SDK init (recommended).");

            if (!_enableMod.Value)
            {
                _log.LogInfo("G920 mod disabled via config.");
                return;
            }

            var harmony = new Harmony(PluginGuid);

            PatchByName(harmony, "DesktopDotExe", "Setup", postfix: nameof(DesktopDotExe_Setup_Postfix));
            PatchByName(harmony, "sCarController", "Update", prefix: nameof(SCarController_Update_Prefix));
            PatchByName(harmony, "sInputManager", "GetInput", postfix: nameof(SInputManager_GetInput_Postfix));

            DetectWheelOnce();
            TryInitLogitech();
            _log.LogInfo("EasyLogiWheelSupport loaded.");
        }

        private void Update()
        {
            UpdateFfb();
        }

        private void OnDestroy()
        {
            ShutdownLogitech();
        }

        private static void SInputManager_GetInput_Postfix(sInputManager __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            // Don't inject wheel input while the game is explicitly locking input (menus/cutscenes/buildings).
            if (__instance.lockInput || PauseSystem.paused)
            {
                return;
            }

            if (_isInWalkingMode)
            {
                return;
            }

            if (!TryGetLogiState(out var state))
            {
                return;
            }

            int rawSteer = GetAxisValue(state, GetSteeringAxis());
            int rawThrottle = GetAxisValue(state, GetThrottleAxis());
            int rawBrake = GetAxisValue(state, GetBrakeAxis());

            float steering = NormalizeSteering(rawSteer);
            float throttle = NormalizePedal(rawThrottle, PedalKind.Throttle);
            float brake = NormalizePedal(rawBrake, PedalKind.Brake);

            float accel = Mathf.Clamp(throttle - brake, -1f, 1f);
            __instance.driveInput = new Vector2(steering, accel);
            __instance.breakPressed = brake > 0.1f;

            SetWheelLastInput(steering, accel);
        }

        #pragma warning disable IDE1006
        private static void SCarController_Update_Prefix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            var car = __instance as sCarController;
            if (car == null)
            {
                return;
            }

            _isInWalkingMode = car.GuyActive;

            if (car.rb != null)
            {
                _currentSpeedKmh = car.rb.linearVelocity.magnitude * 3.6f;
            }

            if (car.wheels != null)
            {
                float totalSlide = 0f;
                bool offroad = false;
                foreach (var w in car.wheels)
                {
                    if (w == null)
                    {
                        continue;
                    }
                    totalSlide += w.slide;
                    if (w.suspention != null && string.Equals(w.suspention.contactTag, "offRoad", StringComparison.OrdinalIgnoreCase))
                    {
                        offroad = true;
                    }
                }
                _isOffRoad = offroad;
                _isSliding = totalSlide > 2.0f;
            }
        }
    }
}
