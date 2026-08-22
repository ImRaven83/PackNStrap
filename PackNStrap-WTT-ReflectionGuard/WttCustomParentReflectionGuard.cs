using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace PackNStrapWttCompat;

// WTTClientCommonLib.Services.CustomItemParentLoader.RegisterCustomParents() scans every
// loaded assembly's types for the [CustomParent] attribute (used to register our
// CustomBeltItemClass/CustomContainerItemClass/CustomSecureContainerClass). In a
// heavily-modded install, reflecting over an unrelated broken/mismatched assembly can throw
// (e.g. a stale BepInEx reference) and abort that scan entirely, silently failing to
// register our items too. This patches the underlying reflection call to swallow exceptions
// only when the lookup was specifically for WTTClientCommonLib.Attributes.CustomParent,
// leaving every other reflection failure to propagate normally.
[BepInPlugin("com.packnstrap.wtt-reflectionguard", "PackNStrap WTT CustomParent Reflection Guard", "1.0.0")]
[BepInDependency("com.wtt.commonlib")]
public sealed class WttCustomParentReflectionGuard : BaseUnityPlugin
{
    private static ManualLogSource Log;

    private Harmony _harmony;

    private void Awake()
    {
        Log = Logger;
        try
        {
            var getCustomAttributes = AccessTools.Method(typeof(string).GetType(), "GetCustomAttributes", new[] { typeof(Type), typeof(bool) });
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
            Logger.LogError("Failed to install WTT CustomParent reflection guard: " + ex);
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

        var typeName = "<unknown>";
        var assemblyName = "<unknown>";
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
            // Best-effort logging only.
        }

        Log?.LogWarning($"Suppressed broken custom-attribute metadata while WTT scanned type '{typeName}' from assembly '{assemblyName}'. {__exception.GetType().Name}: {__exception.Message}");
        __result = Array.Empty<object>();
        return null;
    }
}
