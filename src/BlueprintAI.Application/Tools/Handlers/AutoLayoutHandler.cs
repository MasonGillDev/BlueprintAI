using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class AutoLayoutHandler : IToolHandler
{
    public string Name => "auto_layout";
    public string Description => "Automatically arrange nodes in a left-to-right layout based on execution flow.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "spacing": { "type": "number", "description": "Horizontal spacing between nodes (default 300)" }
        }
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        if (blueprint.Nodes.Count == 0)
            return new ToolResult { Success = true, Message = "No nodes to layout" };

        var spacing = args.TryGetProperty("spacing", out var s) ? s.GetDouble() : 300;
        var verticalSpacing = 150.0;

        // Build adjacency from connections
        var outgoing = new Dictionary<string, List<string>>();
        var incoming = new Dictionary<string, HashSet<string>>();
        foreach (var node in blueprint.Nodes)
        {
            outgoing[node.Id] = new List<string>();
            incoming[node.Id] = new HashSet<string>();
        }
        foreach (var conn in blueprint.Connections)
        {
            if (outgoing.ContainsKey(conn.SourceNodeId) && incoming.ContainsKey(conn.TargetNodeId))
            {
                outgoing[conn.SourceNodeId].Add(conn.TargetNodeId);
                incoming[conn.TargetNodeId].Add(conn.SourceNodeId);
            }
        }

        // Topological sort (Kahn's algorithm)
        var roots = blueprint.Nodes.Where(n => incoming[n.Id].Count == 0).Select(n => n.Id).ToList();
        if (roots.Count == 0) roots.Add(blueprint.Nodes[0].Id);

        var levels = new Dictionary<string, int>();
        var queue = new Queue<string>(roots);
        foreach (var r in roots) levels[r] = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in outgoing[current])
            {
                var newLevel = levels[current] + 1;
                if (!levels.ContainsKey(next) || levels[next] < newLevel)
                {
                    levels[next] = newLevel;
                    queue.Enqueue(next);
                }
            }
        }

        // Assign positions for unvisited nodes
        foreach (var node in blueprint.Nodes)
        {
            if (!levels.ContainsKey(node.Id))
                levels[node.Id] = 0;
        }

        var byLevel = blueprint.Nodes.GroupBy(n => levels[n.Id]).OrderBy(g => g.Key);
        var deltas = new List<BlueprintDelta>();

        foreach (var group in byLevel)
        {
            var y = 100.0;
            foreach (var node in group)
            {
                node.PositionX = 100 + group.Key * spacing;
                node.PositionY = y;
                y += verticalSpacing;
                blueprint.Version++;
                deltas.Add(new BlueprintDelta
                {
                    Type = DeltaType.NodeUpdated,
                    Node = node,
                    Version = blueprint.Version
                });
            }
        }

        return new ToolResult
        {
            Success = true,
            Message = $"Arranged {blueprint.Nodes.Count} nodes across {byLevel.Count()} columns",
            Deltas = deltas
        };
    }
}
