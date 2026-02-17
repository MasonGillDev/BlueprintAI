using BlueprintAI.Application.Interfaces;
using BlueprintAI.Infrastructure.Providers;
using BlueprintAI.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BlueprintAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddSingleton<AnthropicSettings>();
        services.AddSingleton<OpenAISettings>();
        services.AddSingleton<OllamaSettings>();

        services.AddSingleton<AnthropicChatProvider>(sp =>
            new AnthropicChatProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Anthropic"),
                sp.GetRequiredService<AnthropicSettings>()));

        services.AddSingleton<OpenAIChatProvider>(sp =>
            new OpenAIChatProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("OpenAI"),
                sp.GetRequiredService<OpenAISettings>()));

        services.AddSingleton<OllamaChatProvider>(sp =>
            new OllamaChatProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama"),
                sp.GetRequiredService<OllamaSettings>()));

        services.AddSingleton<ChatProviderFactory>();

        services.AddSingleton<IUEBridgeService>(sp =>
            new UEBridgeService(sp.GetRequiredService<IHttpClientFactory>()));

        services.AddSingleton<ConfigPersistenceService>();

        return services;
    }
}

public class ChatProviderFactory
{
    private readonly AnthropicChatProvider _anthropic;
    private readonly OpenAIChatProvider _openai;
    private readonly OllamaChatProvider _ollama;

    public ChatProviderFactory(
        AnthropicChatProvider anthropic,
        OpenAIChatProvider openai,
        OllamaChatProvider ollama)
    {
        _anthropic = anthropic;
        _openai = openai;
        _ollama = ollama;
    }

    public IChatProvider GetProvider(string providerId)
    {
        return providerId switch
        {
            "anthropic" => _anthropic,
            "openai" => _openai,
            "ollama" => _ollama,
            _ => throw new ArgumentException($"Unknown provider: {providerId}")
        };
    }

    public AnthropicChatProvider Anthropic => _anthropic;
    public OpenAIChatProvider OpenAI => _openai;
    public OllamaChatProvider Ollama => _ollama;
}
