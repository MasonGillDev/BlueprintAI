using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class CreateVariableHandler : IToolHandler
{
    public string Name => "create_variable";
    public string Description => "Declare a blueprint variable that can be used with Get/Set nodes.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "name": { "type": "string", "description": "Variable name" },
            "type": { "type": "string", "enum": ["Bool", "Int", "Float", "String", "Vector", "Rotator", "Transform", "Object"], "description": "Variable type" },
            "defaultValue": { "type": "string", "description": "Default value" },
            "category": { "type": "string", "description": "Category for organization" }
        },
        "required": ["name", "type"]
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var variable = new BlueprintVariable
        {
            Name = args.GetProperty("name").GetString()!,
            Type = Enum.Parse<PinType>(args.GetProperty("type").GetString()!),
            DefaultValue = args.TryGetProperty("defaultValue", out var dv) ? dv.GetString() : null,
            Category = args.TryGetProperty("category", out var cat) ? cat.GetString()! : string.Empty
        };

        blueprint.Variables.Add(variable);
        blueprint.Version++;

        return new ToolResult
        {
            Success = true,
            Message = $"Created variable '{variable.Name}' of type {variable.Type}",
            Deltas = new List<BlueprintDelta>
            {
                new() { Type = DeltaType.VariableAdded, Variable = variable, Version = blueprint.Version }
            }
        };
    }
}
