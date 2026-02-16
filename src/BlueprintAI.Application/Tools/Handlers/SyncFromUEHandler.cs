using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class SyncFromUEHandler : IAsyncToolHandler
{
    private readonly IUEBridgeService _bridge;

    public SyncFromUEHandler(IUEBridgeService bridge)
    {
        _bridge = bridge;
    }

    public string Name => "sync_from_ue";
    public string Description => "Import a blueprint from a connected Unreal Engine editor, replacing the current canvas with the UE blueprint's nodes and connections.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "blueprintName": {
                "type": "string",
                "description": "Name of the blueprint to import from UE"
            }
        },
        "required": ["blueprintName"]
    }
    """);

    public ToolResult Execute(JsonElement arguments, Blueprint blueprint)
    {
        return new ToolResult { Success = false, Message = "This tool requires async execution" };
    }

    public async Task<ToolResult> ExecuteAsync(JsonElement args, Blueprint blueprint, CancellationToken ct = default)
    {
        var name = args.GetProperty("blueprintName").GetString()!;

        try
        {
            var imported = await _bridge.ImportBlueprintAsync(name, ct);

            blueprint.Nodes.Clear();
            blueprint.Nodes.AddRange(imported.Nodes);
            blueprint.Connections.Clear();
            blueprint.Connections.AddRange(imported.Connections);
            blueprint.Comments.Clear();
            blueprint.Comments.AddRange(imported.Comments);
            blueprint.Variables.Clear();
            blueprint.Variables.AddRange(imported.Variables);
            blueprint.Name = imported.Name;
            blueprint.Version++;

            return new ToolResult
            {
                Success = true,
                Message = $"Imported blueprint '{name}' with {imported.Nodes.Count} nodes and {imported.Connections.Count} connections",
                Deltas = new List<BlueprintDelta>
                {
                    new()
                    {
                        Type = DeltaType.FullSync,
                        FullState = blueprint,
                        Version = blueprint.Version
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Message = $"Failed to import from UE: {ex.Message}" };
        }
    }
}
