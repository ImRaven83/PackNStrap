using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using Range = SemanticVersioning.Range;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using WTTPackNStrap.Models;
using WTTPackNStrap.Patches;
using Path = System.IO.Path;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Helpers.Server;

namespace WTTPackNStrap;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.wtt.packnstrap";
    public string Name { get; init; } = "WTT-PackNStrapServer";
    public string Author { get; init; } = "GrooveypenguinX";
    public List<string>? Contributors { get; init; } = null;
    public SemanticVersioning.Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version?.ToString(3));
    public Range SptVersion { get; init; } = new("~4.1.1");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~3.0.6") }
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public class WTTPackNStrap(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    JsonUtil jsonUtil,
    ModHelper modHelper,
    TemplateTable templateTable,
    LostOnDeathConfig lostOnDeathConfig,
    TradersTable tradersTable) : IOnLoad
{
    private Assembly _assembly;
    private Dictionary<MongoId, TemplateItem> _itemsDb;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        _assembly = Assembly.GetExecutingAssembly();
        _itemsDb = templateTable.Items;

        EnsureBeltSlot();

        await wttCommon.CustomItemParentService.CreateCustomParents(_assembly);
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(_assembly);
        wttCommon.CustomRigLayoutService.CreateRigLayouts(_assembly);
        await wttCommon.CustomLocaleService.CreateCustomLocales(_assembly);

        PreventBeltsFromBeingNested();
        ApplyConfigSettings();
    }

    // Belts can't be stashed inside another belt's own pouches.
    private void PreventBeltsFromBeingNested()
    {
        var beltIds = BeltIds.Items.Select(id => (MongoId)id).ToHashSet();

        foreach (var caseId in BeltIds.Items)
        {
            if (!_itemsDb.TryGetValue(caseId, out var item))
            {
                continue;
            }

            foreach (var grid in item.Properties?.Grids ?? [])
            {
                foreach (var filter in grid.Properties?.Filters ?? [])
                {
                    filter.ExcludedFilter ??= [];
                    foreach (var beltId in beltIds)
                    {
                        filter.ExcludedFilter.Add(beltId);
                    }
                }
            }
        }
    }

    // Adds a genuine "Belt" slot to the player's inventory root, cloned from the vanilla
    // ArmBand slot definition, so belts no longer compete with armbands for the same slot.
    // "addtoInventorySlots"/"inventorySlots": ["Belt"] on our items only adds them to a
    // slot that already exists on the Equipment template - it never creates one, so the
    // slot itself has to be created here before CreateCustomParents/CreateCustomItems run.
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
                if (_itemsDb.TryGetValue(caseId, out var item) && item.Properties != null)
                {
                    item.Properties.InsuranceDisabled = true;
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

}

