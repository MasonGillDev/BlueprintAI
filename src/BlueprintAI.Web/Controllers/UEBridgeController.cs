using BlueprintAI.Application.Interfaces;
using BlueprintAI.Infrastructure.Providers;
using BlueprintAI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace BlueprintAI.Web.Controllers;

[ApiController]
[Route("api/ue")]
public class UEBridgeController : ControllerBase
{
    private readonly IUEBridgeService _bridge;
    private readonly ConfigPersistenceService _configService;
    private readonly AnthropicSettings _anthropicSettings;
    private readonly OpenAISettings _openaiSettings;
    private readonly OllamaSettings _ollamaSettings;

    public UEBridgeController(
        IUEBridgeService bridge,
        ConfigPersistenceService configService,
        AnthropicSettings anthropicSettings,
        OpenAISettings openaiSettings,
        OllamaSettings ollamaSettings)
    {
        _bridge = bridge;
        _configService = configService;
        _anthropicSettings = anthropicSettings;
        _openaiSettings = openaiSettings;
        _ollamaSettings = ollamaSettings;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var status = await _bridge.CheckConnectionAsync(ct);
        return Ok(status);
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] UEConnectionSettings settings, CancellationToken ct)
    {
        _bridge.Configure(settings);
        var status = await _bridge.CheckConnectionAsync(ct);

        // Persist UE URL on successful connect
        if (status.IsConnected)
        {
            _configService.SaveFrom(_anthropicSettings, _openaiSettings, _ollamaSettings, _bridge);
        }

        return Ok(status);
    }

    [HttpPost("disconnect")]
    public IActionResult Disconnect()
    {
        _bridge.Configure(new UEConnectionSettings { BaseUrl = "" });
        return Ok(new UEConnectionStatus { IsConnected = false });
    }

    [HttpGet("blueprints")]
    public async Task<IActionResult> ListBlueprints(CancellationToken ct)
    {
        try
        {
            var blueprints = await _bridge.ListBlueprintsAsync(ct);
            return Ok(blueprints);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { error = ex.Message });
        }
    }

    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        return Ok(_bridge.GetSettings());
    }
}
