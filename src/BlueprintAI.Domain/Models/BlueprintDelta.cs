using BlueprintAI.Domain.Enums;

namespace BlueprintAI.Domain.Models;

public class BlueprintDelta
{
    public DeltaType Type { get; set; }
    public BlueprintNode? Node { get; set; }
    public Connection? Connection { get; set; }
    public BlueprintComment? Comment { get; set; }
    public BlueprintVariable? Variable { get; set; }
    public string? RemovedId { get; set; }
    public Blueprint? FullState { get; set; }
    public int Version { get; set; }
}
