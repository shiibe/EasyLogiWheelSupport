using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace EasyLogiWheelSupport
{
    public partial class Plugin
    {
        private static Plugin _instance;

        private static ConfigEntry<string> _ignitionSfxOnPath;
        private static ConfigEntry<float> _ignitionSfxVolume;

        private static UnityEngine.Object _ignitionSfxOn;
        private static UnityEngine.Object _ignitionSfxOff;

        private void Awake()
        {
            _log = Logger;
            _instance = this;

            _enableMod = Config.Bind("General", "enable_mod", true, "Enables/disables the mod entirely.");
            _logDetectedDevices = Config.Bind("Debug", "log_detected_devices", true, "Log joystick names detected by Unity on startup.");
            _debugLogging = Config.Bind("Debug", "debug_logging", false, "Log debug information.");

            _desktopMenuIconVisible = Config.Bind("Menu", "show_wheel_menu_icon", true, "Show/hide the Wheel Settings icon on the Main Menu.");
            _desktopMenuIconX = Config.Bind("Menu", "wheel_menu_icon_x", "4", "Main Menu icon X position. Example: 4");
            _desktopMenuIconY = Config.Bind("Menu", "wheel_menu_icon_y", "3.25", "Main Menu icon Y position. Example: 3.25");

            _ignoreXInputControllers = Config.Bind("General", "ignore_xinput_controllers", true, "Pass 'ignoreXInputControllers' to the Logitech SDK init (recommended).");

            _ignitionSfxOnPath = Config.Bind("Ignition", "sfx_on_path", "", "Optional ignition ON sound (.wav PCM; 16/24-bit supported). Leave blank to auto-load ignition_on.wav from the plugin folder.");
            _ignitionSfxVolume = Config.Bind("Ignition", "sfx_volume", 0.6f, "Ignition sound volume (0..1).");

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
            PatchByName(harmony, "sHUD", "DoFuelMath", prefix: nameof(SHUD_DoFuelMath_Prefix));

            // Vehicle lighting control (ignition + tuning).
            PatchByName(harmony, "Headlights", "Toggle", prefix: nameof(Headlights_Toggle_Prefix), postfix: nameof(Headlights_Toggle_Postfix));
            PatchByName(harmony, "Headlights", "Break", prefix: nameof(Headlights_Break_Prefix));

            // Optional sound tweaks in manual mode.
            PatchByName(harmony, "sEngineSFX", "Update", postfix: nameof(SEngineSFX_Update_Postfix));
            PatchByName(harmony, "EngineSFX", "Update", postfix: nameof(SEngineSFX_Update_Postfix));


            DetectWheelOnce();
            TryInitLogitech();
            _manualTransmissionEnabled = GetManualTransmissionEnabled();
            TryLoadIgnitionSfx();

            _log.LogInfo("EasyLogiWheelSupport loaded.");
        }

        private static void TryLoadIgnitionSfx()
        {
            string dir = Path.GetDirectoryName(typeof(Plugin).Assembly.Location) ?? string.Empty;

            string Resolve(string cfg, string baseName)
            {
                if (!string.IsNullOrWhiteSpace(cfg))
                {
                    return cfg;
                }

                string wav = Path.Combine(dir, baseName + ".wav");
                if (File.Exists(wav)) return wav;

                string wav2 = Path.Combine(dir, "sfx", baseName + ".wav");
                if (File.Exists(wav2)) return wav2;
                return string.Empty;
            }

            string onPath = Resolve(_ignitionSfxOnPath != null ? _ignitionSfxOnPath.Value : string.Empty, "ignition_on");
            string offPath = Resolve(string.Empty, "ignition_off");

            _ignitionSfxOn = LoadWavOrNull(onPath);
            _ignitionSfxOff = LoadWavOrNull(offPath);

            // Always log once so it's obvious if the file was found.
            _log?.LogInfo("Ignition SFX: on=" + (_ignitionSfxOn != null) + " off=" + (_ignitionSfxOff != null));
        }

        private static UnityEngine.Object LoadWavOrNull(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".wav")
            {
                _log?.LogWarning("Ignition SFX: only .wav is supported in this build: " + path);
                return null;
            }
            try
            {
                return WavToAudioClip(path);
            }
            catch (Exception e)
            {
                _log?.LogWarning("Ignition SFX load failed: " + path + " (" + e.Message + ")");
                return null;
            }
        }

        private static UnityEngine.Object WavToAudioClip(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 44)
            {
                throw new Exception("WAV too small");
            }

            // RIFF header
            if (ReadAscii(bytes, 0, 4) != "RIFF" || ReadAscii(bytes, 8, 4) != "WAVE")
            {
                throw new Exception("Not a RIFF/WAVE file");
            }

            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            int audioFormat = 0;
            int dataOffset = -1;
            int dataSize = 0;
            int fmtOffset = -1;
            int fmtSize = 0;

            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                string chunkId = ReadAscii(bytes, pos, 4);
                int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                int chunkData = pos + 8;
                if (chunkData + chunkSize > bytes.Length)
                {
                    break;
                }

                if (chunkId == "fmt ")
                {
                    fmtOffset = chunkData;
                    fmtSize = chunkSize;
                    audioFormat = BitConverter.ToInt16(bytes, chunkData + 0);
                    channels = BitConverter.ToInt16(bytes, chunkData + 2);
                    sampleRate = BitConverter.ToInt32(bytes, chunkData + 4);
                    bitsPerSample = BitConverter.ToInt16(bytes, chunkData + 14);
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkData;
                    dataSize = chunkSize;
                    break;
                }

                pos = chunkData + chunkSize;
                if ((pos & 1) == 1) pos++; // word align
            }

            if (dataOffset < 0 || dataSize <= 0)
            {
                throw new Exception("Missing data chunk");
            }

            if (_debugLogging != null && _debugLogging.Value)
            {
                _log?.LogInfo(
                    "Ignition SFX WAV: fmt=" + audioFormat +
                    " bits=" + bitsPerSample +
                    " ch=" + channels +
                    " hz=" + sampleRate +
                    " data=" + dataSize +
                    " bytes (" + Path.GetFileName(path) + ")");
            }

            // Handle WAVE_FORMAT_EXTENSIBLE (often used for 24-bit PCM).
            if (audioFormat == 65534)
            {
                if (fmtOffset < 0 || fmtSize < 40)
                {
                    throw new Exception("WAV extensible missing extended fmt");
                }

                // fmt extension layout (after standard 16 bytes):
                // cbSize(2), validBits(2), channelMask(4), subFormat(16)
                int cbSize = BitConverter.ToInt16(bytes, fmtOffset + 16);
                int sub = fmtOffset + 24;
                if (cbSize < 22 || sub + 16 > fmtOffset + fmtSize)
                {
                    throw new Exception("WAV extensible bad cbSize");
                }

                // PCM GUID: 00000001-0000-0010-8000-00AA00389B71
                // IEEE float GUID: 00000003-0000-0010-8000-00AA00389B71
                bool isPcm = GuidEquals(bytes, sub, new byte[]
                {
                    0x01,0x00,0x00,0x00, 0x00,0x00, 0x10,0x00, 0x80,0x00, 0x00,0xAA,0x00,0x38,0x9B,0x71
                });
                bool isFloat = GuidEquals(bytes, sub, new byte[]
                {
                    0x03,0x00,0x00,0x00, 0x00,0x00, 0x10,0x00, 0x80,0x00, 0x00,0xAA,0x00,0x38,0x9B,0x71
                });
                if (isPcm)
                {
                    audioFormat = 1;
                }
                else if (isFloat)
                {
                    audioFormat = 3;
                }
                else
                {
                    throw new Exception("Unsupported WAV extensible subformat");
                }
            }

            if (audioFormat != 1 && audioFormat != 3)
            {
                throw new Exception("Unsupported WAV format (need PCM/IEEE float)");
            }
            if (channels != 1 && channels != 2)
            {
                throw new Exception("Unsupported channel count: " + channels);
            }
            if (sampleRate <= 0)
            {
                throw new Exception("Invalid sample rate");
            }

            if (audioFormat == 1)
            {
                if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                {
                    throw new Exception("Unsupported PCM bit depth: " + bitsPerSample);
                }
            }
            else
            {
                if (bitsPerSample != 32)
                {
                    throw new Exception("Unsupported float bit depth: " + bitsPerSample);
                }
            }

            int bytesPerSample = bitsPerSample / 8;
            int sampleCount = dataSize / bytesPerSample;
            float[] data = new float[sampleCount];
            int outI = 0;
            int end = Math.Min(bytes.Length, dataOffset + dataSize);

            if (audioFormat == 3)
            {
                for (int i = dataOffset; i + 3 < end; i += 4)
                {
                    float f = BitConverter.ToSingle(bytes, i);
                    data[outI++] = Mathf.Clamp(f, -1f, 1f);
                }
            }
            else if (bitsPerSample == 32)
            {
                for (int i = dataOffset; i + 3 < end; i += 4)
                {
                    int s = BitConverter.ToInt32(bytes, i);
                    data[outI++] = Mathf.Clamp(s / 2147483648f, -1f, 1f);
                }
            }
            else if (bitsPerSample == 24)
            {
                for (int i = dataOffset; i + 2 < end; i += 3)
                {
                    int sample = bytes[i] | (bytes[i + 1] << 8) | (bytes[i + 2] << 16);
                    if ((sample & 0x800000) != 0)
                    {
                        sample |= unchecked((int)0xFF000000);
                    }
                    data[outI++] = Mathf.Clamp(sample / 8388608f, -1f, 1f);
                }
            }
            else if (bitsPerSample == 16)
            {
                for (int i = dataOffset; i + 1 < end; i += 2)
                {
                    short s = BitConverter.ToInt16(bytes, i);
                    data[outI++] = s / 32768f;
                }
            }
            else
            {
                for (int i = dataOffset; i < end; i++)
                {
                    data[outI++] = (bytes[i] - 128) / 128f;
                }
            }

            int samplesPerChannel = outI / channels;
            string name = Path.GetFileNameWithoutExtension(path);
            Type audioClipType = AccessTools.TypeByName("UnityEngine.AudioClip");
            if (audioClipType == null)
            {
                throw new Exception("AudioClip type not found");
            }

            var create = AccessTools.Method(audioClipType, "Create", new[]
            {
                typeof(string),
                typeof(int),
                typeof(int),
                typeof(int),
                typeof(bool)
            });
            if (create == null)
            {
                throw new Exception("AudioClip.Create not found");
            }

            object clip = create.Invoke(null, new object[] { name, samplesPerChannel, channels, sampleRate, false });
            if (clip == null)
            {
                throw new Exception("AudioClip.Create returned null");
            }

            var setData = AccessTools.Method(audioClipType, "SetData", new[] { typeof(float[]), typeof(int) });
            if (setData == null)
            {
                throw new Exception("AudioClip.SetData not found");
            }
            setData.Invoke(clip, new object[] { data, 0 });

            return clip as UnityEngine.Object;
        }

        private static string ReadAscii(byte[] bytes, int offset, int len)
        {
            char[] c = new char[len];
            for (int i = 0; i < len; i++)
            {
                c[i] = (char)bytes[offset + i];
            }
            return new string(c);
        }

        private static bool GuidEquals(byte[] bytes, int offset, byte[] guidBytes)
        {
            if (bytes == null || guidBytes == null)
            {
                return false;
            }
            if (offset < 0 || offset + guidBytes.Length > bytes.Length)
            {
                return false;
            }
            for (int i = 0; i < guidBytes.Length; i++)
            {
                if (bytes[offset + i] != guidBytes[i])
                {
                    return false;
                }
            }
            return true;
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

            // Prevent tiny pedal noise from causing creep.
            if (throttle < 0.05f)
            {
                throttle = 0f;
            }
            if (brake < 0.05f)
            {
                brake = 0f;
            }

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
                int gear = GetManualGear();
                float drive = ComputeManualAccel(throttle);

                float signedKmh = _currentSpeedKmh;
                try
                {
                    if (_currentCar != null && _currentCar.rb != null)
                    {
                        signedKmh = Vector3.Dot(_currentCar.rb.linearVelocity, _currentCar.transform.forward) * 3.6f;
                    }
                }
                catch
                {
                    // ignore; use unsigned speed as a fallback
                }

                // Brake should always slow you down, not act like a reverse input.
                if (gear > 0)
                {
                    // In manual, allow brake to push input negative while moving forward for stronger braking,
                    // but clamp near standstill to avoid "brake = reverse".
                    if (signedKmh > 1.0f)
                    {
                        accel = drive - brake;
                    }
                    else
                    {
                        accel = Mathf.Max(0f, drive - brake);
                    }
                }
                else if (gear < 0)
                {
                    if (signedKmh < -1.0f)
                    {
                        accel = drive + brake;
                    }
                    else
                    {
                        accel = Mathf.Min(0f, drive + brake);
                    }
                }
                else
                {
                    accel = 0f;
                }
            }
            else
            {
                // Auto: keep vanilla behavior (brake can become reverse input).
                accel = throttle - brake;
            }
            accel = Mathf.Clamp(accel, -1f, 1f);

            // Ignition: when off, suppress throttle/drive input but keep steering + brake.
            if (!GetIgnitionEnabledEffective())
            {
                accel = 0f;
            }
            __instance.driveInput = new Vector2(steering, accel);
            __instance.breakPressed = brake > 0.1f;

            // Ignition off: block toggles and force accessories off.
            if (!GetIgnitionEnabledEffective())
            {
                EnforceIgnitionOffForCurrentCar();
            }

            SetWheelLastInput(steering, accel);
        }

        private static void InjectWheelButtonBindings(sInputManager input)
        {
            if (input == null)
            {
                return;
            }

            // Pull a fresh cache frame so Pressed/Released works reliably (when a wheel is present).
            bool haveWheelState = TryGetCachedWheelState(out _);

            var modifier = GetModifierBinding();
            bool modifierDown = haveWheelState && modifier.Kind != BindingKind.None && IsBindingDownForCurrentFrame(modifier);
            BindingLayer layer = modifierDown ? BindingLayer.Modified : BindingLayer.Normal;

            bool allowPovBinds = !_isInWalkingMode && !PauseSystem.paused && !input.lockInput;

            // If a wheel-bound action is used, clear the game's default action flags for this frame.
            // This prevents default controller binds from also firing when you re-map to a wheel button.
            bool clearedDefaultsThisFrame = false;
            void ClearDefaultActionsThisFrame()
            {
                if (clearedDefaultsThisFrame)
                {
                    return;
                }
                clearedDefaultsThisFrame = true;

                input.selectPressed = false;
                input.selectReleased = false;
                input.backPressed = false;
                input.backReleased = false;
                input.pausePressed = false;
                input.mapPressed = false;
                input.inventoryPressed = false;
                input.inventoryReleased = false;
                input.inventoryHeld = false;
                input.cameraPressed = false;
                input.resetPressed = false;
                input.resetHeld = false;
                input.headlightsPressed = false;
                input.hornPressed = false;
                input.radioPressed = false;
                input.radioInput = Vector2.zero;
            }

            bool Pressed(BindingInput b)
            {
                if (!haveWheelState)
                {
                    return false;
                }
                if (b.Kind == BindingKind.Pov && !allowPovBinds)
                {
                    return false;
                }
                return IsBindingPressedThisFrameForCurrentFrame(b);
            }

            bool Released(BindingInput b)
            {
                if (!haveWheelState)
                {
                    return false;
                }
                if (b.Kind == BindingKind.Pov && !allowPovBinds)
                {
                    return false;
                }
                return IsBindingReleasedThisFrameForCurrentFrame(b);
            }

            bool Down(BindingInput b)
            {
                if (!haveWheelState)
                {
                    return false;
                }
                if (b.Kind == BindingKind.Pov && !allowPovBinds)
                {
                    return false;
                }
                return IsBindingDownForCurrentFrame(b);
            }

            // POV -> menu cursor (fake mouse) while paused.
            // Disable this while binding capture OR calibration wizard is open.
            if (PauseSystem.paused && !IsBindingCaptureActive() && !IsCalibrationWizardActive())
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
                bool ignitionFeature = GetIgnitionFeatureEnabled();

                var ign = GetBinding(layer, ButtonBindAction.IgnitionToggle);
                if (ign.Kind == BindingKind.None && _debugLogging != null && _debugLogging.Value)
                {
                    // Only log this occasionally; otherwise it spams every frame.
                    if (Time.unscaledTime - _ignitionBindMissingLastLogTime > 5.0f)
                    {
                        _ignitionBindMissingLastLogTime = Time.unscaledTime;
                        var n = GetBinding(BindingLayer.Normal, ButtonBindAction.IgnitionToggle);
                        var m = GetBinding(BindingLayer.Modified, ButtonBindAction.IgnitionToggle);
                        if (n.Kind == BindingKind.None && m.Kind == BindingKind.None)
                        {
                            _log?.LogInfo("Ignition bind is not set (Bindings -> Ignition). Ignition is treated as always ON.");
                        }
                    }
                }
                bool ignitionDown = false;
                bool ignitionReleased = false;

                if (ign.Kind != BindingKind.None && haveWheelState)
                {
                    ignitionDown = Down(ign);
                    ignitionReleased = Released(ign);
                }

                // If ignition feature is disabled, don't intercept the button at all.
                if (!ignitionFeature)
                {
                    StopIgnitionHoldSfx();
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                    _ignitionHoldWasDown = ignitionDown;
                    ignitionDown = false;
                    ignitionReleased = false;
                }

                if (ignitionDown)
                {
                    // Prevent this button from triggering default gamepad actions while holding.
                    ClearDefaultActionsThisFrame();

                    if (_ignitionHoldStart < 0f)
                    {
                        _ignitionHoldStart = Time.unscaledTime;
                        _ignitionHoldConsumed = false;

                        // While holding to turn the engine on, play ignition-on SFX.
                        if (!GetIgnitionEnabled())
                        {
                            StartIgnitionHoldSfx(_currentCar);
                        }

                        if (_debugLogging != null && _debugLogging.Value)
                        {
                            string src = (ign.Kind != BindingKind.None && haveWheelState)
                                ? ("Wheel " + (modifierDown ? "(Modified) " : "(Normal) ") + GetBindingLabel(ign))
                                : "Wheel";
                            _log?.LogInfo("Ignition hold start: " + src);
                        }
                    }

                    if (!_ignitionHoldConsumed && Time.unscaledTime - _ignitionHoldStart >= IgnitionHoldSeconds)
                    {
                        bool next = !GetIgnitionEnabled();
                        SetIgnitionEnabled(next);
                        ApplyIgnitionStateChange(next);
                        _ignitionHoldConsumed = true;

                        StopIgnitionHoldSfx();

                        if (_debugLogging != null && _debugLogging.Value)
                        {
                            _log?.LogInfo("Ignition toggled -> " + (next ? "ON" : "OFF") + " (held " + (Time.unscaledTime - _ignitionHoldStart).ToString("0.00") + "s)");
                        }
                    }
                }

                if (ignitionReleased)
                {
                    ClearDefaultActionsThisFrame();

                    StopIgnitionHoldSfx();

                    if (_debugLogging != null && _debugLogging.Value)
                    {
                        float held = _ignitionHoldStart < 0f ? 0f : (Time.unscaledTime - _ignitionHoldStart);
                        if (!_ignitionHoldConsumed && held > 0.01f)
                        {
                            _log?.LogInfo("Ignition hold canceled (" + held.ToString("0.00") + "s)");
                        }
                    }
                    _ignitionHoldStart = -1f;
                    _ignitionHoldConsumed = false;
                }

                _ignitionHoldWasDown = ignitionDown;

                var bind = GetBinding(layer, ButtonBindAction.ToggleGearbox);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    ClearDefaultActionsThisFrame();
                    ToggleManualTransmission();
                }

                var up = GetBinding(layer, ButtonBindAction.ShiftUp);
                if (up.Kind != BindingKind.None && Pressed(up))
                {
                    ClearDefaultActionsThisFrame();
                    if (!GetManualTransmissionEnabled())
                    {
                        SetManualTransmissionEnabled(true);
                    }
                    ShiftManualGear(+1);
                }

                var down = GetBinding(layer, ButtonBindAction.ShiftDown);
                if (down.Kind != BindingKind.None && Pressed(down))
                {
                    ClearDefaultActionsThisFrame();
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
                        ClearDefaultActionsThisFrame();
                        input.selectPressed = true;
                    }
                    if (Released(bind))
                    {
                        ClearDefaultActionsThisFrame();
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
                        ClearDefaultActionsThisFrame();
                        input.backPressed = true;
                    }
                    if (Released(bind))
                    {
                        ClearDefaultActionsThisFrame();
                        input.backReleased = true;
                    }
                }
            }

            // Pause
            {
                var bind = GetBinding(layer, ButtonBindAction.Pause);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    ClearDefaultActionsThisFrame();
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
                            ClearDefaultActionsThisFrame();
                            input.inventoryPressed = true;
                        }
                        if (Released(bind))
                        {
                            ClearDefaultActionsThisFrame();
                            input.inventoryReleased = true;
                        }
                        if (Down(bind))
                        {
                            ClearDefaultActionsThisFrame();
                            input.inventoryHeld = true;
                        }
                    }
                    else
                    {
                        if (Pressed(bind))
                        {
                            ClearDefaultActionsThisFrame();
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
                    ClearDefaultActionsThisFrame();
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
                        ClearDefaultActionsThisFrame();
                        input.resetPressed = true;
                    }
                    // Some game code reads resetHeld (oddly assigned with WasPerformedThisFrame). Treat as pressed.
                    if (Down(bind))
                    {
                        ClearDefaultActionsThisFrame();
                        input.resetHeld = true;
                    }
                }
            }

            // Headlights
            {
                var bind = GetBinding(layer, ButtonBindAction.Headlights);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    ClearDefaultActionsThisFrame();
                    input.headlightsPressed = true;
                }
            }


            // Horn
            {
                var bind = GetBinding(layer, ButtonBindAction.Horn);
                if (bind.Kind != BindingKind.None && Pressed(bind))
                {
                    ClearDefaultActionsThisFrame();
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
                        ClearDefaultActionsThisFrame();
                        radio.y = -1f;
                        radioPressed = true;
                    }
                }

                // Right: Scan
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioScanRight);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        ClearDefaultActionsThisFrame();
                        radio.x = 1f;
                        radioPressed = true;
                    }
                }

                // Left: Channels
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioScanLeft);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        ClearDefaultActionsThisFrame();
                        radio.x = -1f;
                        radioPressed = true;
                    }
                }

                // Up: Scan toggle (optional)
                {
                    var bind = GetBinding(layer, ButtonBindAction.RadioScanToggle);
                    if (bind.Kind != BindingKind.None && Pressed(bind))
                    {
                        ClearDefaultActionsThisFrame();
                        radio.y = 1f;
                        radioPressed = true;
                    }
                }

                if (radioPressed)
                {
                    ClearDefaultActionsThisFrame();
                    input.radioPressed = true;
                    input.radioInput = radio;
                }
            }
        }

        private static bool SHUD_DoFuelMath_Prefix(sHUD __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return true;
            }

            // Ignition off: no fuel consumption while driving.
            if (!GetIgnitionEnabledEffective())
            {
                return false;
            }
            return true;
        }

        private const float IgnitionHoldSeconds = 1.5f;

        private static float _ignitionHoldStart = -1f;
        private static bool _ignitionHoldConsumed;
        private static bool _ignitionHoldWasDown;

        private static bool _ignitionPrevHeadlightsOn;
        private static bool _ignitionPrevRadioOn;

        private static GameObject _ignitionHoldSfxGo;
        private static AudioSource _ignitionHoldSfxSource;
        private static bool _suppressNextIgnitionOnSfx;

        private static readonly Dictionary<int, (float intensity, float range)> _lightDefaults = new Dictionary<int, (float intensity, float range)>();

        private static Type _headlightsRuntimeType;
        private static FieldInfo _headlightsHeadLightsField;
        private static FieldInfo _headlightsCarMatField;
        private static FieldInfo _headlightsEmissiveRegularField;
        private static FieldInfo _headlightsEmissiveBreakingField;
        private static FieldInfo _headlightsHeadlightsOnField;
        private static FieldInfo _headlightsModelField;
        private static Texture2D _ignitionBlackEmissiveTex;

        private static Type _lensFlareSrpType;
        private static readonly Dictionary<int, bool> _lensFlareSrpDefaults = new Dictionary<int, bool>();

        private static Type _flareIntensityType;
        private static readonly Dictionary<int, bool> _flareIntensityDefaults = new Dictionary<int, bool>();

        private static readonly Dictionary<int, bool> _artifactRendererDefaults = new Dictionary<int, bool>();

        private static void EnsureHeadlightsRefs(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var t = instance.GetType();
            if (_headlightsRuntimeType == t)
            {
                return;
            }

            _headlightsRuntimeType = t;
            _headlightsHeadLightsField = AccessTools.Field(t, "headLights");
            _headlightsCarMatField = AccessTools.Field(t, "carMat");
            _headlightsEmissiveRegularField = AccessTools.Field(t, "emissiveRegular");
            _headlightsEmissiveBreakingField = AccessTools.Field(t, "emissiveBreaking");
            _headlightsHeadlightsOnField = AccessTools.Field(t, "headlightsOn");
            _headlightsModelField = AccessTools.Field(t, "model");
        }

        private static void EnsureLensFlareRefs()
        {
            if (_lensFlareSrpType == null)
            {
                _lensFlareSrpType = AccessTools.TypeByName("UnityEngine.Rendering.LensFlareComponentSRP");
            }
        }

        private static void EnsureFlareIntensityRefs()
        {
            if (_flareIntensityType == null)
            {
                _flareIntensityType = AccessTools.TypeByName("flareIntensity");
            }
        }

        private static bool LooksLikeHeadlightArtifactRenderer(Renderer r)
        {
            if (r == null)
            {
                return false;
            }

            string n = r.gameObject != null ? r.gameObject.name : string.Empty;
            if (!string.IsNullOrEmpty(n))
            {
                // Exclude obvious rear/indicator lighting.
                if (n.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("brake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("indicator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }

                if (n.IndexOf("flare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("headlight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            try
            {
                var mat = r.sharedMaterial;
                if (mat != null)
                {
                    string mn = mat.name;
                    if (!string.IsNullOrEmpty(mn))
                    {
                        if (mn.IndexOf("tail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("brake", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("indicator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("signal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("turn", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return false;
                        }

                        if (mn.IndexOf("flare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("headlight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            mn.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }

                    if (mat.shader != null)
                    {
                        string sn = mat.shader.name;
                        if (!string.IsNullOrEmpty(sn))
                        {
                            if (sn.IndexOf("flare", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                sn.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                sn.IndexOf("headlight", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                        if (!string.IsNullOrEmpty(sn) && sn.IndexOf("additive", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private static void SetHeadlightArtifactsEnabled(GameObject root, bool enabled, bool restoreDefaults)
        {
            if (root == null)
            {
                return;
            }

            // Lens flare SRP.
            SetLensFlaresEnabled(root, enabled, restoreDefaults);

            // flareIntensity (drives lens flare intensity off Light intensity).
            EnsureFlareIntensityRefs();
            if (_flareIntensityType != null)
            {
                Component[] comps;
                try
                {
                    comps = root.GetComponentsInChildren(_flareIntensityType, true);
                }
                catch
                {
                    comps = null;
                }

                if (comps != null)
                {
                    for (int i = 0; i < comps.Length; i++)
                    {
                        var c = comps[i] as Behaviour;
                        if (c == null)
                        {
                            continue;
                        }
                        int id = c.GetInstanceID();
                        if (!_flareIntensityDefaults.ContainsKey(id))
                        {
                            _flareIntensityDefaults[id] = c.enabled;
                        }
                        c.enabled = enabled ? (restoreDefaults ? _flareIntensityDefaults[id] : true) : false;
                    }
                }
            }

            // Any obvious flare sprites/quads.
            Renderer[] renderers;
            try
            {
                renderers = root.GetComponentsInChildren<Renderer>(true);
            }
            catch
            {
                renderers = null;
            }

            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!LooksLikeHeadlightArtifactRenderer(r))
                {
                    continue;
                }
                int id = r.GetInstanceID();
                if (!_artifactRendererDefaults.ContainsKey(id))
                {
                    _artifactRendererDefaults[id] = r.enabled;
                }
                r.enabled = enabled ? (restoreDefaults ? _artifactRendererDefaults[id] : true) : false;
            }
        }

        private static void SetLensFlaresEnabled(GameObject root, bool enabled, bool restoreDefaults)
        {
            if (root == null)
            {
                return;
            }

            EnsureLensFlareRefs();
            if (_lensFlareSrpType == null)
            {
                return;
            }

            Component[] flares;
            try
            {
                flares = root.GetComponentsInChildren(_lensFlareSrpType, true);
            }
            catch
            {
                return;
            }

            if (flares == null || flares.Length == 0)
            {
                return;
            }

            for (int i = 0; i < flares.Length; i++)
            {
                var c = flares[i];
                if (c == null)
                {
                    continue;
                }

                int id = c.GetInstanceID();
                bool current = true;
                bool can = false;

                var b = c as Behaviour;
                if (b != null)
                {
                    can = true;
                    current = b.enabled;
                    if (!_lensFlareSrpDefaults.ContainsKey(id))
                    {
                        _lensFlareSrpDefaults[id] = current;
                    }
                    b.enabled = enabled ? (restoreDefaults ? _lensFlareSrpDefaults[id] : true) : false;
                }
                else
                {
                    // Fallback via reflection.
                    var p = AccessTools.Property(c.GetType(), "enabled");
                    if (p != null)
                    {
                        try
                        {
                            can = true;
                            current = (bool)p.GetValue(c, null);
                            if (!_lensFlareSrpDefaults.ContainsKey(id))
                            {
                                _lensFlareSrpDefaults[id] = current;
                            }
                            p.SetValue(c, enabled ? (restoreDefaults ? _lensFlareSrpDefaults[id] : true) : false, null);
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }

                if (_debugLogging != null && _debugLogging.Value && can)
                {
                    if (Time.unscaledTime - _ignitionEnforceLastLogTime > 1.0f)
                    {
                        // piggyback the existing throttle log gate to avoid spam
                        _log?.LogInfo("Lens flare SRP: " + (enabled ? "restore" : "disable") + " on " + root.name);
                    }
                }
            }
        }

        private static Texture2D GetIgnitionBlackEmissionTex()
        {
            if (_ignitionBlackEmissiveTex != null)
            {
                return _ignitionBlackEmissiveTex;
            }

            _ignitionBlackEmissiveTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _ignitionBlackEmissiveTex.SetPixels(new[] { Color.black, Color.black, Color.black, Color.black });
            _ignitionBlackEmissiveTex.Apply(false, true);
            _ignitionBlackEmissiveTex.hideFlags = HideFlags.HideAndDontSave;
            return _ignitionBlackEmissiveTex;
        }

        private static void ForceVehicleLightsOff(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            // Disable the headlight GameObject (actual Light components).
            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go != null && go.activeSelf)
            {
                go.SetActive(false);
            }

            // Disable any lens flare components that may be on the model (not always a child of headLights).
            var model = _headlightsModelField != null ? _headlightsModelField.GetValue(hl) as GameObject : null;
            SetHeadlightArtifactsEnabled(go, enabled: false, restoreDefaults: false);
            SetHeadlightArtifactsEnabled(model, enabled: false, restoreDefaults: false);
            SetHeadlightArtifactsEnabled(car.gameObject, enabled: false, restoreDefaults: false);

            // Keep the public state consistent.
            try
            {
                hl.headlightsOn = false;
            }
            catch
            {
                // ignore
            }

            // Force taillight/brake emissive off.
            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            if (mat != null)
            {
                mat.SetTexture("_EmissionMap", GetIgnitionBlackEmissionTex());
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        private static void RestoreVehicleLightState(sCarController car, bool wantOn)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go != null)
            {
                // Silent restore (no click).
                go.SetActive(wantOn);
            }
            try
            {
                hl.headlightsOn = wantOn;
            }
            catch
            {
                // ignore
            }

            // Restore emission to regular (Brake() will override when braking).
            var mat = _headlightsCarMatField != null ? _headlightsCarMatField.GetValue(hl) as Material : null;
            var regular = _headlightsEmissiveRegularField != null ? _headlightsEmissiveRegularField.GetValue(hl) as Texture : null;
            if (mat != null && regular != null)
            {
                mat.SetTexture("_EmissionMap", regular);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.white);
                }
            }

            // Restore/disable lens flare components to match the headlight state.
            var model = _headlightsModelField != null ? _headlightsModelField.GetValue(hl) as GameObject : null;
            if (wantOn)
            {
                SetHeadlightArtifactsEnabled(go, enabled: true, restoreDefaults: true);
                SetHeadlightArtifactsEnabled(model, enabled: true, restoreDefaults: true);
                SetHeadlightArtifactsEnabled(car.gameObject, enabled: true, restoreDefaults: true);
            }
            else
            {
                SetHeadlightArtifactsEnabled(go, enabled: false, restoreDefaults: false);
                SetHeadlightArtifactsEnabled(model, enabled: false, restoreDefaults: false);
                SetHeadlightArtifactsEnabled(car.gameObject, enabled: false, restoreDefaults: false);
            }
        }

        private static void ApplyHeadlightTuning(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            var hl = car.headlights;
            if (hl == null)
            {
                return;
            }

            EnsureHeadlightsRefs(hl);

            var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
            if (go == null)
            {
                return;
            }

            float intenMul = GetHeadlightIntensityMult();
            float rangeMul = GetHeadlightRangeMult();

            // If ignition is off, clamp to zero so any stray enabled Light is dark.
            if (!GetIgnitionEnabledEffective())
            {
                intenMul = 0f;
                rangeMul = 0f;
            }

            var lights = go.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null)
                {
                    continue;
                }
                int id = l.GetInstanceID();
                if (!_lightDefaults.TryGetValue(id, out var d))
                {
                    d = (l.intensity, l.range);
                    _lightDefaults[id] = d;
                }
                l.intensity = d.intensity * intenMul;
                l.range = d.range * rangeMul;
            }
        }

        private static void ApplySpeedScaleTuning(sCarController car)
        {
            if (car == null)
            {
                return;
            }

            int id = car.GetInstanceID();
            if (!_carScaleDefaults.ContainsKey(id))
            {
                _carScaleDefaults[id] = (car.maxSpeedScale, car.drivePowerScale);
            }
            var d = _carScaleDefaults[id];

            // Always restore in on-foot mode.
            if (car.GuyActive)
            {
                car.maxSpeedScale = d.maxSpeedScale;
                car.drivePowerScale = d.drivePowerScale;
                return;
            }

            float mult;
            if (GetManualTransmissionEnabled())
            {
                mult = _manualGear < 0 ? GetManualSpeedMultReverse() : GetManualSpeedMultForward();
            }
            else
            {
                float dir = 0f;
                try
                {
                    if (car.player >= 0 && car.player < sInputManager.players.Length)
                    {
                        dir = sInputManager.players[car.player].driveInput.y;
                    }
                }
                catch
                {
                    dir = 0f;
                }
                mult = dir < 0f ? GetManualSpeedMultReverse() : GetManualSpeedMultForward();
            }

            car.maxSpeedScale = d.maxSpeedScale * mult;
            car.drivePowerScale = d.drivePowerScale;
        }

        private static bool Headlights_Toggle_Prefix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return true;
            }

            // When ignition is off, don't allow the headlights to be toggled on.
            if (!GetIgnitionEnabledEffective())
            {
                return false;
            }
            return true;
        }

        private static void Headlights_Toggle_Postfix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return;
            }

            // Vanilla leaves lens flare on sometimes; sync it to the actual headlight state.
            try
            {
                var hl = __instance as Headlights;
                if (hl == null)
                {
                    return;
                }
                EnsureHeadlightsRefs(hl);

                var go = _headlightsHeadLightsField != null ? _headlightsHeadLightsField.GetValue(hl) as GameObject : null;
                bool on = go != null && go.activeSelf;
                var model = _headlightsModelField != null ? _headlightsModelField.GetValue(hl) as GameObject : null;
                var car = hl.GetComponentInParent<sCarController>();

                if (on)
                {
                    SetHeadlightArtifactsEnabled(go, enabled: true, restoreDefaults: true);
                    SetHeadlightArtifactsEnabled(model, enabled: true, restoreDefaults: true);
                    if (car != null) SetHeadlightArtifactsEnabled(car.gameObject, enabled: true, restoreDefaults: true);
                }
                else
                {
                    SetHeadlightArtifactsEnabled(go, enabled: false, restoreDefaults: false);
                    SetHeadlightArtifactsEnabled(model, enabled: false, restoreDefaults: false);
                    if (car != null) SetHeadlightArtifactsEnabled(car.gameObject, enabled: false, restoreDefaults: false);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static bool Headlights_Break_Prefix(object __instance)
        {
            if (!ShouldApply() || __instance == null)
            {
                return true;
            }

            // When ignition is off, prevent taillight/brake emissive updates.
            if (!GetIgnitionEnabledEffective())
            {
                return false;
            }
            return true;
        }

        private static void ApplyIgnitionStateChange(bool ignitionOn)
        {
            var car = _currentCar;
            if (car == null || car.GuyActive)
            {
                if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogInfo("Ignition state change ignored (no active car)");
                }
                return;
            }

            var hl = car.headlights;
            var radio = sRadioSystem.instance;
            bool radioIsForCar = radio != null && ReferenceEquals(radio.car, car);

            if (!ignitionOn)
            {
                // Capture current accessory state so we can restore it.
                _ignitionPrevHeadlightsOn = hl != null && hl.headlightsOn;
                _ignitionPrevRadioOn = radioIsForCar && radio.source != null && radio.source.enabled;

                PlayIgnitionSfx(false, car);

                // Force off.
                ForceVehicleLightsOff(car);
                if (radioIsForCar && radio.source != null && radio.source.enabled)
                {
                    radio.ToggleRadio();
                }

                if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogInfo("Ignition OFF: throttle disabled, engine muted, fuel paused, lights/radio forced off");
                }
                return;
            }

            if (_suppressNextIgnitionOnSfx)
            {
                _suppressNextIgnitionOnSfx = false;
            }
            else
            {
                PlayIgnitionSfx(true, car);
            }

            // Restore previous accessory state on ignition on.
            RestoreVehicleLightState(car, _ignitionPrevHeadlightsOn);
            if (radioIsForCar && radio.source != null && _ignitionPrevRadioOn && !radio.source.enabled)
            {
                radio.ToggleRadio();
            }

            // Reset to 1st gear when the vehicle turns back on.
            if (GetManualTransmissionEnabled())
            {
                _manualGear = 1;
            }

            if (_debugLogging != null && _debugLogging.Value)
            {
                _log?.LogInfo("Ignition ON: restoring lights=" + _ignitionPrevHeadlightsOn + " radio=" + _ignitionPrevRadioOn);
            }
        }

        private static void PlayIgnitionSfx(bool ignitionOn, sCarController car)
        {
            if (car == null)
            {
                return;
            }

            if (!GetIgnitionSfxEnabled())
            {
                return;
            }

            float vol = _ignitionSfxVolume != null ? Mathf.Clamp01(_ignitionSfxVolume.Value) : 0.6f;
            if (vol <= 0.001f)
            {
                return;
            }

            UnityEngine.Object clip = ignitionOn ? _ignitionSfxOn : _ignitionSfxOff;
            if (clip == null)
            {
                if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogInfo("Ignition SFX missing for " + (ignitionOn ? "ON" : "OFF"));
                }
                return;
            }

            if (car.headlights != null)
            {
                // Headlights.PlaySound(AudioClip clip, float volume)
                Type audioClipType = AccessTools.TypeByName("UnityEngine.AudioClip");
                var m = audioClipType != null
                    ? AccessTools.Method(car.headlights.GetType(), "PlaySound", new[] { audioClipType, typeof(float) })
                    : null;
                if (m != null)
                {
                    m.Invoke(car.headlights, new object[] { clip, vol });
                    if (_debugLogging != null && _debugLogging.Value)
                    {
                        _log?.LogInfo("Ignition SFX played via Headlights (" + (ignitionOn ? "ON" : "OFF") + ")");
                    }
                }
                return;
            }
        }

        private static void StartIgnitionHoldSfx(sCarController car)
        {
            if (car == null || car.GuyActive)
            {
                return;
            }

            if (!GetIgnitionSfxEnabled())
            {
                return;
            }

            if (_ignitionSfxOn == null)
            {
                if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogInfo("Ignition hold SFX skipped: ignition_on.wav not loaded");
                }
                return;
            }

            float vol = _ignitionSfxVolume != null ? Mathf.Clamp01(_ignitionSfxVolume.Value) : 0.6f;
            if (vol <= 0.001f)
            {
                return;
            }

            if (_ignitionHoldSfxGo == null)
            {
                _ignitionHoldSfxGo = new GameObject("IgnitionHoldSFX");
                _ignitionHoldSfxGo.hideFlags = HideFlags.HideAndDontSave;
                _ignitionHoldSfxSource = _ignitionHoldSfxGo.AddComponent<AudioSource>();
                _ignitionHoldSfxSource.loop = true;
                _ignitionHoldSfxSource.playOnAwake = false;
                _ignitionHoldSfxSource.spatialBlend = 1f;
                _ignitionHoldSfxSource.dopplerLevel = 0f;
                _ignitionHoldSfxSource.rolloffMode = AudioRolloffMode.Linear;
                _ignitionHoldSfxSource.minDistance = 5f;
                _ignitionHoldSfxSource.maxDistance = 40f;
                _ignitionHoldSfxSource.spread = 0f;

                // Route through the game's SFX mixer if available.
                try
                {
                    if (PauseSystem.pauseSystem != null && PauseSystem.pauseSystem.masterMix != null)
                    {
                        var groups = PauseSystem.pauseSystem.masterMix.FindMatchingGroups("SFX");
                        if (groups != null && groups.Length > 0)
                        {
                            _ignitionHoldSfxSource.outputAudioMixerGroup = groups[0];
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // Emit from the vehicle (follow the car).
            if (_ignitionHoldSfxGo.transform.parent != car.transform)
            {
                _ignitionHoldSfxGo.transform.SetParent(car.transform, false);
            }
            _ignitionHoldSfxGo.transform.localPosition = Vector3.zero;
            _ignitionHoldSfxSource.volume = vol;

            // Set clip via reflection so we don't need a hard AudioClip reference.
            try
            {
                var clipProp = AccessTools.Property(_ignitionHoldSfxSource.GetType(), "clip");
                if (clipProp != null)
                {
                    clipProp.SetValue(_ignitionHoldSfxSource, _ignitionSfxOn, null);
                }
                else if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogWarning("Ignition hold SFX: AudioSource.clip property not found");
                }
            }
            catch (Exception e)
            {
                if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogWarning("Ignition hold SFX: failed to set clip (" + e.Message + ")");
                }
            }

            if (!_ignitionHoldSfxSource.isPlaying)
            {
                try
                {
                    _ignitionHoldSfxSource.Play();
                }
                catch (Exception e)
                {
                    if (_debugLogging != null && _debugLogging.Value)
                    {
                        _log?.LogWarning("Ignition hold SFX: Play() failed (" + e.Message + ")");
                    }
                }
            }

            // Don't double-play the ON sound when the toggle completes.
            _suppressNextIgnitionOnSfx = true;

            if (_debugLogging != null && _debugLogging.Value)
            {
                float len = -1f;
                try
                {
                    var lenProp = _ignitionSfxOn.GetType().GetProperty("length");
                    if (lenProp != null)
                    {
                        len = (float)lenProp.GetValue(_ignitionSfxOn, null);
                    }
                }
                catch
                {
                    // ignore
                }
                _log?.LogInfo("Ignition hold SFX started (playing=" + _ignitionHoldSfxSource.isPlaying + ", len=" + len.ToString("0.00") + "s)");
            }
        }

        private static void StopIgnitionHoldSfx()
        {
            if (_ignitionHoldSfxSource != null && _ignitionHoldSfxSource.isPlaying)
            {
                _ignitionHoldSfxSource.Stop();
                if (_debugLogging != null && _debugLogging.Value)
                {
                    _log?.LogInfo("Ignition hold SFX stopped");
                }
            }
            _suppressNextIgnitionOnSfx = false;
        }

        private static void EnforceIgnitionOffForCurrentCar()
        {
            var car = _currentCar;
            if (car == null || car.GuyActive)
            {
                return;
            }

            // Prevent game input from turning accessories back on.
            if (car.player >= 0 && car.player < sInputManager.players.Length)
            {
                sInputManager.players[car.player].headlightsPressed = false;
                sInputManager.players[car.player].radioPressed = false;
                sInputManager.players[car.player].radioInput = Vector2.zero;
            }

            // Force lights off.
            ForceVehicleLightsOff(car);

            // Force radio off.
            var radio = sRadioSystem.instance;
            if (radio != null && ReferenceEquals(radio.car, car) && radio.source != null && radio.source.enabled)
            {
                radio.ToggleRadio();
            }

            if (_debugLogging != null && _debugLogging.Value)
            {
                if (Time.unscaledTime - _ignitionEnforceLastLogTime > 1.0f)
                {
                    _ignitionEnforceLastLogTime = Time.unscaledTime;
                    _log?.LogInfo("Ignition OFF enforced (blocking accessory inputs)");
                }
            }
        }

        private static float _ignitionEnforceLastLogTime = -999f;

        private static float _ignitionBindMissingLastLogTime = -999f;

        private static readonly Dictionary<int, (float maxSpeedScale, float drivePowerScale)> _carScaleDefaults =
            new Dictionary<int, (float maxSpeedScale, float drivePowerScale)>();



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

            ApplyHeadlightTuning(car);
            ApplySpeedScaleTuning(car);

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

            bool ignitionFeature = GetIgnitionFeatureEnabled();
            bool ignOn = GetIgnitionEnabledEffective();
            bool starting = ignitionFeature && !GetIgnitionEnabled() && _ignitionHoldStart >= 0f && !_ignitionHoldConsumed;

            // Hide readouts while the vehicle is off.
            bool showSpeed = ignOn && GetHudShowSpeed();
            bool manualEnabled = GetManualTransmissionEnabled();
            bool showTach = ignOn && manualEnabled && GetHudShowTach();
            bool showGear = ignOn && manualEnabled && GetHudShowGear();

            bool showOff = ignitionFeature && !ignOn && !starting;
            if (!starting && !showOff && !showSpeed && !showTach && !showGear)
            {
                return;
            }

            // Default anchors:
            // - Bottom L: under money/time
            // - Bottom R: under radio
            float xLeft = 68f;
            float xRight = R.width - 68f;
            float yLeft = R.height - 64f + 22f;
            float yRight = yLeft;

            var aSpeed = GetHudSpeedAnchor();
            var aTach = GetHudTachAnchor();
            var aGear = GetHudGearAnchor();

            void PutLine(HudReadoutAnchor a, string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }
                if (a == HudReadoutAnchor.BottomRight)
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
                    R.fput(text, xRight, yRight);
                    yRight += 10f;
                }
                else
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(text, xLeft, yLeft);
                    yLeft += 10f;
                }
            }

            if (starting)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - _ignitionHoldStart) / IgnitionHoldSeconds);
                PutLine(aSpeed, "START " + Mathf.RoundToInt(t * 100f) + "%");
            }
            else if (showOff)
            {
                PutLine(aSpeed, "ENGN OFF");
            }


            if (showSpeed)
            {
                float spd = ConvertSpeedForHud(_currentSpeedKmh);
                int spdInt = Mathf.Max(0, Mathf.RoundToInt(spd));
                string unit = GetHudSpeedUnitLabel(GetHudSpeedUnit());
                PutLine(aSpeed, $"{spdInt}{unit}");
            }
            if (showTach)
            {
                float target = GetEstimatedRpm();
                if (_rpmHudSmooth <= 0f)
                {
                    _rpmHudSmooth = target;
                }
                _rpmHudSmooth = Mathf.Lerp(_rpmHudSmooth, target, Time.unscaledDeltaTime * 8f);

                float rpmNorm = Mathf.Clamp(GetEstimatedRpmNormForSound(), 0f, 1.2f);
                int gNow = GetManualGear();
                bool lastGear = gNow > 0 && gNow == GetManualGearCount();
                string suffix = lastGear ? "" : (rpmNorm >= 1.0f ? "!!" : (rpmNorm >= 0.92f ? "!" : ""));
                PutLine(aTach, $"{Mathf.RoundToInt(_rpmHudSmooth)}rpm{suffix}");
            }
            if (showGear)
            {
                // Two-line selector:
                // RN12345
                //      ^
                int g = GetManualGear();
                int gearCount = GetManualGearCount();

                // Line 1
                string line = "RN";
                for (int i = 1; i <= gearCount; i++)
                {
                    line += i.ToString();
                }
                if (aGear == HudReadoutAnchor.BottomRight)
                {
                    // Keep the selector monospaced and right-aligned to the radio block.
                    float lineXLeft = xRight - 8f * line.Length;
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(line, lineXLeft, yRight);

                    // Pointer under selected gear.
                    int selIndex = g < 0 ? 0 : (g == 0 ? 1 : Mathf.Clamp(g, 1, gearCount) + 1);
                    yRight += 10f;
                    R.put("^", lineXLeft + 8f * selIndex, yRight);
                    yRight += 10f;
                }
                else
                {
                    R.fontOptions.alignment = sFancyText.FontOptions.Alignment.left;
                    R.put(line, xLeft, yLeft);

                    // Pointer under selected gear.
                    int selIndex = g < 0 ? 0 : (g == 0 ? 1 : Mathf.Clamp(g, 1, gearCount) + 1);
                    yLeft += 10f;
                    R.put("^", xLeft + 8f * selIndex, yLeft);
                    yLeft += 10f;
                }
            }
        }

        private static float _rpmHudSmooth;


        private static Type _engineSfxRuntimeType;
        private static System.Reflection.FieldInfo _engineCarField;
        private static System.Reflection.FieldInfo _engineIdleField;
        private static System.Reflection.FieldInfo _engineDriveField;
        private static System.Reflection.FieldInfo _engineIntenseField;
        private static System.Reflection.FieldInfo _engineDistortionField;

        private static readonly Dictionary<int, float> _enginePitchMulApplied = new Dictionary<int, float>();

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
            if (!ShouldApply() || __instance == null)
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

            // Ignition off: hard-mute engine loops (works for both Auto + Manual).
            if (!GetIgnitionEnabledEffective())
            {
                var idle0 = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as AudioSource : null;
                var drive0 = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as AudioSource : null;
                var intense0 = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as AudioSource : null;
                if (idle0 != null) idle0.volume = 0f;
                if (drive0 != null) drive0.volume = 0f;
                if (intense0 != null) intense0.volume = 0f;

                var dist0 = _engineDistortionField != null ? _engineDistortionField.GetValue(__instance) as AudioDistortionFilter : null;
                if (dist0 != null) dist0.distortionLevel = 0f;
                return;
            }

            // Manual-mode-only sound tweaks.
            if (!GetManualTransmissionEnabled())
            {
                return;
            }

            float rpmNorm = GetEstimatedRpmNormForSound();
            float over = Mathf.Clamp01((rpmNorm - 1f) / 0.2f);
            float neutral = (_manualGear == 0) ? Mathf.Clamp01(_neutralRev01) : 0f;

            // Warning ramp for high RPM (aligns with HUD suffix thresholds).
            float warn = Mathf.Clamp01((rpmNorm - 0.92f) / (1.0f - 0.92f));

            // Apply pitch boost to all loops so revving in Neutral is audible.
            float pitchMul = 1f + neutral * 0.85f + over * 0.35f;

            var idle = _engineIdleField != null ? _engineIdleField.GetValue(__instance) as AudioSource : null;
            var drive = _engineDriveField != null ? _engineDriveField.GetValue(__instance) as AudioSource : null;
            var intense = _engineIntenseField != null ? _engineIntenseField.GetValue(__instance) as AudioSource : null;

            void ApplyPitchMul(AudioSource src)
            {
                if (src == null)
                {
                    return;
                }
                int id = src.GetInstanceID();
                float last = 1f;
                if (_enginePitchMulApplied.TryGetValue(id, out float prev) && prev > 0.0001f)
                {
                    last = prev;
                }
                // Avoid compounding multipliers frame-to-frame.
                src.pitch = (src.pitch / last) * pitchMul;
                _enginePitchMulApplied[id] = pitchMul;
            }

            ApplyPitchMul(idle);
            ApplyPitchMul(drive);
            ApplyPitchMul(intense);

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

            // Overrev: ramp up the same "revving" feel as Neutral (warn -> !, over -> !!).
            // This is intentionally additive to the base blend so you can hear the engine "climb" even before hard overrev.
            if (warn > 0.01f)
            {
                float driveBoost = Mathf.Min(1f, warn * 0.35f + over * 0.45f);
                float intenseBoost = Mathf.Min(1f, warn * 0.22f + over * 0.35f);
                if (drive != null)
                {
                    drive.volume = Mathf.Max(drive.volume, driveBoost);
                }
                if (intense != null)
                {
                    intense.volume = Mathf.Max(intense.volume, intenseBoost);
                }
            }

            // Over-rev: make it sound like a strained neutral rev with sputter.
            if (over > 0.01f)
            {
                // Sawtooth stutter: repeated ramp-down + snap-back.
                float freq = Mathf.Lerp(7f, 14f, over);
                float saw = Mathf.Repeat(Time.time * freq, 1f);
                float ramp = 1f - saw; // 1..0
                float cut = (saw < 0.12f) ? Mathf.Lerp(0.25f, 1f, saw / 0.12f) : 1f;

                float baseMul = Mathf.Lerp(1.0f, 0.72f, over);
                float stutterMul = Mathf.Lerp(1.0f, ramp, over) * cut;
                float volMul = baseMul * stutterMul;
                float pitchJitter = 1f + (Mathf.Sin(Time.time * 55f) * 0.012f * over);

                if (drive != null)
                {
                    drive.volume *= volMul;
                    drive.pitch *= pitchJitter;
                }
                if (intense != null)
                {
                    intense.volume *= volMul;
                    intense.pitch *= pitchJitter;
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
