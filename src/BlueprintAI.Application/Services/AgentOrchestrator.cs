using System.Text.Json;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Services;

public class SessionState
{
    public Blueprint Blueprint { get; } = new();
    public List<ChatMessage> Messages { get; } = new();
    public Stack<string> UndoStack { get; } = new();
    public Stack<string> RedoStack { get; } = new();

    public void SaveSnapshot()
    {
        UndoStack.Push(JsonSerializer.Serialize(Blueprint));
        RedoStack.Clear();
    }
}

public class AgentOrchestrator
{
    private readonly ToolRegistry _toolRegistry;
    private readonly ToolExecutor _toolExecutor;

    public AgentOrchestrator(ToolRegistry toolRegistry, ToolExecutor toolExecutor)
    {
        _toolRegistry = toolRegistry;
        _toolExecutor = toolExecutor;
    }

    public async Task ProcessMessageAsync(
        string userMessage,
        SessionState session,
        IChatProvider provider,
        Func<string, Task> onTextDelta,
        Func<BlueprintDelta, Task> onBlueprintDelta,
        Func<string, string, Task> onToolCallStarted,
        Func<string, string, string, Task> onToolCallCompleted,
        Func<string, Task> onAskUser,
        Func<Task> onComplete,
        CancellationToken ct = default)
    {
        session.Messages.Add(new ChatMessage { Role = "user", Content = userMessage });

        var tools = _toolRegistry.GetToolDefinitions();
        var maxIterations = 10;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var fullText = new System.Text.StringBuilder();
            var toolCalls = new List<ToolCallInfo>();
            var currentToolArguments = new Dictionary<string, System.Text.StringBuilder>();

            await foreach (var chunk in provider.StreamCompletionAsync(
                session.Messages, tools, SystemPrompt.Value, ct))
            {
                if (chunk.TextDelta != null)
                {
                    fullText.Append(chunk.TextDelta);
                    await onTextDelta(chunk.TextDelta);
                }

                if (chunk.ToolCall != null)
                {
                    var tc = chunk.ToolCall;
                    if (!currentToolArguments.ContainsKey(tc.Id))
                    {
                        currentToolArguments[tc.Id] = new System.Text.StringBuilder();
                        toolCalls.Add(tc);
                        await onToolCallStarted(tc.Name, tc.Id);
                    }

                    if (!tc.IsComplete)
                    {
                        currentToolArguments[tc.Id].Append(tc.ArgumentsJson);
                    }
                    else
                    {
                        var existingTc = toolCalls.First(t => t.Id == tc.Id);
                        existingTc.ArgumentsJson = currentToolArguments[tc.Id].ToString();
                        existingTc.IsComplete = true;
                    }
                }
            }

            // Add assistant message
            var assistantMsg = new ChatMessage
            {
                Role = "assistant",
                Content = fullText.Length > 0 ? fullText.ToString() : null,
                ToolCalls = toolCalls.Count > 0 ? toolCalls : null
            };
            session.Messages.Add(assistantMsg);

            // If no tool calls, we're done
            if (toolCalls.Count == 0)
            {
                await onComplete();
                return;
            }

            // Execute tool calls
            var hasAskUser = false;
            foreach (var tc in toolCalls.Where(t => t.IsComplete))
            {
                session.SaveSnapshot();
                var result = await _toolExecutor.ExecuteAsync(tc.Name, tc.ArgumentsJson, session.Blueprint, ct);

                foreach (var delta in result.Deltas)
                {
                    await onBlueprintDelta(delta);
                }

                if (result.AskUserQuestion != null)
                {
                    await onAskUser(result.AskUserQuestion);
                    hasAskUser = true;
                }

                await onToolCallCompleted(tc.Name, tc.Id, result.Message);

                // Add tool result message
                session.Messages.Add(new ChatMessage
                {
                    Role = "tool",
                    Content = result.Message,
                    ToolCallId = tc.Id,
                    ToolName = tc.Name
                });
            }

            // If ask_user was called, stop and wait for user response
            if (hasAskUser)
            {
                await onComplete();
                return;
            }

            // Otherwise loop back to get the next LLM response
        }

        await onComplete();
    }

    public Blueprint? Undo(SessionState session)
    {
        if (session.UndoStack.Count == 0) return null;
        session.RedoStack.Push(JsonSerializer.Serialize(session.Blueprint));
        var state = JsonSerializer.Deserialize<Blueprint>(session.UndoStack.Pop())!;
        CopyBlueprintState(state, session.Blueprint);
        session.Blueprint.Version++;
        return session.Blueprint;
    }

    public Blueprint? Redo(SessionState session)
    {
        if (session.RedoStack.Count == 0) return null;
        session.UndoStack.Push(JsonSerializer.Serialize(session.Blueprint));
        var state = JsonSerializer.Deserialize<Blueprint>(session.RedoStack.Pop())!;
        CopyBlueprintState(state, session.Blueprint);
        session.Blueprint.Version++;
        return session.Blueprint;
    }

    private static void CopyBlueprintState(Blueprint source, Blueprint target)
    {
        target.Nodes.Clear();
        target.Nodes.AddRange(source.Nodes);
        target.Connections.Clear();
        target.Connections.AddRange(source.Connections);
        target.Comments.Clear();
        target.Comments.AddRange(source.Comments);
        target.Variables.Clear();
        target.Variables.AddRange(source.Variables);
    }
}
