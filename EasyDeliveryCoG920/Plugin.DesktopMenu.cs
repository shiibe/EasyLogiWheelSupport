using System;
using System.Globalization;
using UnityEngine;

namespace EasyDeliveryCoG920
{
    public partial class Plugin
    {
        private static float ParseDesktopIconFloat(string value, float fallback, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = value.Trim();

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            if (float.TryParse(trimmed, out parsed))
            {
                return parsed;
            }

            LogDebug($"Failed to parse {label}='{value}', using {fallback:0.###}.");
            return fallback;
        }

        private static void DesktopDotExe_Setup_Postfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            var desktop = __instance as DesktopDotExe;
            if (desktop == null)
            {
                return;
            }

            bool visible = _desktopMenuIconVisible == null || _desktopMenuIconVisible.Value;
            float x = ParseDesktopIconFloat(_desktopMenuIconX != null ? _desktopMenuIconX.Value : null, 5.5f, "wheel_menu_icon_x");
            float y = ParseDesktopIconFloat(_desktopMenuIconY != null ? _desktopMenuIconY.Value : null, 3.25f, "wheel_menu_icon_y");
            var position = new Vector2(x, y);

            DesktopDotExe.File existingFile = null;
            foreach (var file in desktop.files)
            {
                if (file != null && string.Equals(file.name, G920MenuWindow.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    existingFile = file;
                    break;
                }
            }

            if (existingFile == null)
            {
                var file = new DesktopDotExe.File(desktop.R, desktop)
                {
                    name = G920MenuWindow.FileName,
                    type = DesktopDotExe.FileType.exe,
                    data = G920MenuWindow.ListenerData,
                    icon = 7,
                    iconHover = 7,
                    position = position,
                    visible = visible,
                    cantFolder = false
                };
                desktop.files.Add(file);
            }
            else
            {
                existingFile.icon = 7;
                existingFile.iconHover = 7;
                existingFile.position = position;
                existingFile.visible = visible;
            }

            var root = desktop.transform;
            if (root.Find(G920MenuWindow.ListenerName) == null)
            {
                var listener = new GameObject(G920MenuWindow.ListenerName);
                listener.transform.SetParent(root, false);
                listener.AddComponent<G920MenuWindow>();
            }
        }
    }
}
