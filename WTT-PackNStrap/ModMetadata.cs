using Range = SemanticVersioning.Range;
using SPTarkov.Server.Core.Models.Spt.Mod;

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
