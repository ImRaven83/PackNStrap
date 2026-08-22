using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using WTTPackNStrap.Models;
using WTTPackNStrap.Patches;
using Path = System.IO.Path;

namespace WTTPackNStrap;

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public class WTTPackNStrap(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    TemplateTable templateTable,
    TradersTable tradersTable,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    LostOnDeathConfig lostOnDeathConfig) : IOnLoad
{
    private readonly Assembly _assembly = Assembly.GetExecutingAssembly();
    private readonly Dictionary<MongoId, TemplateItem> _itemsDb = templateTable.Items;
    private readonly Dictionary<MongoId, Trader> _traderDb = tradersTable;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        EnsureBeltSlot();

        await wttCommon.CustomItemParentService.CreateCustomParents(_assembly, "db/CustomParents");
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(_assembly);
        wttCommon.CustomRigLayoutService.CreateRigLayouts(_assembly);
        await wttCommon.CustomLocaleService.CreateCustomLocales(_assembly);

        ApplyConfigSettings();
    }

    private void ApplyConfigSettings()
    {

        var modPath = modHelper.GetAbsolutePathToModFolder(_assembly);
        var configPath = Path.Join(modPath, "config", "config.jsonc");

        if (!File.Exists(configPath))
        {
            return;
        }

        var configJson = File.ReadAllText(configPath);
        var config = jsonUtil.Deserialize<PackNStrapConfig>(configJson);

        if (config is { loseArmbandOnDeath: false })
        {
            new IsItemKeptAfterDeathPatch().Enable();
            new HandleInsuredItemLostEventPatch().Enable();
            foreach (var caseId in BeltIds.Items)
            {
                if (_itemsDb.TryGetValue(caseId, out var item))
                {
                    item.Properties?.InsuranceDisabled = true;
                }
            }
        }
        else
        {
            lostOnDeathConfig.Equipment.ArmBand = true;
        }

        if (config is { addCasesToSecureContainers: true })
        {
            foreach (var caseId in ContainerIds.Items)
            {
                foreach (var item in _itemsDb.Values)
                {
                    if (item.Parent == "5448bf274bdc2dfc2f8b456a" || item.Parent == "68154651f849fb4e7d816738")
                    {
                        if (item.Id == "5c0a794586f77461c458f892")
                        {
                            continue;
                        }

                        var grids = item.Properties?.Grids?.ToList();
                        if (grids?.Count > 0)
                        {
                            var filters = grids[0].Properties?.Filters?.FirstOrDefault();
                            if (filters != null)
                            {
                                filters.Filter ??= [];
                                filters.Filter.Add((MongoId)caseId);
                            }
                        }
                    }
                }
            }
        }
    }

    // Adds a genuine "Belt" slot to the player's inventory root, cloned from the vanilla
    // ArmBand slot definition, so belts no longer compete with armbands for the same slot.
    private void EnsureBeltSlot()
    {
        if (!_itemsDb.TryGetValue("55d7217a4bdc2d86028b456d", out var defaultInventory) || defaultInventory.Properties == null)
        {
            return;
        }

        var slots = defaultInventory.Properties.Slots?.ToList() ?? [];
        if (slots.Any(slot => slot.Name == "Belt"))
        {
            return;
        }

        var armBandSlot = slots.FirstOrDefault(slot => slot.Name == "ArmBand");
        var beltSlot = new Slot
        {
            Name = "Belt",
            Id = "6815465859b8c6ff13f94027",
            Parent = armBandSlot?.Parent ?? "55d7217a4bdc2d86028b456d",
            Required = false,
            MaxCount = armBandSlot?.MaxCount ?? 1.0,
            MergeSlotWithChildren = armBandSlot?.MergeSlotWithChildren,
            Prototype = armBandSlot?.Prototype,
            Properties = new SlotProperties
            {
                MaxStackCount = armBandSlot?.Properties?.MaxStackCount ?? 1.0,
                Filters = [new SlotFilter { Filter = [(MongoId)"6815465859b8c6ff13f94026"] }]
            }
        };

        slots.Add(beltSlot);
        defaultInventory.Properties.Slots = slots;
    }
}
