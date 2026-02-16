using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class GetBlueprintStateHandler : IToolHandler
{
    public string Name => "get_blueprint_state";
    public string Description => "Get the current state of the blueprint, including all nodes, connections, variables, and comments.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {}
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine($"Blueprint: {blueprint.Name} (v{blueprint.Version})");
        summary.AppendLine($"Nodes ({blueprint.Nodes.Count}):");
        foreach (var node in blueprint.Nodes)
        {
            summary.AppendLine($"  - [{node.Id}] {node.Title} ({node.Style}) at ({node.PositionX}, {node.PositionY})");
            foreach (var pin in node.InputPins)
                summary.AppendLine($"    IN: {pin.Name} ({pin.Type}){(pin.DefaultValue != null ? $" = {pin.DefaultValue}" : "")}{(pin.IsConnected ? " [connected]" : "")}");
            foreach (var pin in node.OutputPins)
                summary.AppendLine($"    OUT: {pin.Name} ({pin.Type}){(pin.IsConnected ? " [connected]" : "")}");
        }
        summary.AppendLine($"Connections ({blueprint.Connections.Count}):");
        foreach (var conn in blueprint.Connections)
            summary.AppendLine($"  - {conn.SourceNodeId}.{conn.SourcePinId} → {conn.TargetNodeId}.{conn.TargetPinId} ({conn.PinType})");
        summary.AppendLine($"Variables ({blueprint.Variables.Count}):");
        foreach (var v in blueprint.Variables)
            summary.AppendLine($"  - {v.Name}: {v.Type}{(v.DefaultValue != null ? $" = {v.DefaultValue}" : "")}");

        return new ToolResult
        {
            Success = true,
            Message = summary.ToString()
        };
    }
}
