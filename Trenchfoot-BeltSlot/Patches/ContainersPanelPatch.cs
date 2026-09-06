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
                if (Plugin.Instance.EnableLogging)
                {
                    Plugin.Instance.Log.LogInfo($"[Belt Slots] ContainersPanelPatch.PreFix called");
                }

                if (slotName == EquipmentSlot.ArmBand)
                {
                    SlotView template = defaultSlotTemplate.GetValue(__instance) as SlotView;
                    if (template != null)
                    {
                        __result = UnityEngine.Object.Instantiate<SlotView>(template);

                        if (Plugin.Instance.EnableLogging)
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
        private static FieldInfo slotViewsField;

        protected override MethodBase GetTargetMethod()
        {
            slotViewsField = AccessTools.Field(typeof(ContainersPanel), "_slotViews");
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Show));
        }

        [PatchPostfix]
        static void Postfix(
            ContainersPanel __instance,
            ItemContext parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompany insurance,
            bool inRaid)
        {
            try
            {
                var beltSlot = BeltSlotLookup.GetBeltSlot(equipment);
                if (beltSlot == null)
                {
                    return;
                }

                var slotViews = slotViewsField?.GetValue(__instance) as Dictionary<EquipmentSlot, SlotView>;
                if (slotViews == null || !slotViews.TryGetValue(EquipmentSlot.ArmBand, out var slotView) || slotView == null)
                {
                    Plugin.Instance.Log.LogWarning("[Belt Slots] Could not find the Belt UI placeholder.");
                    return;
                }

                slotView.Close();
                slotView.Show(beltSlot, parentContext, inventoryController, ItemUiContext.Instance, skills, insurance, true);
                slotView.gameObject.SetActive(true);
                Plugin.UiMappings.setBeltSlot_Settings(slotView.gameObject);

                if (Plugin.Instance.EnableLogging)
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

    public class MainMenuShowOperationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MainMenuShowOperation), nameof(MainMenuShowOperation.method_48));
        }
        [PatchPostfix]
        static void Postfix()
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] MainMenuShowOperationPatch.Postfix called");
            }
            Plugin.Instance.SetInsuranceArmbandSlot();
        }
    }

    public class ComplexStashPanelPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ComplexStashPanel), nameof(ComplexStashPanel.Show));
        }

        [PatchPostfix]
        static void Postfix(ComplexStashPanel __instance)
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ComplexStashPanelPatch.Postfix called");
            }
            Plugin.Instance.ComplexStashPanelLoaded = true;
            Plugin.Instance.SetLootArmbandSlotOnOpen();
        }
    }

    public class ComplexStashPanelPatch2 : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ComplexStashPanel), nameof(ComplexStashPanel.Close));
        }
        [PatchPostfix]
        static void Postfix(ComplexStashPanel __instance)
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ComplexStashPanelPatch2.Postfix called");
            }
            Plugin.Instance.ComplexStashPanelLoaded = false;
        }
    }

    public class ItemViewPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemView), nameof(ItemView.Update));
        }
        [PatchPostfix]
        static void Postfix(ItemView __instance)
        {
            if (Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] ItemViewPatch.Postfix called");
            }
            Plugin.Instance.UpdateLootArmBandSlot();
            Plugin.Instance.UpdatePlayerArmBandSlot();
            Plugin.Instance.UpdateScavInventoryArmbandSlot();
        }
    }
}
