using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using Range = SemanticVersioning.Range;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using WTTPackNStrap.Models;
using WTTPackNStrap.Patches;
using Path = System.IO.Path;

namespace WTTPackNStrap;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.wtt.packnstrap";
    public string Name { get; init; } = "WTT-PackNStrapServer";
    public string Author { get; init; } = "GrooveypenguinX";
    public List<string>? Contributors { get; init; } = null;
    public SemanticVersioning.Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version?.ToString(3));
    public Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~3.0.0") }
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

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
        CreateCustomItemsAndTemplates();
        ConfigureCustomItemsToTraders();
        AddToInventorySlots();

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

    private void AddToInventorySlots()
    {
        var defaultInventory = _itemsDb["55d7217a4bdc2d86028b456d"];

        foreach (var slot in defaultInventory.Properties?.Slots ?? [])
        {
            if (slot.Name == "SecuredContainer")
            {
                slot.Properties?.Filters?.First().Filter?.Add("68154651f849fb4e7d816738");
            }
            if (slot.Name == "ArmBand")
            {
                slot.Properties?.Filters?.First().Filter?.Add("6815465859b8c6ff13f94026");
            }
        }
    }

    private void ConfigureCustomItemsToTraders()
    {
        // Belts to Ragman
        _traderDb["5ac3b934156ae10c4430e83c"].Base.ItemsBuy?.Category.Add("6815465859b8c6ff13f94026");
        // Cases and Secure Containers to Therapist
        _traderDb["54cb57776803fa99248b456e"].Base.ItemsBuy?.Category.Add("680fd1dae5044e670a092e16");
        _traderDb["54cb57776803fa99248b456e"].Base.ItemsBuy?.Category.Add("68154651f849fb4e7d816738");

    }

    private void CreateCustomItemsAndTemplates()
    {
        _itemsDb["680fce2ec7b9b222270f074c"] = new TemplateItem()
        {
            Id = "680fce2ec7b9b222270f074c",
            Name = "CustomContainerTemplate",
            Parent = "566162e44bdc2d3f298b4573",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };
        _itemsDb["680fd1dae5044e670a092e16"] = new TemplateItem()
        {
            Id = "680fd1dae5044e670a092e16",
            Name = "CustomContainerItem",
            Parent = "680fce2ec7b9b222270f074c",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };
        _itemsDb["68154651f849fb4e7d816738"] = new TemplateItem()
        {
            Id = "68154651f849fb4e7d816738",
            Name = "CustomSecureContainerItem",
            Parent = "680fce2ec7b9b222270f074c",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };
        _itemsDb["6815465859b8c6ff13f94026"] = new TemplateItem()
        {
            Id = "6815465859b8c6ff13f94026",
            Name = "CustomBeltItem",
            Parent = "680fce2ec7b9b222270f074c",
            Type = "Node",
            Properties = new TemplateItemProperties()
        };

    }
}

