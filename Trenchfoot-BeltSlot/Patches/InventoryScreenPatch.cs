using EFT;
using EFT.Achievements;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.Prestige;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Reflection;

namespace BeltSlot.Patches
{
    internal class InventoryScreenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
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

            Plugin.Instance.InventoryScreen = __instance;
            Plugin.Instance.InventoryScreenLoaded = true;
        }
    }
}