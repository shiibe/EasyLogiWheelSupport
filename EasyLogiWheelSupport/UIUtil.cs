using UnityEngine;

namespace EasyLogiWheelSupport
{
    public class UIUtil
    {
        public DesktopDotExe M;
        public GamepadNavigation Nav;
        public MiniRenderer R;

        public void Label(string name, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.center;
            R.fput(name, x, y);
        }

        public void ValueLabel(string value, float x, float y)
        {
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(value, x, y);
        }

        public bool Button(string name, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            if (M.MouseOver((int)x - 2, (int)y, name.Length * 8 + 4, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return false;
        }

        public bool SimpleButton(string name, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            if (M.MouseOver((int)x - 32, (int)y, 64, 8))
            {
                M.mouseIcon = 128;
                x += 4f;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.center;
            R.fput(name, x, y);
            return false;
        }

        public bool SimpleButtonRaw(string name, float x, float y)
        {
            if (M.MouseOver((int)x - 32, (int)y, 64, 8))
            {
                M.mouseIcon = 128;
                x += 4f;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.center;
            R.fput(name, x, y);
            return false;
        }

        public bool FancyButton(string title, float centerX, float y)
        {
            title = LocalizationDictionary.Translate(title);
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.center;

            // Measure title width (in pixels).
            float textWidthPx = R.fput(title, centerX, -32f, 0f, 13f, 0f, -1);
            float wTiles = textWidthPx / 8f + 1f;

            // Tile drawing is offset by +4px (half-tile), so shift the pixel center
            // back by 4px when converting to tile coordinates.
            float tileXCenter = (centerX - 4f) / 8f;
            float tileY = y / 8f;

            bool hovered = M.MouseOver((int)((tileXCenter - wTiles / 2f) * 8f + 4f), (int)(tileY * 8f), (int)((wTiles + 1f) * 8f), 24);
            if (hovered)
            {
                M.mouseIcon = 128;
                wTiles += 2f;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                    wTiles -= 2f;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }

            // Text (pixel-centered).
            R.fput(title, tileXCenter * 8f + 4f, tileY * 8f + 8f, 0f, 13f, 0f, -1);
            M.drawBox(tileXCenter - wTiles / 2f, tileY, wTiles, 2f);
            return false;
        }

        public bool CycleButton(string name, string value, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            string text = "[" + value + "]";
            R.put(text, x + 4f, y);
            if (M.MouseOver((int)x - 2, (int)y, text.Length * 8 + 4, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return false;
        }

        public bool CycleButtonRaw(string name, string value, float x, float y)
        {
            string text = "[" + value + "]";
            R.put(text, x + 4f, y);
            if (M.MouseOver((int)x - 2, (int)y, text.Length * 8 + 4, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    return true;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return false;
        }

        public bool? Toggle(string name, bool state, float x, float y)
        {
            name = LocalizationDictionary.Translate(name);
            bool? result = null;
            string text = "[" + (state ? "on" : "off") + "]";
            R.put(text, x + 4f, y);
            if (M.MouseOver((int)x - 2, (int)y, text.Length * 8 + 4, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                }
                if (M.mouseButtonUp)
                {
                    result = !state;
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return result;
        }

        public float? Slider(string name, float value, float x, float y, ref float mouseYLock)
        {
            name = LocalizationDictionary.Translate(name);
            float? result = null;
            int num = 10;
            for (int i = 0; i < num; i++)
            {
                R.spr(32f, 0f, x + 4f + i * 8f, y, 8f, 8f);
            }
            float x2 = x + value * num * 8f;
            R.spr(0f, 24f, x2, y, 8f, 8f);
            if (M.MouseOver((int)x - 8, (int)y, num * 8 + 16, 8))
            {
                M.mouseIcon = 128;
                name = ">" + name;
                if (Nav is not null && Nav.menuInput.x < 0f)
                {
                    result = Mathf.Clamp01(value - Time.unscaledDeltaTime / 2f);
                }
                if (Nav is not null && Nav.menuInput.x > 0f)
                {
                    result = Mathf.Clamp01(value + Time.unscaledDeltaTime / 2f);
                }
                if (M.mouseButton)
                {
                    M.mouseIcon = 160;
                    value = Mathf.InverseLerp(x + 4f, x + 4f + num * 8f, M.mouse.x);
                    value = Mathf.Clamp01(value);
                    result = value;
                    if (mouseYLock == 0f)
                    {
                        mouseYLock = M.mouse.y;
                    }
                }
            }
            R.fontOptions.alignment = sFancyText.FontOptions.Alignment.right;
            R.fput(name, x - 4f, y);
            return result;
        }
    }
}
