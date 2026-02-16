using BlueprintAI.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BlueprintAI.Web.Controllers;

[ApiController]
[Route("api/ue")]
public class UEBridgeController : ControllerBase
{
    private readonly IUEBridgeService _bridge;

    public UEBridgeController(IUEBridgeService bridge)
    {
        _bridge = bridge;
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
