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
    public string Description => "Push the current blueprint to Unreal Engine. Can update an existing open blueprint or create a brand new one in the project.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "blueprintName": {
                "type": "string",
                "description": "Target blueprint name in UE (e.g. 'BP_MyActor')"
            },
            "createNew": {
                "type": "boolean",
                "description": "If true, creates a new blueprint asset in UE instead of updating an existing one. Default false."
            },
            "path": {
                "type": "string",
                "description": "Content folder path for new blueprints (e.g. '/Game/Blueprints'). Only used when createNew is true. Default '/Game/Blueprints'."
            },
            "parentClass": {
                "type": "string",
                "description": "Parent class for new blueprints: Actor, Pawn, Character, PlayerController, GameModeBase, ActorComponent. Only used when createNew is true. Default 'Actor'."
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
        var createNew = args.TryGetProperty("createNew", out var cn) && cn.GetBoolean();

        try
        {
            if (createNew)
            {
                var path = args.TryGetProperty("path", out var p) ? p.GetString() ?? "/Game/Blueprints" : "/Game/Blueprints";
                var parentClass = args.TryGetProperty("parentClass", out var pc) ? pc.GetString() ?? "Actor" : "Actor";

                var result = await _bridge.CreateBlueprintAsync(name, path, parentClass, blueprint, ct);
                if (result.Success)
                {
                    return new ToolResult
                    {
                        Success = true,
                        Message = $"Created new blueprint '{result.Name}' at {result.Path} in UE with {blueprint.Nodes.Count} nodes. It's now open in the UE editor."
                    };
                }

                return new ToolResult { Success = false, Message = $"Failed to create blueprint: {result.Error}" };
            }
            else
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
        }
        catch (Exception ex)
        {
            return new ToolResult { Success = false, Message = $"Failed to push to UE: {ex.Message}" };
        }
    }
}
