using System;
using System.Linq;
using EFT.InventoryLogic;

namespace PackNStrap.Helpers;

public static class BeltSlotHelper
{
    public const string SlotId = "Belt";

    public static Slot GetBeltSlot(InventoryEquipment equipment)
    {
        return equipment?.Slots?.FirstOrDefault(slot => slot != null && string.Equals(slot.ID, SlotId, StringComparison.Ordinal));
    }
}
