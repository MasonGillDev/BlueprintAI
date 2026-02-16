using BlueprintAI.Application.Interfaces;
using BlueprintAI.Application.Services;
using BlueprintAI.Application.Tools.Handlers;
using Microsoft.Extensions.DependencyInjection;

// IUEBridgeService must be registered by the Infrastructure layer before AddApplication is called

namespace BlueprintAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            registry.Register(new CreateNodeHandler());
            registry.Register(new ConnectPinsHandler());
            registry.Register(new DeleteNodeHandler());
            registry.Register(new UpdateNodeHandler());
            registry.Register(new DisconnectPinsHandler());
            registry.Register(new CreateCommentHandler());
            registry.Register(new CreateVariableHandler());
            registry.Register(new AutoLayoutHandler());
            registry.Register(new AskUserHandler());
            registry.Register(new GetBlueprintStateHandler());

            var bridge = sp.GetRequiredService<IUEBridgeService>();
            registry.Register(new SyncFromUEHandler(bridge));
            registry.Register(new PushToUEHandler(bridge));

            return registry;
        });

        services.AddSingleton<ToolExecutor>();
        services.AddSingleton<AgentOrchestrator>();

        return services;
    }
}
