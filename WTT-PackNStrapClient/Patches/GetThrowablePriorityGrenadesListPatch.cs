using EFT.InventoryLogic;
using HarmonyLib;
using PackNStrap.Helpers;
using SPT.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PackNStrap.Patches;

internal class GetThrowablePriorityGrenadesListPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(InventoryExtension),
            nameof(InventoryExtension.GetThrowablePriorityGrenadesList)
        );
    }

    [PatchPostfix]
    public static void Postfix(
        InventoryController inventoryController,
        ref List<ThrowWeap> __result)
    {
        if (inventoryController?.Inventory?.Equipment == null || __result == null)
            return;

        if (BeltSlotHelper.GetBeltSlot(inventoryController.Inventory.Equipment)
                ?.ContainedItem is not CompoundItem belt)
        {
            return;
        }

        var containers = new List<CompoundItem> { belt };

        var beltGrenades = containers
            .GetTopLevelItems()
            .OfType<ThrowWeap>()
            .Where(inventoryController.Examined);

        __result.AddRange(beltGrenades);
        __result.Sort(InventoryExtension.CG_Class2411.CG_Class2411.method_3);
    }
}