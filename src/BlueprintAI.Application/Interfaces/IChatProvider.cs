using System.Text.Json;

namespace BlueprintAI.Application.Interfaces;

public class CompletionChunk
{
    public string? TextDelta { get; set; }
    public ToolCallInfo? ToolCall { get; set; }
    public bool IsComplete { get; set; }
    public string? StopReason { get; set; }
}

public class ToolCallInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public bool IsComplete { get; set; }
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string? Content { get; set; }
    public List<ToolCallInfo>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}

public class ToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonDocument Schema { get; set; } = JsonDocument.Parse("{}");
}

public interface IChatProvider
{
    string ProviderId { get; }
    bool SupportsNativeToolCalling { get; }
    IAsyncEnumerable<CompletionChunk> StreamCompletionAsync(
        List<ChatMessage> messages,
        List<ToolDefinition> tools,
        string systemPrompt,
        CancellationToken ct = default);
}
