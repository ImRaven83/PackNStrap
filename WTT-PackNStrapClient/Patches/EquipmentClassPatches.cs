using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;

namespace PackNStrap.Patches
{
    internal class ContainerSlotsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), "ContainerSlots");
        }

        [PatchPostfix]
        public static void PatchPostfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            var beltSlot = BeltSlotHelper.GetBeltSlot(__instance);
            if (beltSlot == null)
            {
                return;
            }

            List<Slot> newResult = __result.ToList();
            newResult.Add(beltSlot);
            __result = newResult;
        }
    }

    internal class PaymentSlotsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), "PaymentSlots");
        }

        [PatchPostfix]
        public static void PatchPostfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            var beltSlot = BeltSlotHelper.GetBeltSlot(__instance);
            if (beltSlot == null)
            {
                return;
            }

            List<Slot> newResult = __result.ToList();
            newResult.Add(beltSlot);
            __result = newResult;
        }
    }


    internal class GrenadeThrowingSlotsPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(InventoryEquipment), "GrenadeThrowingSlots");
        }

        [PatchPostfix]

        public static void PatchPostfix(InventoryEquipment __instance, ref IReadOnlyList<Slot> __result)
        {
            var beltSlot = BeltSlotHelper.GetBeltSlot(__instance);
            if (beltSlot == null)
            {
                return;
            }

            List<Slot> newResult = __result.ToList();
            newResult.Add(beltSlot);
            __result = newResult;
        }
    }

}
