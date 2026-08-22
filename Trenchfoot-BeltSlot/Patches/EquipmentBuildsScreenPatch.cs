using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace BeltSlot.Patches
{
    public class EquipmentBuildsScreenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentBuildsScreen), nameof(EquipmentBuildsScreen.Show), new[]
            {
                typeof(IEftSession),
                typeof(BackEndInventoryController),
                typeof(IHealthController),
                typeof(InventoryEquipment)
            });
        }
        [PatchPostfix]
        static void Postfix(EquipmentBuildsScreen __instance)
        {
            Plugin.Instance.SetBuildsArmbandSlot();
        }
    }
}
