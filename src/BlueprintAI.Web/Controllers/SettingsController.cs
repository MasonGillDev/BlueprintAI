using BlueprintAI.Application.Interfaces;
using BlueprintAI.Infrastructure;
using BlueprintAI.Infrastructure.Providers;
using BlueprintAI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlueprintAI.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ChatProviderFactory _providerFactory;
    private readonly AnthropicSettings _anthropicSettings;
    private readonly OpenAISettings _openaiSettings;
    private readonly OllamaSettings _ollamaSettings;
    private readonly ConfigPersistenceService _configService;
    private readonly IUEBridgeService _ueBridge;

    public SettingsController(
        ChatProviderFactory providerFactory,
        AnthropicSettings anthropicSettings,
        OpenAISettings openaiSettings,
        OllamaSettings ollamaSettings,
        ConfigPersistenceService configService,
        IUEBridgeService ueBridge)
    {
        _providerFactory = providerFactory;
        _anthropicSettings = anthropicSettings;
        _openaiSettings = openaiSettings;
        _ollamaSettings = ollamaSettings;
        _configService = configService;
        _ueBridge = ueBridge;
    }

    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        return Ok(new[]
        {
            new
            {
                id = "anthropic",
                name = "Anthropic",
                models = new[] { "claude-sonnet-4-20250514", "claude-haiku-4-20250414", "claude-opus-4-20250514" },
                hasApiKey = !string.IsNullOrEmpty(_anthropicSettings.ApiKey)
            },
            new
            {
                id = "openai",
                name = "OpenAI",
                models = new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo" },
                hasApiKey = !string.IsNullOrEmpty(_openaiSettings.ApiKey)
            },
            new
            {
                id = "ollama",
                name = "Ollama (Local)",
                models = new[] { "llama3", "mistral", "codellama" },
                hasApiKey = true
            }
        });
    }

    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _configService.Load();
        return Ok(new
        {
            activeProvider = config.ActiveProvider ?? "anthropic",
            ueBaseUrl = _ueBridge.GetSettings().BaseUrl
        });
    }

    [HttpPost("provider/{providerId}")]
    public IActionResult UpdateProvider(string providerId, [FromBody] ProviderSettingsDto dto)
    {
        switch (providerId)
        {
            case "anthropic":
                if (dto.ApiKey != null) _anthropicSettings.ApiKey = dto.ApiKey;
                if (dto.Model != null) _anthropicSettings.Model = dto.Model;
                _providerFactory.Anthropic.UpdateSettings(_anthropicSettings);
                break;
            case "openai":
                if (dto.ApiKey != null) _openaiSettings.ApiKey = dto.ApiKey;
                if (dto.Model != null) _openaiSettings.Model = dto.Model;
                if (dto.BaseUrl != null) _openaiSettings.BaseUrl = dto.BaseUrl;
                _providerFactory.OpenAI.UpdateSettings(_openaiSettings);
                break;
            case "ollama":
                if (dto.Model != null) _ollamaSettings.Model = dto.Model;
                if (dto.BaseUrl != null) _ollamaSettings.BaseUrl = dto.BaseUrl;
                _providerFactory.Ollama.UpdateSettings(_ollamaSettings);
                break;
            default:
                return NotFound($"Unknown provider: {providerId}");
        }

        // Persist to disk
        _configService.SaveFrom(_anthropicSettings, _openaiSettings, _ollamaSettings, _ueBridge, providerId);

        return Ok();
    }
}

public class ProviderSettingsDto
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
}
