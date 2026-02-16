using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Interfaces;

public interface IBlueprintHub
{
    Task ReceiveTextDelta(string text);
    Task ReceiveBlueprintDelta(BlueprintDelta delta);
    Task ReceiveToolCallStarted(string toolName, string toolCallId);
    Task ReceiveToolCallCompleted(string toolName, string toolCallId, string result);
    Task ReceiveAskUser(string question);
    Task ReceiveError(string error);
    Task ReceiveStreamComplete();
}
