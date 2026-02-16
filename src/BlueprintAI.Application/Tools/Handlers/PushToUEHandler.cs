using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class PushToUEHandler : IAsyncToolHandler
{
    private readonly IUEBridgeService _bridge;

    public PushToUEHandler(IUEBridgeService bridge)
    {
        _bridge = bridge;
    }

    public string Name => "push_to_ue";
    public string Description => "Push the current blueprint state to a connected Unreal Engine editor, creating or updating the blueprint in UE.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "blueprintName": {
                "type": "string",
                "description": "Target blueprint name in UE to push to"
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
            var success = await _bridge.PushFullBlueprintAsync(name, blueprint, ct);
            if (success)
            {
                return new ToolResult
                {
                    Success = true,
                    Message = $"Successfully pushed blueprint with {blueprint.Nodes.Count} nodes to UE as '{name}'"
                };
            }

            return new ToolResult { Success = false, Message = "UE rejected the blueprint push" };
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Message = $"Failed to push to UE: {ex.Message}" };
        }
    }
}
