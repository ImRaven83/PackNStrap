using TMPro;
using UnityEngine;

namespace BeltSlot.Helpers
{
    internal class UI_Mappings
    {
        // Set the settings of the belt slot, such as its name
        public void setBeltSlot_Settings(GameObject targetBelt)
        {
            if(Plugin.Instance.EnableLogging)
            {
                Plugin.Instance.Log.LogInfo($"[Belt Slots] setBeltSlot_Settings called for {targetBelt.name}");
            }

            if (targetBelt != null)
            {
                GameObject _headerPanel = targetBelt.transform.GetChild(0).gameObject; // Header panel of the belt slot
                GameObject _slotViewHeader = _headerPanel.transform.GetChild(1).gameObject; // Slot view header of the belt slot
                GameObject _slotName = _slotViewHeader.transform.GetChild(2).gameObject; // Slot name of the belt slot

                _slotName.GetComponent<TextMeshProUGUI>().text = "BELT"; // Set the slot name to "BELT"
            }
        }
    }
}
