using BeltSlot.Helpers;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Insurance;
using EFT.UI.Screens;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BeltSlot.Patches
{
    public class ContainersPanelPatch : ModulePatch
    {
        private static FieldInfo? defaultSlotTemplate;

        protected override MethodBase GetTargetMethod()
        {
            defaultSlotTemplate = AccessTools.Field(typeof(ContainersPanel), "_defaultSlotTemplate");
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.InstantiateSlotView));
        }

        [PatchPrefix]
        static bool Prefix(ContainersPanel __instance, EquipmentSlot slotName, ref SlotView __result)
        {
            try
            {
                if (Plugin.Instance.enableLogging)
                {
                    Plugin.Instance.Log.LogInfo($"[Belt Slots] ContainersPanelPatch.PreFix called");
                }

                if (slotName == EquipmentSlot.ArmBand)
                {
                    SlotView template = defaultSlotTemplate.GetValue(__instance) as SlotView;
                    if (template != null)
                    {
                        __result = UnityEngine.Object.Instantiate<SlotView>(template);

                        if (Plugin.Instance.enableLogging)
                        {
                            Plugin.Instance.Log.LogInfo($"[Belt Slots] default template for armband");
                        }
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] Exception: {ex}");
            }

            return true;
        }
    }

    public class ContainersPanelPatch2 : ModulePatch
    {
        private static FieldInfo slotViewsDictionary;

        protected override MethodBase GetTargetMethod()
        {
            slotViewsDictionary = AccessTools.Field(typeof(ContainersPanel), "_slotViews");
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Show));
        }

        [PatchPostfix]
        static void Postfix(ContainersPanel __instance, ItemContext parentContext, InventoryEquipment equipment, InventoryController inventoryController, SkillManager skills, InsuranceCompany insurance, bool inRaid)
        {
            try
            {
                var beltSlot = BeltSlotLookup.GetBeltSlot(equipment);
                if (beltSlot == null)
                {
                    return;
                }

                if (slotViewsDictionary?.GetValue(__instance) is not Dictionary<EquipmentSlot, SlotView> dictionary
                    || !dictionary.TryGetValue(EquipmentSlot.ArmBand, out var slotView)
                    || slotView == null)
                {
                    Plugin.Instance.Log.LogWarning($"[Belt Slots] Could not find the Belt UI placeholder.");
                    return;
                }

                slotView.Close();
                slotView.Show(beltSlot, parentContext, inventoryController, ItemUiContext.Instance, skills, insurance, true);
                slotView.gameObject.SetActive(true);
                Plugin.UiMappings.setBeltSlot_Settings(slotView.gameObject);

                if (Plugin.Instance.enableLogging)
                {
                    Plugin.Instance.Log.LogInfo($"[Belt Slots] Bound Belt UI to independent Belt slot.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance.Log.LogError($"[Belt Slots] Failed to bind independent Belt slot: {ex}");
            }
        }
    }

    // Insurance, builds, complex-stash (looting), and squadmate-equipment screens all render
    // their equipment rows through this same ContainersPanel component, so ContainersPanelPatch
    // and ContainersPanelPatch2 above already cover them -- no per-screen patch needed.
}
