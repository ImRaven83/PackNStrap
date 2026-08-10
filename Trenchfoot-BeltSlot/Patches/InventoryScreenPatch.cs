using EFT;
using EFT.Achievements;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.Prestige;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace BeltSlot.Patches
{
    internal class InventoryScreenPatch : ModulePatch // all patches must inherit ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // one way methods can be patched is by targeting both their class name and the name of the method itself
            // the example in this patch is the Jump() method in the Player class
            return AccessTools.Method(typeof(InventoryScreen), nameof(InventoryScreen.Show), new[]
            {
                typeof(IHealthController),
                typeof(InventoryController),
                typeof(QuestController),
                typeof(AchievementsController),
                typeof(PrestigeController),
                typeof(CompoundItem),
                typeof(EInventoryTab),
                typeof(IEftSession),
                typeof(ItemContext),
                typeof(bool)
            });
        }

        [PatchPostfix]
        static void Postfix(InventoryScreen __instance)
        {
            if (Plugin.Instance == null) 
                return;

            Plugin.Instance.inventoryScreen = __instance;
            Plugin.Instance.inventoryScreenLoaded = true;
        }
    }
}