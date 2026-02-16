using BlueprintAI.Domain.Enums;

namespace BlueprintAI.Domain.Models;

public class BlueprintNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public NodeStyle Style { get; set; }
    public List<Pin> InputPins { get; set; } = new();
    public List<Pin> OutputPins { get; set; } = new();
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public bool IsCompact { get; set; }
}
