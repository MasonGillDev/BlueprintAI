using BlueprintAI.Domain.Enums;

namespace BlueprintAI.Domain.Models;

public class BlueprintVariable
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public PinType Type { get; set; }
    public string? DefaultValue { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsEditable { get; set; } = true;
}
