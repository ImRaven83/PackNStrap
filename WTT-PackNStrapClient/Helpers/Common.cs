using EFT;
using EFT.InventoryLogic;
using PackNStrap.Core.Items;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PackNStrap.Helpers;

public abstract class Common
{
    public static List<CustomContainerItemClass> GetMagDumpPouches(InventoryEquipment equipment, bool backpackIncluded)
    {
        if (equipment == null)
        {
            Console.WriteLine("Equipment is null.");
            return null;
        }

        List<CustomContainerItemClass> magDumpPouches = new List<CustomContainerItemClass>();
        var magDumpPouchItemId = "440de5d056825485a0cf3a19";

        void FindMagDumpPouchInItem(Item item)
        {
            if (item == null) return;

            foreach (var itemInGrid in item.GetAllItems())
            {
                if (itemInGrid is CustomContainerItemClass potentialMagDumpPouch 
                    && potentialMagDumpPouch.TemplateId == magDumpPouchItemId)
                {
                    if (potentialMagDumpPouch.IsChildOf(item))
                        magDumpPouches.Add(potentialMagDumpPouch);
                }
            }
        }

        Slot tacticalVestSlot = equipment.GetSlot(EquipmentSlot.TacticalVest);
        Slot pocketsSlot = equipment.GetSlot(EquipmentSlot.Pockets);
        Slot backpackSlot = equipment.GetSlot(EquipmentSlot.Backpack);
        Slot beltSlot = BeltSlotHelper.GetBeltSlot(equipment);

        FindMagDumpPouchInItem(tacticalVestSlot?.ContainedItem as Vest);
        FindMagDumpPouchInItem(pocketsSlot?.ContainedItem as Pockets);
        if (backpackIncluded)
            FindMagDumpPouchInItem(backpackSlot?.ContainedItem as Backpack);
        FindMagDumpPouchInItem(beltSlot?.ContainedItem as CustomBeltItemClass);

        return magDumpPouches;
    }

    public static bool CanAcceptItems(Grid grid)
    {
        Player player = PackNStrap.Player;
        if (player != null && player.HandsController != null && player.HandsController?.Item != null && player.HandsController?.Item?.GetCurrentMagazine() != null)
        {
            return grid.CanAccept(player.HandsController?.Item?.GetCurrentMagazine());
        }
        return false;
    }
}