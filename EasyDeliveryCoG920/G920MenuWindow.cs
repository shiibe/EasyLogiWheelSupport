using UnityEngine;

namespace EasyDeliveryCoG920
{
    public class G920MenuWindow : MonoBehaviour
    {
        public const string FileName = "wheel";
        public const string ListenerName = "G920Menu";
        public const string ListenerData = "listener_G920Menu";

        private float _mouseYLock;
        private UIUtil _util;

        private Page _page;
        private CalStep _calStep;

        private enum CalStep
        {
            None = 0,
            SteeringCenter = 1,
            SteeringLeft = 2,
            SteeringRight = 3,
            ThrottleReleased = 4,
            ThrottlePressed = 5,
            BrakeReleased = 6,
            BrakePressed = 7
        }

        private enum Page
        {
            Main = 0,
            Ffb = 1,
            Steering = 2,
            Calibration = 3,
            Bindings = 4
        }

        public void FrameUpdate(DesktopDotExe.WindowView view)
        {
            if (view == null)
            {
                return;
            }

            _util ??= new UIUtil();

            _util.M = view.M;
            _util.R = view.R;
            _util.Nav = view.M.nav;

            Rect p = new Rect(view.position * 8f, view.size * 8f);
            p.position += new Vector2(8f, 8f);

            if (_util.M.mouseButtonUp)
            {
                _mouseYLock = 0f;
            }

            if (_mouseYLock > 0f)
            {
                _util.M.mouse.y = _mouseYLock;
            }

            DrawMenu(p);
        }

        public void BackButtonPressed()
        {
            if (_page != Page.Main)
            {
                _calStep = CalStep.None;
                _page = Page.Main;
            }
        }

