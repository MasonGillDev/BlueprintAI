using System.Collections.Concurrent;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Application.Services;
using BlueprintAI.Domain.Enums;
using BlueprintAI.Domain.Models;
using BlueprintAI.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace BlueprintAI.Web.Hubs;

public class BlueprintHub : Hub<IBlueprintHub>
{
    private static readonly ConcurrentDictionary<string, SessionState> Sessions = new();
    private static readonly ConcurrentDictionary<string, string> ActiveProviders = new();
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveRequests = new();

    private readonly AgentOrchestrator _orchestrator;
    private readonly ChatProviderFactory _providerFactory;
    private readonly IUEBridgeService _ueBridge;

    public BlueprintHub(AgentOrchestrator orchestrator, ChatProviderFactory providerFactory, IUEBridgeService ueBridge)
    {
        _orchestrator = orchestrator;
        _providerFactory = providerFactory;
        _ueBridge = ueBridge;
    }

    public override async Task OnConnectedAsync()
    {
        var session = Sessions.GetOrAdd(Context.ConnectionId, _ => new SessionState());
        ActiveProviders.TryAdd(Context.ConnectionId, "anthropic");
        await Clients.Caller.ReceiveBlueprintDelta(new BlueprintDelta
        {
            Type = DeltaType.FullSync,
            FullState = session.Blueprint,
            Version = session.Blueprint.Version
        });
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Sessions.TryRemove(Context.ConnectionId, out _);
        ActiveProviders.TryRemove(Context.ConnectionId, out _);
        if (ActiveRequests.TryRemove(Context.ConnectionId, out var cts))
            cts.Cancel();
        return base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(string message)
    {
        if (!Sessions.TryGetValue(Context.ConnectionId, out var session))
            return;

        // Cancel any existing request
        if (ActiveRequests.TryRemove(Context.ConnectionId, out var existingCts))
            existingCts.Cancel();

        var cts = new CancellationTokenSource();
        ActiveRequests[Context.ConnectionId] = cts;

        var providerId = ActiveProviders.GetValueOrDefault(Context.ConnectionId, "anthropic");
        IChatProvider provider;
        try
        {
            provider = _providerFactory.GetProvider(providerId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.ReceiveError($"Provider error: {ex.Message}");
            return;
        }

        try
        {
            await _orchestrator.ProcessMessageAsync(
                message,
                session,
                provider,
                onTextDelta: text => Clients.Caller.ReceiveTextDelta(text),
                onBlueprintDelta: delta => Clients.Caller.ReceiveBlueprintDelta(delta),
                onToolCallStarted: (name, id) => Clients.Caller.ReceiveToolCallStarted(name, id),
                onToolCallCompleted: (name, id, result) => Clients.Caller.ReceiveToolCallCompleted(name, id, result),
                onAskUser: question => Clients.Caller.ReceiveAskUser(question),
                onComplete: () => Clients.Caller.ReceiveStreamComplete(),
                ct: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignored - user cancelled
        }
        catch (Exception ex)
        {
            await Clients.Caller.ReceiveError($"Error: {ex.Message}");
            await Clients.Caller.ReceiveStreamComplete();
        }
        finally
        {
            ActiveRequests.TryRemove(Context.ConnectionId, out _);
        }
    }

    public async Task Undo()
    {
        if (!Sessions.TryGetValue(Context.ConnectionId, out var session))
            return;

        var result = _orchestrator.Undo(session);
        if (result != null)
        {
            await Clients.Caller.ReceiveBlueprintDelta(new BlueprintDelta
            {
                Type = DeltaType.FullSync,
                FullState = result,
                Version = result.Version
            });
        }
    }

    public async Task Redo()
    {
        if (!Sessions.TryGetValue(Context.ConnectionId, out var session))
            return;

        var result = _orchestrator.Redo(session);
        if (result != null)
        {
            await Clients.Caller.ReceiveBlueprintDelta(new BlueprintDelta
            {
                Type = DeltaType.FullSync,
                FullState = result,
                Version = result.Version
            });
        }
    }

    public async Task SetProvider(string providerId)
    {
        ActiveProviders[Context.ConnectionId] = providerId;
        await Task.CompletedTask;
    }

    public async Task CancelRequest()
    {
        if (ActiveRequests.TryRemove(Context.ConnectionId, out var cts))
        {
            cts.Cancel();
            await Clients.Caller.ReceiveStreamComplete();
        }
    }

    public async Task ImportFromUE(string blueprintName)
    {
        if (!Sessions.TryGetValue(Context.ConnectionId, out var session))
            return;

        try
        {
            var imported = await _ueBridge.ImportBlueprintAsync(blueprintName);

            session.SaveSnapshot();
            session.Blueprint.Nodes.Clear();
            session.Blueprint.Nodes.AddRange(imported.Nodes);
            session.Blueprint.Connections.Clear();
            session.Blueprint.Connections.AddRange(imported.Connections);
            session.Blueprint.Comments.Clear();
            session.Blueprint.Comments.AddRange(imported.Comments);
            session.Blueprint.Variables.Clear();
            session.Blueprint.Variables.AddRange(imported.Variables);
            session.Blueprint.Name = imported.Name;
            session.Blueprint.Version++;

            await Clients.Caller.ReceiveBlueprintDelta(new BlueprintDelta
            {
                Type = DeltaType.FullSync,
                FullState = session.Blueprint,
                Version = session.Blueprint.Version
            });
        }
        catch (Exception ex)
        {
            await Clients.Caller.ReceiveError($"Failed to import from UE: {ex.Message}");
        }
    }
}
