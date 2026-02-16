using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class DeleteNodeHandler : IToolHandler
{
    public string Name => "delete_node";
    public string Description => "Delete a node and all its connections from the blueprint.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "nodeId": { "type": "string", "description": "ID of the node to delete" }
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

        var deltas = new List<BlueprintDelta>();

        var connections = blueprint.Connections
            .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
            .ToList();

        foreach (var conn in connections)
        {
            blueprint.Connections.Remove(conn);
            blueprint.Version++;
            deltas.Add(new BlueprintDelta
            {
                Type = DeltaType.ConnectionRemoved,
                RemovedId = conn.Id,
                Version = blueprint.Version
            });
        }

        blueprint.Nodes.Remove(node);
        blueprint.Version++;
        deltas.Add(new BlueprintDelta
        {
            Type = DeltaType.NodeRemoved,
            RemovedId = nodeId,
            Version = blueprint.Version
        });

        return new ToolResult
        {
            Success = true,
            Message = $"Deleted node '{node.Title}' and {connections.Count} connection(s)",
            Deltas = deltas
        };
    }
}
