using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class ConnectPinsHandler : IToolHandler
{
    public string Name => "connect_pins";
    public string Description => "Connect an output pin of one node to an input pin of another node.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "sourceNodeId": { "type": "string", "description": "ID of the source node" },
            "sourcePinName": { "type": "string", "description": "Name of the output pin on source node" },
            "targetNodeId": { "type": "string", "description": "ID of the target node" },
            "targetPinName": { "type": "string", "description": "Name of the input pin on target node" }
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
        if (sourceNode == null)
            return new ToolResult { Success = false, Message = $"Source node '{sourceNodeId}' not found" };

        var targetNode = blueprint.Nodes.FirstOrDefault(n => n.Id == targetNodeId);
        if (targetNode == null)
            return new ToolResult { Success = false, Message = $"Target node '{targetNodeId}' not found" };

        var sourcePin = sourceNode.OutputPins.FirstOrDefault(p => p.Name == sourcePinName);
        if (sourcePin == null)
            return new ToolResult { Success = false, Message = $"Output pin '{sourcePinName}' not found on node '{sourceNode.Title}'" };

        var targetPin = targetNode.InputPins.FirstOrDefault(p => p.Name == targetPinName);
        if (targetPin == null)
            return new ToolResult { Success = false, Message = $"Input pin '{targetPinName}' not found on node '{targetNode.Title}'" };

        if (sourcePin.Type != PinType.Exec && sourcePin.Type != PinType.Wildcard &&
            targetPin.Type != PinType.Wildcard && sourcePin.Type != targetPin.Type)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Type mismatch: cannot connect {sourcePin.Type} to {targetPin.Type}"
            };
        }

        var connection = new Connection
        {
            SourceNodeId = sourceNodeId,
            SourcePinId = sourcePin.Id,
            TargetNodeId = targetNodeId,
            TargetPinId = targetPin.Id,
            PinType = sourcePin.Type
        };

        sourcePin.IsConnected = true;
        targetPin.IsConnected = true;
        blueprint.Connections.Add(connection);
        blueprint.Version++;

        return new ToolResult
        {
            Success = true,
            Message = $"Connected {sourceNode.Title}.{sourcePinName} → {targetNode.Title}.{targetPinName}",
            Deltas = new List<BlueprintDelta>
            {
                new() { Type = DeltaType.ConnectionAdded, Connection = connection, Version = blueprint.Version }
            }
        };
    }
}
