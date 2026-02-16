using BlueprintAI.Domain.Enums;

namespace BlueprintAI.Domain.Models;

public class Pin
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public PinType Type { get; set; }
    public PinDirection Direction { get; set; }
    public string? DefaultValue { get; set; }
    public string? SubType { get; set; }
    public bool IsConnected { get; set; }
}
