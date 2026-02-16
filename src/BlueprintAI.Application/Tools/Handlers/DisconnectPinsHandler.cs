using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class DisconnectPinsHandler : IToolHandler
{
    public string Name => "disconnect_pins";
    public string Description => "Remove a connection between two pins.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "sourceNodeId": { "type": "string", "description": "ID of the source node" },
            "sourcePinName": { "type": "string", "description": "Name of the output pin" },
            "targetNodeId": { "type": "string", "description": "ID of the target node" },
            "targetPinName": { "type": "string", "description": "Name of the input pin" }
        },
        "required": ["sourceNodeId", "sourcePinName", "targetNodeId", "targetPinName"]
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var sourceNodeId = args.GetProperty("sourceNodeId").GetString()!;
        var sourcePinName = args.GetProperty("sourcePinName").GetString()!;
        var targetNodeId = args.GetProperty("targetNodeId").GetString()!;
        var targetPinName = args.GetProperty("targetPinName").GetString()!;

        var sourceNode = blueprint.Nodes.FirstOrDefault(n => n.Id == sourceNodeId);
        var targetNode = blueprint.Nodes.FirstOrDefault(n => n.Id == targetNodeId);
        if (sourceNode == null || targetNode == null)
            return new ToolResult { Success = false, Message = "Source or target node not found" };

        var sourcePin = sourceNode.OutputPins.FirstOrDefault(p => p.Name == sourcePinName);
        var targetPin = targetNode.InputPins.FirstOrDefault(p => p.Name == targetPinName);
        if (sourcePin == null || targetPin == null)
            return new ToolResult { Success = false, Message = "Source or target pin not found" };

        var connection = blueprint.Connections.FirstOrDefault(c =>
            c.SourceNodeId == sourceNodeId && c.SourcePinId == sourcePin.Id &&
            c.TargetNodeId == targetNodeId && c.TargetPinId == targetPin.Id);

        if (connection == null)
            return new ToolResult { Success = false, Message = "Connection not found" };

        blueprint.Connections.Remove(connection);
        sourcePin.IsConnected = blueprint.Connections.Any(c => c.SourcePinId == sourcePin.Id);
        targetPin.IsConnected = blueprint.Connections.Any(c => c.TargetPinId == targetPin.Id);
        blueprint.Version++;

        return new ToolResult
        {
            Success = true,
            Message = $"Disconnected {sourceNode.Title}.{sourcePinName} from {targetNode.Title}.{targetPinName}",
            Deltas = new List<BlueprintDelta>
            {
                new() { Type = DeltaType.ConnectionRemoved, RemovedId = connection.Id, Version = blueprint.Version }
            }
        };
    }
}
