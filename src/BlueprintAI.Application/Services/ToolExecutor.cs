using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Services;

public class ToolExecutor
{
    private readonly ToolRegistry _registry;

    public ToolExecutor(ToolRegistry registry)
    {
        _registry = registry;
    }

    public ToolResult Execute(string toolName, string argumentsJson, Blueprint blueprint)
    {
        var handler = _registry.GetHandler(toolName);
        if (handler == null)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Unknown tool: {toolName}"
            };
        }

        try
        {
            var arguments = JsonDocument.Parse(argumentsJson).RootElement;
            return handler.Execute(arguments, blueprint);
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Tool execution error: {ex.Message}"
            };
        }
    }

    public async Task<ToolResult> ExecuteAsync(string toolName, string argumentsJson, Blueprint blueprint, CancellationToken ct = default)
    {
        var handler = _registry.GetHandler(toolName);
        if (handler == null)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Unknown tool: {toolName}"
            };
        }

        try
        {
            var arguments = JsonDocument.Parse(argumentsJson).RootElement;
            if (handler is IAsyncToolHandler asyncHandler)
                return await asyncHandler.ExecuteAsync(arguments, blueprint, ct);
            return handler.Execute(arguments, blueprint);
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Tool execution error: {ex.Message}"
            };
        }
    }
}
