using System;
using HarmonyLib;

namespace EasyDeliveryCoG920
{
    public partial class Plugin
    {
        // The base game applies a gamepad-tuned steeringCurve + smoothing in sCarController.
        // For wheels, we want direct, linear steering.

        [HarmonyPatch(typeof(sCarController), nameof(sCarController.SetInput), typeof(UnityEngine.Vector2))]
        private static class CarController_SetInput_Vector2_Patch
        {
            private static readonly AccessTools.FieldRef<sCarController, UnityEngine.Vector2> TargetInputRef =
                AccessTools.FieldRefAccess<sCarController, UnityEngine.Vector2>("targetInput");

            private static bool Prefix(sCarController __instance, UnityEngine.Vector2 input)
            {
                if (__instance == null)
                {
                    return true;
                }

                if (!ShouldApply() || __instance.GuyActive)
                {
                    return true;
                }

                // Only engage when our wheel override ran this frame.
                if (!TryGetWheelLastInput(out _, out _))
                {
                    return true;
                }

                var target = input;
                // Keep throttle curve for now; steering is linear.
                if (__instance.throttleCurve != null)
                {
                    target.y = __instance.throttleCurve.Evaluate(Math.Abs(target.y)) * Math.Sign(target.y);
                }

                TargetInputRef(__instance) = target;
                return false;
            }
        }

        [HarmonyPatch(typeof(sCarController), "Move")]
        private static class CarController_Move_Prefix_Patch
        {
            private static void Prefix(sCarController __instance)
            {
                if (__instance == null)
                {
                    return;
                }

                if (!ShouldApply() || __instance.GuyActive)
                {
                    return;
                }

                if (!TryGetWheelLastInput(out float steer, out float accel))
                {
                    return;
                }

                // Override right before physics uses input.
                __instance.input.x = steer;
                __instance.input.y = accel;
            }
        }
    }
}
