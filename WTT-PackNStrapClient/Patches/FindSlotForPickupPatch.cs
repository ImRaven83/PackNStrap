using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Core.Items;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;

namespace PackNStrap.Patches
{
    internal class FindSlotForPickupPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InventoryExtension), 
                nameof(InventoryExtension.FindSlotToPickUp),
                new[] { typeof(InventoryEquipment), typeof(Item) }
            );
        }

        [PatchPostfix]
        public static void Postfix(
            ref ItemAddress __result,
            InventoryEquipment equipment,
            Item item)
        {
            if (__result != null || !(item is CustomBeltItemClass))
            {
                return;
            }

            var beltSlot = BeltSlotHelper.GetBeltSlot(equipment);
            if (beltSlot == null || beltSlot.Deleted || !beltSlot.CheckCompatibility(item))
            {
                return;
            }

            var address = beltSlot.FindLocationForItem(item, out _);
            if (address != null)
            {
                __result = address;
            }
        }
    }
}