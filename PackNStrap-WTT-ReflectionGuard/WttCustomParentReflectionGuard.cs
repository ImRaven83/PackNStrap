using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;

namespace PackNStrapWttCompat
{
    [BepInPlugin(PluginConstants.Guid, PluginConstants.Name, PluginConstants.Version)]
    [BepInDependency("com.wtt.packnstrap", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class WttCustomParentReflectionGuard : BaseUnityPlugin
    {
        private static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            try
            {
                var runtimeType = typeof(string).GetType(); // System.RuntimeType (not public, but shared by every reflected Type instance)
                var getCustomAttributes = AccessTools.Method(runtimeType, "GetCustomAttributes", new[] { typeof(Type), typeof(bool) });

                if (getCustomAttributes == null)
                {
                    Logger.LogError("Could not locate System.RuntimeType.GetCustomAttributes(Type, bool).");
                    return;
                }

                var finalizer = AccessTools.Method(typeof(WttCustomParentReflectionGuard), nameof(GetCustomAttributesFinalizer));
                _harmony = new Harmony("com.packnstrap.wtt-reflectionguard");
                _harmony.Patch(getCustomAttributes, finalizer: new HarmonyMethod(finalizer));

                Logger.LogInfo("Patched RuntimeType.GetCustomAttributes with a WTT CustomParent-only exception guard.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to install WTT CustomParent reflection guard: {ex}");
            }
        }

        public static Exception GetCustomAttributesFinalizer(Exception __exception, object __instance, Type __0, ref object[] __result)
        {
            if (__exception == null)
            {
                return null;
            }

            if (__0 == null || __0.FullName != "WTTClientCommonLib.Attributes.CustomParent")
            {
                return __exception;
            }

            string typeName = "<unknown>";
            string assemblyName = "<unknown>";
            try
            {
                if (__instance is Type type)
                {
                    typeName = type.FullName ?? type.Name;
                    assemblyName = type.Assembly.GetName().Name;
                }
                else if (__instance != null)
                {
                    typeName = __instance.ToString();
                }
            }
            catch
            {
                // best-effort diagnostics only
            }

            Log?.LogWarning($"Suppressed broken custom-attribute metadata while WTT scanned type '{typeName}' from assembly '{assemblyName}'. {__exception.GetType().Name}: {__exception.Message}");

            __result = Array.Empty<object>();
            return null;
        }
    }
}
