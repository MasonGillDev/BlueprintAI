using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class UpdateNodeHandler : IToolHandler
{
    public string Name => "update_node";
    public string Description => "Update an existing node's title, position, or pin default values.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "nodeId": { "type": "string", "description": "ID of the node to update" },
            "title": { "type": "string", "description": "New title" },
            "positionX": { "type": "number" },
            "positionY": { "type": "number" },
            "pinDefaults": {
                "type": "object",
                "description": "Map of pin name to new default value",
                "additionalProperties": { "type": "string" }
            }
        },
        "required": ["nodeId"]
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var nodeId = args.GetProperty("nodeId").GetString()!;
        var node = blueprint.Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null)
            return new ToolResult { Success = false, Message = $"Node '{nodeId}' not found" };

        if (args.TryGetProperty("title", out var title))
            node.Title = title.GetString()!;
        if (args.TryGetProperty("positionX", out var px))
            node.PositionX = px.GetDouble();
        if (args.TryGetProperty("positionY", out var py))
            node.PositionY = py.GetDouble();

        if (args.TryGetProperty("pinDefaults", out var defaults))
        {
            foreach (var prop in defaults.EnumerateObject())
            {
                var pin = node.InputPins.FirstOrDefault(p => p.Name == prop.Name)
                       ?? node.OutputPins.FirstOrDefault(p => p.Name == prop.Name);
                if (pin != null)
                    pin.DefaultValue = prop.Value.GetString();
            }
        }

        blueprint.Version++;
        return new ToolResult
        {
            Success = true,
            Message = $"Updated node '{node.Title}'",
            Deltas = new List<BlueprintDelta>
            {
                new() { Type = DeltaType.NodeUpdated, Node = node, Version = blueprint.Version }
            }
        };
    }
}
