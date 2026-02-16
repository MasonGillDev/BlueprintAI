using BlueprintAI.Domain.Enums;

namespace BlueprintAI.Domain.Models;

public class Connection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePinId { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPinId { get; set; } = string.Empty;
    public PinType PinType { get; set; }
}
