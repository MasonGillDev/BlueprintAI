using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Tools.Handlers;

public class AskUserHandler : IToolHandler
{
    public string Name => "ask_user";
    public string Description => "Ask the user a clarifying question when more information is needed to build the blueprint.";

    public JsonDocument ParameterSchema => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "question": { "type": "string", "description": "The question to ask the user" }
        },
        "required": ["question"]
    }
    """);

    public ToolResult Execute(JsonElement args, Blueprint blueprint)
    {
        var question = args.GetProperty("question").GetString()!;
        return new ToolResult
        {
            Success = true,
            Message = question,
            AskUserQuestion = question
        };
    }
}
