using System.Text.Json;
using System.Text.Json.Serialization;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Infrastructure.Providers;

namespace BlueprintAI.Infrastructure.Services;

public class PersistedConfig
{
    public string? AnthropicApiKey { get; set; }
    public string? AnthropicModel { get; set; }
    public string? OpenAIApiKey { get; set; }
    public string? OpenAIModel { get; set; }
    public string? OpenAIBaseUrl { get; set; }
    public string? OllamaModel { get; set; }
    public string? OllamaBaseUrl { get; set; }
    public string? UEBridgeBaseUrl { get; set; }
    public string? ActiveProvider { get; set; }
}

public class ConfigPersistenceService
{
    private readonly string _configPath;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConfigPersistenceService(string? configPath = null)
    {
        _configPath = configPath ?? Path.Combine(
            AppContext.BaseDirectory, "blueprintai-config.json");
    }

    public PersistedConfig Load()
    {
        if (!File.Exists(_configPath))
            return new PersistedConfig();

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<PersistedConfig>(json, JsonOpts) ?? new PersistedConfig();
        }
        catch
        {
            return new PersistedConfig();
        }
    }

    public void Save(PersistedConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOpts);
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to save config: {ex.Message}");
        }
    }

    public void ApplyTo(
        AnthropicSettings anthropic,
        OpenAISettings openai,
        OllamaSettings ollama,
        IUEBridgeService ueBridge)
    {
        var config = Load();

        if (!string.IsNullOrEmpty(config.AnthropicApiKey)) anthropic.ApiKey = config.AnthropicApiKey;
        if (!string.IsNullOrEmpty(config.AnthropicModel)) anthropic.Model = config.AnthropicModel;
        if (!string.IsNullOrEmpty(config.OpenAIApiKey)) openai.ApiKey = config.OpenAIApiKey;
        if (!string.IsNullOrEmpty(config.OpenAIModel)) openai.Model = config.OpenAIModel;
        if (!string.IsNullOrEmpty(config.OpenAIBaseUrl)) openai.BaseUrl = config.OpenAIBaseUrl;
        if (!string.IsNullOrEmpty(config.OllamaModel)) ollama.Model = config.OllamaModel;
        if (!string.IsNullOrEmpty(config.OllamaBaseUrl)) ollama.BaseUrl = config.OllamaBaseUrl;
        if (!string.IsNullOrEmpty(config.UEBridgeBaseUrl))
            ueBridge.Configure(new UEConnectionSettings { BaseUrl = config.UEBridgeBaseUrl });
    }

    public void SaveFrom(
        AnthropicSettings anthropic,
        OpenAISettings openai,
        OllamaSettings ollama,
        IUEBridgeService ueBridge,
        string? activeProvider = null)
    {
        var config = new PersistedConfig
        {
            AnthropicApiKey = string.IsNullOrEmpty(anthropic.ApiKey) ? null : anthropic.ApiKey,
            AnthropicModel = anthropic.Model,
            OpenAIApiKey = string.IsNullOrEmpty(openai.ApiKey) ? null : openai.ApiKey,
            OpenAIModel = openai.Model,
            OpenAIBaseUrl = openai.BaseUrl,
            OllamaModel = ollama.Model,
            OllamaBaseUrl = ollama.BaseUrl,
            UEBridgeBaseUrl = ueBridge.GetSettings().BaseUrl,
            ActiveProvider = activeProvider
        };
        Save(config);
    }
}
