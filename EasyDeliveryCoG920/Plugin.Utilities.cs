using System;
using System.Reflection;
using HarmonyLib;

namespace EasyDeliveryCoG920
{
    public partial class Plugin
    {
        private static void PatchByName(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                LogDebug($"Type '{typeName}' not found for patch {methodName}.");
                return;
            }

            MethodInfo method;
            try
            {
                method = AccessTools.Method(type, methodName);
            }
            catch (AmbiguousMatchException)
            {
                method = ResolveAmbiguousMethod(type, methodName);
            }

            if (method == null)
            {
                LogDebug($"Method '{typeName}.{methodName}' not found for patch.");
                return;
            }

            HarmonyMethod prefixMethod = null;
            HarmonyMethod postfixMethod = null;

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                prefixMethod = new HarmonyMethod(typeof(Plugin), prefix);
            }

            if (!string.IsNullOrWhiteSpace(postfix))
            {
                postfixMethod = new HarmonyMethod(typeof(Plugin), postfix);
            }

            harmony.Patch(method, prefixMethod, postfixMethod);
        }

        private static MethodInfo ResolveAmbiguousMethod(Type type, string methodName)
        {
            MethodInfo best = null;
            int bestParamCount = int.MaxValue;
            for (var current = type; current != null; current = current.BaseType)
            {
                var methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var candidate in methods)
                {
                    if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int paramCount = candidate.GetParameters().Length;
                    if (paramCount == 0)
                    {
                        return candidate;
                    }

                    if (paramCount < bestParamCount)
                    {
                        bestParamCount = paramCount;
                        best = candidate;
                    }
                }
            }

            return best;
        }
    }
}
