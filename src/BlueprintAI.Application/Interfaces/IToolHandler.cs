using System.Text.Json;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Interfaces;

public class ToolResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<BlueprintDelta> Deltas { get; set; } = new();
    public string? AskUserQuestion { get; set; }
}

public interface IToolHandler
{
    string Name { get; }
    string Description { get; }
    JsonDocument ParameterSchema { get; }
    ToolResult Execute(JsonElement arguments, Blueprint blueprint);
}

public interface IAsyncToolHandler : IToolHandler
{
    Task<ToolResult> ExecuteAsync(JsonElement arguments, Blueprint blueprint, CancellationToken ct = default);
}
