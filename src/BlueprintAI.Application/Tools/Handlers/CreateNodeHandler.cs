using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class CreateNodeHandler : IToolHandler
{
    public string Name => "create_node";
    public string Description => "Create a new Blueprint node with specified title, category, style, pins, and position.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "title": { "type": "string", "description": "Display title of the node (e.g., 'Print String', 'Event BeginPlay')" },
            "category": { "type": "string", "description": "Node category (e.g., 'Flow Control', 'String', 'Utilities')" },
            "style": { "type": "string", "enum": ["Event", "Function", "Pure", "FlowControl", "Variable", "Macro"], "description": "Visual style determining header color" },
            "inputPins": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string" },
                        "type": { "type": "string", "enum": ["Exec", "Bool", "Int", "Float", "String", "Vector", "Rotator", "Transform", "Object", "Class", "Wildcard"] },
                        "defaultValue": { "type": "string" }
                    },
                    "required": ["name", "type"]
                }
            },
            "outputPins": {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "name": { "type": "string" },
                        "type": { "type": "string", "enum": ["Exec", "Bool", "Int", "Float", "String", "Vector", "Rotator", "Transform", "Object", "Class", "Wildcard"] }
                    },
                    "required": ["name", "type"]
                }
            },
            "positionX": { "type": "number", "description": "X coordinate on canvas" },
            "positionY": { "type": "number", "description": "Y coordinate on canvas" },
            "isCompact": { "type": "boolean", "description": "Whether to render as compact node" }
        },
        "required": ["title", "category", "style", "inputPins", "outputPins"]
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var node = new BlueprintNode
        {
            Title = args.GetProperty("title").GetString()!,
            Category = args.GetProperty("category").GetString()!,
            Style = Enum.Parse<NodeStyle>(args.GetProperty("style").GetString()!),
            PositionX = args.TryGetProperty("positionX", out var px) ? px.GetDouble() : blueprint.Nodes.Count * 300,
            PositionY = args.TryGetProperty("positionY", out var py) ? py.GetDouble() : 200,
            IsCompact = args.TryGetProperty("isCompact", out var ic) && ic.GetBoolean()
        };

        foreach (var pin in args.GetProperty("inputPins").EnumerateArray())
        {
            node.InputPins.Add(new Pin
            {
                Name = pin.GetProperty("name").GetString()!,
                Type = Enum.Parse<PinType>(pin.GetProperty("type").GetString()!),
                Direction = PinDirection.Input,
                DefaultValue = pin.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null
            });
        }

        foreach (var pin in args.GetProperty("outputPins").EnumerateArray())
        {
            node.OutputPins.Add(new Pin
            {
                Name = pin.GetProperty("name").GetString()!,
                Type = Enum.Parse<PinType>(pin.GetProperty("type").GetString()!),
                Direction = PinDirection.Output
            });
        }

        blueprint.Nodes.Add(node);
        blueprint.Version++;

        return new ToolResult
        {
            Success = true,
            Message = $"Created node '{node.Title}' with id '{node.Id}'",
            Deltas = new List<BlueprintDelta>
            {
                new() { Type = DeltaType.NodeAdded, Node = node, Version = blueprint.Version }
            }
        };
    }
}
