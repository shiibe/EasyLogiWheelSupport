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
        }

        private void DrawMenu(Rect p)
        {
            float center = p.x + p.width / 2f - 16f;
            float y = p.y + 10f;
            float line = 12f;
            float sectionGap = 4f;

            _util.Label("Wheel Settings", p.x + p.width / 2f, y);
            y += line + sectionGap;

            _util.Label("Force Feedback", p.x + p.width / 2f, y);
            y += line;

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
            y += line + sectionGap;

            _util.Label("Wheel", p.x + p.width / 2f, y);
            y += line;

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
        }
    }
}