        private void DrawMenu(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            _util.Label("Wheel Settings", p.x + p.width / 2f, y);
            y += line + sectionGap;

            if (_page == Page.Main)
            {
                DrawMain(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.Ffb)
            {
                DrawFfb(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.Steering)
            {
                DrawSteering(p, center, ref y, line, sectionGap);
            }
            else if (_page == Page.Calibration)
            {
                DrawCalibration(p, center, ref y, line, sectionGap);
            }
            else
            {
                DrawBindings(p, center, ref y, line, sectionGap);
            }
        }

        private void DrawMain(Rect p, float center, ref float y, float line, float sectionGap)
        {
            _util.Label("Logitech SDK", p.x + p.width / 2f, y);
            _util.ValueLabel(Plugin.GetLogitechStatus(), p.x + p.width - 12f, y);
            y += line;

            if (_util.SimpleButton("Retry SDK", p.x + p.width / 2f, y))
            {
                Plugin.ForceReinitLogitech();
            }
            y += line + sectionGap;

            if (_util.SimpleButton("Force Feedback", p.x + p.width / 2f, y))
            {
                _page = Page.Ffb;
            }
            y += line;
            if (_util.SimpleButton("Steering", p.x + p.width / 2f, y))
            {
                _page = Page.Steering;
            }
            y += line;
            if (_util.SimpleButton("Calibration", p.x + p.width / 2f, y))
            {
                _page = Page.Calibration;
            }
            y += line;
            if (_util.SimpleButton("Bindings", p.x + p.width / 2f, y))
            {
                _page = Page.Bindings;
            }
        }

        private void DrawFfb(Rect p, float center, ref float y, float line, float sectionGap)
        {
            _util.Label("Force Feedback", p.x + p.width / 2f, y);
            y += line;
            if (_util.Button("Back", p.x + 52f, y))
            {
                _page = Page.Main;
                return;
            }
            y += line + sectionGap;

            bool ffbEnabled = Plugin.GetFfbEnabled();
            bool? newFfbEnabled = _util.Toggle("Enable FFB", ffbEnabled, center, y);
            if (newFfbEnabled.HasValue)
            {
                Plugin.SetFfbEnabled(newFfbEnabled.Value);
            }
            y += line;

            float overall = Plugin.GetFfbOverallGain();
            _util.ValueLabel($"{Mathf.RoundToInt(overall * 100f)}%", p.x + p.width - 12f, y);
            float? newOverall = _util.Slider("Strength", overall, center, y, ref _mouseYLock);
            if (newOverall.HasValue)
            {
                Plugin.SetFfbOverallGain(newOverall.Value);
            }
            y += line;

            float spring = Plugin.GetFfbSpringGain();
            _util.ValueLabel($"{Mathf.RoundToInt(spring * 100f)}%", p.x + p.width - 12f, y);
            float? newSpring = _util.Slider("Spring", spring, center, y, ref _mouseYLock);
            if (newSpring.HasValue)
            {
                Plugin.SetFfbSpringGain(newSpring.Value);
            }
            y += line;

            float damper = Plugin.GetFfbDamperGain();
            _util.ValueLabel($"{Mathf.RoundToInt(damper * 100f)}%", p.x + p.width - 12f, y);
            float? newDamper = _util.Slider("Damper", damper, center, y, ref _mouseYLock);
            if (newDamper.HasValue)
            {
                Plugin.SetFfbDamperGain(newDamper.Value);
            }
        }

        private void DrawSteering(Rect p, float center, ref float y, float line, float sectionGap)
        {
            _util.Label("Steering", p.x + p.width / 2f, y);
            y += line;
            if (_util.Button("Back", p.x + 52f, y))
            {
                _page = Page.Main;
                return;
            }
            y += line + sectionGap;

            int range = Plugin.GetWheelRange();
            _util.ValueLabel($"{range} deg", p.x + p.width - 12f, y);
            float rangeValue = Mathf.InverseLerp(180f, 900f, range);
            float? newRangeValue = _util.Slider("Range", rangeValue, center, y, ref _mouseYLock);
            if (newRangeValue.HasValue)
            {
                int newRange = Mathf.RoundToInt(Mathf.Lerp(180f, 900f, newRangeValue.Value));
                newRange = Mathf.Clamp(newRange, 180, 900);
                Plugin.SetWheelRange(newRange);
            }
            y += line;

            float steerGain = Plugin.GetSteeringGain();
            _util.ValueLabel($"{steerGain:0.00}x", p.x + p.width - 12f, y);
            float steerGainNorm = Mathf.InverseLerp(0.5f, 2.5f, steerGain);
            float? newSteerGainNorm = _util.Slider("Steer Sens", steerGainNorm, center, y, ref _mouseYLock);
            if (newSteerGainNorm.HasValue)
            {
                Plugin.SetSteeringGain(Mathf.Lerp(0.5f, 2.5f, newSteerGainNorm.Value));
            }
            y += line;

            float dz = Plugin.GetSteeringDeadzone();
            _util.ValueLabel($"{Mathf.RoundToInt(dz * 100f)}%", p.x + p.width - 12f, y);
            float dzNorm = Mathf.InverseLerp(0f, 0.12f, dz);
            float? newDzNorm = _util.Slider("Deadzone", dzNorm, center, y, ref _mouseYLock);
            if (newDzNorm.HasValue)
            {
                Plugin.SetSteeringDeadzone(Mathf.Lerp(0f, 0.12f, newDzNorm.Value));
            }
        }

        private void DrawBindings(Rect p, float center, ref float y, float line, float sectionGap)
        {
            _util.Label("Bindings", p.x + p.width / 2f, y);
            y += line;
            if (_util.Button("Back", p.x + 52f, y))
            {
                _page = Page.Main;
                return;
            }
            y += line + sectionGap;
            _util.Label("Not implemented yet.", p.x + p.width / 2f, y);
        }

        private void DrawCalibration(Rect p, float center, ref float y, float line, float sectionGap)
        {
            _util.Label("Calibration", p.x + p.width / 2f, y);
            y += line;

            if (_util.Button("Back", p.x + 52f, y))
            {
                _calStep = CalStep.None;
                _page = Page.Main;
                return;
            }
            if (_util.Button("Clear", p.x + 124f, y))
            {
                Plugin.ClearCalibration();
            }
            y += line + sectionGap;

            if (!Plugin.TryGetLogiState(out var state))
            {
                _util.Label("Wheel not detected (Logitech SDK not ready).", p.x + p.width / 2f, y);
                return;
            }

            _util.Label("Live Axes", p.x + p.width / 2f, y);
            y += line;

            // show a few common axes so it's easy to find where the throttle lives
            string axes1 = $"lX={state.lX} lY={state.lY} lZ={state.lZ}";
            string axes2 = $"lRx={state.lRx} lRy={state.lRy} lRz={state.lRz}";
            int s0 = state.rglSlider != null && state.rglSlider.Length > 0 ? state.rglSlider[0] : 0;
            int s1 = state.rglSlider != null && state.rglSlider.Length > 1 ? state.rglSlider[1] : 0;
            string axes3 = $"slider0={s0} slider1={s1}";
            _util.Label(axes1, p.x + p.width / 2f, y);
            y += line - 2f;
            _util.Label(axes2, p.x + p.width / 2f, y);
            y += line - 2f;
            _util.Label(axes3, p.x + p.width / 2f, y);
            y += line + sectionGap;

            // axis selection
            Plugin.AxisId throttleAxis = Plugin.GetThrottleAxis();
            Plugin.AxisId brakeAxis = Plugin.GetBrakeAxis();
            if (_util.CycleButton("Throttle Axis", throttleAxis.ToString(), center, y))
            {
                Plugin.SetThrottleAxis(NextAxis(throttleAxis));
                _calStep = CalStep.None;
            }
            y += line;
            if (_util.CycleButton("Brake Axis", brakeAxis.ToString(), center, y))
            {
                Plugin.SetBrakeAxis(NextAxis(brakeAxis));
                _calStep = CalStep.None;
            }
            y += line + sectionGap;

            // guided capture
            if (_calStep == CalStep.None)
            {
                if (_util.Button("Start Guided", p.x + 140f, y))
                {
                    _calStep = CalStep.SteeringCenter;
                }
                y += line;

                // quick capture helpers for pedals
                int rawThr = Plugin.GetAxisValue(state, Plugin.GetThrottleAxis());
                int rawBrk = Plugin.GetAxisValue(state, Plugin.GetBrakeAxis());
                float thr = Plugin.NormalizePedal(rawThr, Plugin.PedalKind.Throttle);
                float brk = Plugin.NormalizePedal(rawBrk, Plugin.PedalKind.Brake);
                _util.Label($"Current: thr={thr:0.00} brk={brk:0.00}", p.x + p.width / 2f, y);
                y += line;
                return;
            }

            string prompt = GetCalPrompt(_calStep);
            _util.Label(prompt, p.x + p.width / 2f, y);
            y += line;

            int rawSteer = Plugin.GetAxisValue(state, Plugin.GetSteeringAxis());
            int rawThr2 = Plugin.GetAxisValue(state, Plugin.GetThrottleAxis());
            int rawBrk2 = Plugin.GetAxisValue(state, Plugin.GetBrakeAxis());
            _util.Label($"Selected raw: steer={rawSteer} thr={rawThr2} brk={rawBrk2}", p.x + p.width / 2f, y);
            y += line;

            if (_util.Button("Capture", p.x + 120f, y))
            {
                CaptureStep(_calStep, state);
                _calStep = NextStep(_calStep);
            }
            if (_util.Button("Cancel", p.x + 180f, y))
            {
                _calStep = CalStep.None;
            }
        }

        private static Plugin.AxisId NextAxis(Plugin.AxisId axis)
        {
            int next = (int)axis + 1;
            if (next > (int)Plugin.AxisId.slider1)
            {
                next = 0;
            }
            return (Plugin.AxisId)next;
        }

        private static string GetCalPrompt(CalStep step)
        {
            switch (step)
            {
                case CalStep.SteeringCenter:
                    return "Center the wheel";
                case CalStep.SteeringLeft:
                    return "Turn fully left";
                case CalStep.SteeringRight:
                    return "Turn fully right";
                case CalStep.ThrottleReleased:
                    return "Release throttle";
                case CalStep.ThrottlePressed:
                    return "Press throttle fully";
                case CalStep.BrakeReleased:
                    return "Release brake";
                case CalStep.BrakePressed:
                    return "Press brake fully";
                default:
                    return "";
            }
        }

        private static CalStep NextStep(CalStep step)
        {
            switch (step)
            {
                case CalStep.SteeringCenter:
                    return CalStep.SteeringLeft;
                case CalStep.SteeringLeft:
                    return CalStep.SteeringRight;
                case CalStep.SteeringRight:
                    return CalStep.ThrottleReleased;
                case CalStep.ThrottleReleased:
                    return CalStep.ThrottlePressed;
                case CalStep.ThrottlePressed:
                    return CalStep.BrakeReleased;
                case CalStep.BrakeReleased:
                    return CalStep.BrakePressed;
                case CalStep.BrakePressed:
                    return CalStep.None;
                default:
                    return CalStep.None;
            }
        }

        private static void CaptureStep(CalStep step, LogitechGSDK.DIJOYSTATE2ENGINES state)
        {
            switch (step)
            {
                case CalStep.SteeringCenter:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalSteerCenter, Plugin.GetAxisValue(state, Plugin.GetSteeringAxis()));
                    break;
                case CalStep.SteeringLeft:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalSteerLeft, Plugin.GetAxisValue(state, Plugin.GetSteeringAxis()));
                    break;
                case CalStep.SteeringRight:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalSteerRight, Plugin.GetAxisValue(state, Plugin.GetSteeringAxis()));
                    break;
                case CalStep.ThrottleReleased:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalThrottleReleased, Plugin.GetAxisValue(state, Plugin.GetThrottleAxis()));
                    break;
                case CalStep.ThrottlePressed:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalThrottlePressed, Plugin.GetAxisValue(state, Plugin.GetThrottleAxis()));
                    break;
                case CalStep.BrakeReleased:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalBrakeReleased, Plugin.GetAxisValue(state, Plugin.GetBrakeAxis()));
                    break;
                case CalStep.BrakePressed:
                    PlayerPrefs.SetInt(Plugin.PrefKeyCalBrakePressed, Plugin.GetAxisValue(state, Plugin.GetBrakeAxis()));
                    break;
            }
        }
    }
}
