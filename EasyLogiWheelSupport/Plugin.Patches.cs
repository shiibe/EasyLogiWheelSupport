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

            // Allow wheel buttons to navigate menus even while paused/locked.
            InjectWheelButtonBindings(__instance);

            // Don't inject wheel input while the game is explicitly locking input (menus/cutscenes/buildings).
            if (__instance.lockInput || PauseSystem.paused)
            {
                return;
            }

            if (_isInWalkingMode)
            {
                return;
            }

            if (!TryGetCachedWheelState(out var state))
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

        private static void InjectWheelButtonBindings(sInputManager input)
        {
            if (input == null)
            {
                return;
            }

            // Pull a fresh cache frame so Pressed/Released works reliably.
            if (!TryGetCachedWheelState(out _))
            {
                return;
            }

            var modifier = GetModifierBinding();
            bool modifierDown = modifier.Kind != BindingKind.None && IsBindingDownForCurrentFrame(modifier);
            BindingLayer layer = modifierDown ? BindingLayer.Modified : BindingLayer.Normal;

            bool allowPovBinds = !_isInWalkingMode && !PauseSystem.paused && !input.lockInput;

            bool Pressed(BindingInput b)
            {
                if (b.Kind == BindingKind.Pov && !allowPovBinds)
                {
                    return false;
                }
                return IsBindingPressedThisFrameForCurrentFrame(b);
            }

            bool Released(BindingInput b)
            {
                if (b.Kind == BindingKind.Pov && !allowPovBinds)
                {
                    return false;
                }
                return IsBindingReleasedThisFrameForCurrentFrame(b);
            }

            bool Down(BindingInput b)
            {
                if (b.Kind == BindingKind.Pov && !allowPovBinds)
                {
                    return false;
                }
                return IsBindingDownForCurrentFrame(b);
            }

            // POV -> menu cursor (fake mouse) while paused.
            // Disable this while the bindings capture screen is open so the user can bind D-pad directions.
            if (PauseSystem.paused && !IsBindingCaptureActive())
            {
                if (TryGetPov8Vector(out var pov))
                {
                    // Map POV to the menu cursor input. Normalize diagonals so they aren't faster.
                    Vector2 mouse = -pov;
                    if (mouse != Vector2.zero)
                    {
                        if (Mathf.Abs(mouse.x) > 0.1f && Mathf.Abs(mouse.y) > 0.1f)
                        {
                            mouse.Normalize();
                        }
                        input.mouseInput = mouse;
                    }
                }
            }

            // Don't trigger gameplay/program actions while the wheel menu is open.
            // Keep D-pad menu cursor active unless we're in binding capture.
            if (IsWheelMenuActive())
            {
                return;
            }

            // POV -> walking movement.
            if (_isInWalkingMode && !PauseSystem.paused && !input.lockInput)
            {
                if (TryGetPov8Vector(out var pov))
                {
                    // Invert so D-pad direction matches on-screen movement.
                    Vector2 move = -pov;
                    if (move != Vector2.zero)
                    {
                        input.playerInput = move;
                    }
                }
            }

            // Click/Select (Interact/OK)
            {
                var bind = GetBinding(layer, ButtonBindAction.InteractOk);
                if (bind.Kind != BindingKind.None)
                {
                    if (Pressed(bind))
                    {
                        input.selectPressed = true;
                    }
                    if (Released(bind))
                    {
                        input.selectReleased = true;
                    }
                }
            }

            // Back
            {
                var bind = GetBinding(layer, ButtonBindAction.Back);
                if (bind.Kind != BindingKind.None)
                {
                    if (Pressed(bind))
                    {
                        input.backPressed = true;
                    }
                    if (Released(bind))
                    {
                        input.backReleased = true;
                    }
                }
            }

            // Pause
            {
                var bind = GetBinding(layer, ButtonBindAction.Pause);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    input.pausePressed = true;
                }
            }

            // Map/Items
            {
                var bind = GetBinding(layer, ButtonBindAction.MapItems);
                if (bind.Kind != BindingKind.None)
                {
                    // Context-sensitive, matching the game's intent:
                    // - Driving: open the job/map program (Map)
                    // - On foot: open items (Inventory)
                    if (_isInWalkingMode)
                    {
                        if (Pressed(bind))
                        {
                            input.inventoryPressed = true;
                        }
                        if (Released(bind))
                        {
                            input.inventoryReleased = true;
                        }
                        if (Down(bind))
                        {
                            input.inventoryHeld = true;
                        }
                    }
                    else
                    {
                        if (Pressed(bind))
                        {
                            input.mapPressed = true;
                        }
                    }
                }
            }

            // Camera change
            {
                var bind = GetBinding(layer, ButtonBindAction.Camera);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    input.cameraPressed = true;
                }
            }

            // Reset vehicle
            {
                var bind = GetBinding(layer, ButtonBindAction.ResetVehicle);
                if (bind.Kind != BindingKind.None)
                {
                    if (Pressed(bind))
                    {
                        input.resetPressed = true;
                    }
                    // Some game code reads resetHeld (oddly assigned with WasPerformedThisFrame). Treat as pressed.
                    if (Down(bind))
                    {
                        input.resetHeld = true;
                    }
                }
            }

            // Headlights
            {
                var bind = GetBinding(layer, ButtonBindAction.Headlights);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    input.headlightsPressed = true;
                }
            }


            // Horn
            {
                var bind = GetBinding(layer, ButtonBindAction.Horn);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    input.hornPressed = true;
                }
            }

            // Radio: emulate the game's Radio action (radioPressed + radioInput).
            // Only while driving; D-pad is reserved for walking movement.
            if (!_isInWalkingMode && !PauseSystem.paused && !input.lockInput)
            {
                Vector2 radio = Vector2.zero;
                bool radioPressed = false;

                // Down: Radio on/off
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioPower);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        radio.y = -1f;
                        radioPressed = true;
                    }
                }

                // Right: Scan
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioScanRight);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        radio.x = 1f;
                        radioPressed = true;
                    }
                }

                // Left: Channels
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioScanLeft);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        radio.x = -1f;
                        radioPressed = true;
                    }
                }

                // Up: Scan toggle (optional)
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioScanToggle);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        radio.y = 1f;
                        radioPressed = true;
                    }
                }

                if (radioPressed)
                {
                    input.radioPressed = true;
                    input.radioInput = radio;
                }
            }
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
