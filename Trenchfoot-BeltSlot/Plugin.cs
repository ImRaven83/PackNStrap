using BeltSlot.Helpers;
using BeltSlot.Patches;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using EFT.InventoryLogic;
using EFT.UI;
using System.Linq;
using System.Reflection;

namespace BeltSlot
{
    [BepInPlugin(
        PluginConstants.Guid,
        PluginConstants.Name,
        PluginConstants.Version)]
    [BepInDependency("com.SPT.core", "4.0.4")]
    [BepInDependency("com.wtt.packnstrap", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        #region Variables
        public bool EnableLogging = false;
        public bool packNStrapInstalled;
        internal static Plugin Instance { get; set; }
        internal ManualLogSource Log { get; set; }
        private static UI_Mappings uiMappings;
        internal static UI_Mappings UiMappings { get => uiMappings; set => uiMappings = value; }
        #endregion

        #region Belt Settings
        private static EquipmentSlot[] belowEquipmentSlots = new[]
        {
            EquipmentSlot.TacticalVest,
            EquipmentSlot.Pockets,
            EquipmentSlot.ArmBand,
            EquipmentSlot.Backpack,
            EquipmentSlot.SecuredContainer,
            EquipmentSlot.Dogtag
        };

        void SetEquipmentSlots()
        {
            // Belt now lives in its own independent slot, so it always renders below Pockets.
            typeof(ContainersPanel)
                .GetField("_slotNames", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, belowEquipmentSlots);
        }
        #endregion

        private void Awake()
        {
            packNStrapInstalled = Chainloader.PluginInfos.Keys.Contains("com.wtt.packnstrap");
            Instance = this;
            Log = Logger;
            UiMappings = new UI_Mappings();

            SetEquipmentSlots();
            new ContainersPanelPatch().Enable();
            new ContainersPanelPatch2().Enable();

            // Enables the correct patch based on if PackNStrap is installed or not
            if (packNStrapInstalled)
            {
                new GetPrioritizedContainersPatch().Disable();
                new GetPrioritizedContainersPackNStrapPatch().Enable();
            }
            else
            {
                new GetPrioritizedContainersPackNStrapPatch().Disable();
                new GetPrioritizedContainersPatch().Enable();
            }
        }
    }
}
