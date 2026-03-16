using System;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoG920
{
    public partial class Plugin
    {
        [HarmonyPatch(typeof(InteriorInteraction), "AnimateSteeringWheel")]
        private static class InteriorInteraction_AnimateSteeringWheel_Patch
        {
            private static readonly AccessTools.FieldRef<InteriorInteraction, sCarController> CarRef =
                AccessTools.FieldRefAccess<InteriorInteraction, sCarController>("car");

            private static readonly AccessTools.FieldRef<InteriorInteraction, Transform> HandPivotRef =
                AccessTools.FieldRefAccess<InteriorInteraction, Transform>("handPivot");

            private static readonly AccessTools.FieldRef<InteriorInteraction, Vector3[]> HandRestLocalPositionsRef =
                AccessTools.FieldRefAccess<InteriorInteraction, Vector3[]>("handRestLocalPositions");

            private static bool Prefix(InteriorInteraction __instance)
            {
                if (__instance == null)
                {
                    return true;
                }

                if (!ShouldApply())
                {
                    return true;
                }

                // Only override visuals when the wheel input path is active.
                if (!TryGetWheelLastInput(out _, out _))
                {
                    return true;
                }

                var car = CarRef(__instance);
                if (car == null)
                {
                    return true;
                }

                // Don't animate steering wheel while on foot.
                if (car.GuyActive)
                {
                    return true;
                }

                if (__instance.steeringWheel == null)
                {
                    return false;
                }

                // Use the wheel value directly so visuals don't inherit any car/input smoothing.
                float steer;
                if (!TryGetWheelLastInputRecent(0.10f, out steer, out _))
                {
                    steer = Mathf.Clamp(car.input.x, -1f, 1f);
                }

                // The base game applies steeringCurve again here; for wheels we want 1:1 with actual steering.
                float degrees = steer * 420f;
                __instance.steeringWheel.localEulerAngles = Vector3.up * degrees;

                if (__instance.handRest != null && __instance.hand != null)
                {
                    float t = Mathf.Clamp01((Mathf.Abs(degrees) - 30f) / 60f);
                    Vector3[] localPositions = HandRestLocalPositionsRef(__instance);
                    if (localPositions != null && localPositions.Length >= 2)
                    {
                        // Keep existing side selection behavior.
                        __instance.handRest.localPosition = localPositions[(degrees < 0f) ? 0 : 1];
                    }

                    var pivot = HandPivotRef(__instance);
                    if (pivot != null)
                    {
                        __instance.hand.transform.position = Vector3.Lerp(pivot.position, __instance.handRest.position, t);
                    }
                }

                return false;
            }
        }
    }
}
