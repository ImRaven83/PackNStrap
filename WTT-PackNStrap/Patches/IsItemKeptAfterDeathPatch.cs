using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace WTTPackNStrap.Patches;

public class IsItemKeptAfterDeathPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod()
    {
        return typeof(InRaidHelper).GetMethod(
            "IsItemKeptAfterDeath",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
    }

    [PatchPostfix]
    public static void Postfix(PmcData pmcData, Item itemToCheck, ref bool __result)
    {
        if (!__result && IsItemInBelt(pmcData, itemToCheck))
        {
            __result = true;
        }
    }

    private static bool IsItemInBelt(PmcData pmcData, Item item)
    {
        var inventoryItems = pmcData.Inventory?.Items ?? [];

        // Find the independent Belt slot
        var beltItem = inventoryItems.FirstOrDefault(i => i.SlotId == "Belt");
        if (beltItem == null)
        {
            return false;
        }

        // Check if item is the belt itself or a child of it
        return item.Id == beltItem.Id ||
               inventoryItems.GetItemWithChildren(beltItem.Id).Any(i => i.Id == item.Id);
    }
}