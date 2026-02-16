using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class CreateCommentHandler : IToolHandler
{
    public string Name => "create_comment";
    public string Description => "Add a comment box to the blueprint canvas.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "text": { "type": "string", "description": "Comment text" },
            "positionX": { "type": "number" },
            "positionY": { "type": "number" },
            "width": { "type": "number" },
            "height": { "type": "number" },
            "color": { "type": "string", "description": "Hex color for the comment box" }
        },
        "required": ["text"]
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var comment = new BlueprintComment
        {
            Text = args.GetProperty("text").GetString()!,
            PositionX = args.TryGetProperty("positionX", out var px) ? px.GetDouble() : 0,
            PositionY = args.TryGetProperty("positionY", out var py) ? py.GetDouble() : 0,
            Width = args.TryGetProperty("width", out var w) ? w.GetDouble() : 400,
            Height = args.TryGetProperty("height", out var h) ? h.GetDouble() : 200,
            Color = args.TryGetProperty("color", out var c) ? c.GetString()! : "#FFFFFF"
        };

        blueprint.Comments.Add(comment);
        blueprint.Version++;

        return new ToolResult
        {
            Success = true,
            Message = $"Created comment '{comment.Text}'",
            Deltas = new List<BlueprintDelta>
            {
                new() { Type = DeltaType.CommentAdded, Comment = comment, Version = blueprint.Version }
            }
        };
    }
}
