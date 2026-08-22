using System;
using System.Linq;
using EFT.InventoryLogic;

namespace BeltSlot.Helpers;

internal static class BeltSlotLookup
{
    internal const string SlotId = "Belt";

    internal static Slot GetBeltSlot(InventoryEquipment equipment)
    {
        return equipment?.Slots?.FirstOrDefault(slot => slot != null && string.Equals(slot.ID, SlotId, StringComparison.Ordinal));
    }
}
