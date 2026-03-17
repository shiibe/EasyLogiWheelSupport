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
            PatchByName(harmony, "sHUD", "RadioDisplay", postfix: nameof(SHUD_RadioDisplay_Postfix));

            // Optional sound tweaks in manual mode.
            PatchByName(harmony, "sEngineSFX", "Update", postfix: nameof(SEngineSFX_Update_Postfix));
            PatchByName(harmony, "EngineSFX", "Update", postfix: nameof(SEngineSFX_Update_Postfix));


            DetectWheelOnce();
            TryInitLogitech();
            _manualTransmissionEnabled = GetManualTransmissionEnabled();
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

            // When testing with a controller plugged in, the game's default bindings can interfere.
            // Optional exclusive mode: clear all non-wheel inputs, then re-inject wheel bindings.
            if (GetExclusiveWheelInputEnabled())
            {
                __instance.driveInput = Vector2.zero;
                __instance.playerInput = Vector2.zero;
                __instance.mouseInput = Vector2.zero;
                __instance.radioInput = Vector2.zero;
                __instance.cameraLook = Vector2.zero;

                __instance.radioPressed = false;
                __instance.selectPressed = false;
                __instance.selectReleased = false;
                __instance.backPressed = false;
                __instance.backReleased = false;
                __instance.breakPressed = false;
                __instance.inventoryPressed = false;
                __instance.inventoryReleased = false;
                __instance.inventoryHeld = false;
                __instance.headlightsPressed = false;
                __instance.cameraPressed = false;
                __instance.resetPressed = false;
                __instance.resetHeld = false;
                __instance.pausePressed = false;
                __instance.mapPressed = false;
                __instance.hornPressed = false;
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

            _lastThrottle01 = Mathf.Clamp01(throttle);
            if (GetManualTransmissionEnabled() && GetManualGear() == 0)
            {
                _neutralRev01 = Mathf.Lerp(_neutralRev01, _lastThrottle01, Time.deltaTime * 8f);
            }
            else
            {
                _neutralRev01 = Mathf.Lerp(_neutralRev01, 0f, Time.deltaTime * 6f);
            }

            float accel;
            if (GetManualTransmissionEnabled())
            {
                accel = Mathf.Clamp(ComputeManualAccel(throttle), -1f, 1f);
            }
            else
            {
                accel = Mathf.Clamp(throttle - brake, -1f, 1f);
            }
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

            // Transmission (manual gearbox).
            // Only while driving; avoid menu/walking conflicts.
            if (!_isInWalkingMode && !PauseSystem.paused && !input.lockInput)
            {
                var bind = GetBinding(layer, ButtonBindAction.ToggleGearbox);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    ToggleManualTransmission();
                }

                var up = GetBinding(layer, ButtonBindAction.ShiftUp);
                if (up.Kind != BindingKind.None && Pressed(up))
                {
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(+1);
                }

                var down = GetBinding(layer, ButtonBindAction.ShiftDown);
                if (down.Kind != BindingKind.None && Pressed(down))
                {
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(-1);
                }

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

            _currentCar = car;

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

        private static void SHUD_RadioDisplay_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            var hud = __instance as sHUD;
            if (hud == null)
            {
                return;
            }

            // Only while driving.
            var car = UnityEngine.Object.FindFirstObjectByType<sCarController>();
            if (car == null || car.GuyActive)
            {
                return;
            }

            // MiniRenderer is a private field in sHUD.
            var rField = AccessTools.Field(typeof(sHUD), "R");
            var R = rField != null ? rField.GetValue(hud) as MiniRenderer : null;
            if (R == null)
            {
                return;
            }

            bool showSpeed = GetHudShowSpeed();
            bool manualEnabled = GetManualTransmissionEnabled();
            bool showTach = manualEnabled && GetHudShowTach();
            bool showGear = manualEnabled && GetHudShowGear();
            if (!showSpeed && !showTach && !showGear)
            {
                return;
            }

            // Place under the default money/time readout area.
            // sHUD uses vector=(68, height-64) and draws money at y-2, time at y+10.
            float x = 68f;
            float y = R.height - 64f + 22f;

            void PutLeft(string text, float leftX, float yy)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    R.put(text, leftX, yy);
                }
            }

            if (showSpeed)
            {
                float spd = ConvertSpeedForHud(_currentSpeedKmh);
                int spdInt = Mathf.Max(0, Mathf.RoundToInt(spd));
                string unit = GetHudSpeedUnitLabel(GetHudSpeedUnit());
                PutLeft($"{spdInt}{unit}", x, y);
                y += 10f;
            }
            if (showTach)
            {
                PutLeft($"{Mathf.RoundToInt(GetEstimatedRpm())}rpm", x, y);
                y += 10f;
            }
            if (showGear)
            {
                // Example: R P (1) 2 3 4 5
                int g = GetManualGear();

                string GearToken(string t, bool selected)
                {
                    return selected ? "(" + t + ")" : t;
                }

                string line =
                    GearToken("R", g < 0) +
                    " " + GearToken("N", g == 0) +
                    " " + GearToken("1", g == 1) +
                    " " + GearToken("2", g == 2) +
                    " " + GearToken("3", g == 3) +
                    " " + GearToken("4", g == 4) +
                    " " + GearToken("5", g == 5);

                PutLeft(line, x, y);
            }
        }


        private static Type _engineSfxRuntimeType;
        private static System.Reflection.FieldInfo _engineCarField;
        private static System.Reflection.FieldInfo _engineIdleField;
        private static System.Reflection.FieldInfo _engineDriveField;
        private static System.Reflection.FieldInfo _engineIntenseField;
        private static System.Reflection.FieldInfo _engineDistortionField;

        private static void EnsureEngineSfxRefs(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var t = instance.GetType();
            if (_engineSfxRuntimeType == t)
            {
                return;
            }

            _engineSfxRuntimeType = t;
            _engineCarField = AccessTools.Field(t, "car");
            _engineIdleField = AccessTools.Field(t, "idle");
            _engineDriveField = AccessTools.Field(t, "drive");
            _engineIntenseField = AccessTools.Field(t, "intense");
            _engineDistortionField = AccessTools.Field(t, "distortionFilter");
        }

        private static void SEngineSFX_Update_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null || !GetManualTransmissionEnabled())
            {
                return;
            }

            EnsureEngineSfxRefs(__instance);

            var car = _engineCarField != null ? _engineCarField.GetValue(__instance) as sCarController : null;
            if (car == null || car.GuyActive)
            {
                return;
            }

            // Only adjust the active car.
            if (_currentCar != null && !ReferenceEquals(car, _currentCar))
            {
                return;
            }

            float rpmNorm = GetEstimatedRpmNormForSound();
            float over = Mathf.Clamp01((rpmNorm - 1f) / 0.2f);
            float neutral = (_manualGear == 0) ? Mathf.Clamp01(_neutralRev01) : 0f;

            // Apply pitch boost to all loops so revving in Neutral is audible.
            float pitchMul = 1f + neutral * 0.85f + over * 0.35f;

            var idle = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as AudioSource : null;
            var drive = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as AudioSource : null;
            var intense = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as AudioSource : null;
            if (idle != null)
            {
                idle.pitch *= pitchMul;
            }
            if (drive != null)
            {
                drive.pitch *= pitchMul;
            }
            if (intense != null)
            {
                intense.pitch *= pitchMul;
            }

            // In neutral, also push volume up so it actually sounds like revving.
            if (neutral > 0.01f)
            {
                if (drive != null)
                {
                    drive.volume = Mathf.Max(drive.volume, neutral * 0.55f);
                }
                if (intense != null)
                {
                    intense.volume = Mathf.Max(intense.volume, neutral * 0.35f);
                }
            }

            // Extra distortion when over-revving (simulated).
            var dist = _engineDistortionField != null ? _engineDistortionField.GetValue(__instance) as AudioDistortionFilter : null;
            if (dist != null)
            {
                float target = dist.distortionLevel;
                if (over > 0f)
                {
                    target = Mathf.Max(target, 0.10f + over * 0.55f);
                }
                if (neutral > 0.1f)
                {
                    target = Mathf.Max(target, 0.05f + neutral * 0.10f);
                }
                dist.distortionLevel = target;
            }
        }
    }
}
