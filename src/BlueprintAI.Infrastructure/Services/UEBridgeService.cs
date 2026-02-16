using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlueprintAI.Application.Interfaces;
using BlueprintAI.Domain.Models;

namespace BlueprintAI.Infrastructure.Services;

public class UEBridgeService : IUEBridgeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private UEConnectionSettings _settings = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public UEBridgeService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void Configure(UEConnectionSettings settings)
    {
        _settings = settings;
    }

    public UEConnectionSettings GetSettings() => _settings;

    public async Task<UEConnectionStatus> CheckConnectionAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_settings.BaseUrl))
            return new UEConnectionStatus { IsConnected = false, Error = "No URL configured" };

        try
        {
            var client = _httpClientFactory.CreateClient("UEBridge");
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync($"{_settings.BaseUrl}/api/status", ct);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                return new UEConnectionStatus
                {
                    IsConnected = true,
                    EngineVersion = body.TryGetProperty("engineVersion", out var v) ? v.GetString() : "Unknown"
                };
            }

            return new UEConnectionStatus { IsConnected = false, Error = $"HTTP {response.StatusCode}" };
        }
        catch (Exception ex)
        {
            return new UEConnectionStatus { IsConnected = false, Error = ex.Message };
        }
    }

    public async Task<List<UEBlueprintInfo>> ListBlueprintsAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("UEBridge");
        client.Timeout = TimeSpan.FromSeconds(10);
        var response = await client.GetAsync($"{_settings.BaseUrl}/api/blueprints", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<UEBlueprintInfo>>(JsonOpts, ct) ?? new();
    }

    public async Task<Blueprint> ImportBlueprintAsync(string name, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("UEBridge");
        client.Timeout = TimeSpan.FromSeconds(30);
        var response = await client.GetAsync(
            $"{_settings.BaseUrl}/api/blueprint?name={Uri.EscapeDataString(name)}", ct);
        response.EnsureSuccessStatusCode();
        var blueprint = await response.Content.ReadFromJsonAsync<Blueprint>(JsonOpts, ct);
        return blueprint ?? throw new InvalidOperationException("Failed to deserialize blueprint from UE");
    }

    public async Task<bool> PushDeltaAsync(string blueprintName, BlueprintDelta delta, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("UEBridge");
        client.Timeout = TimeSpan.FromSeconds(30);
        var response = await client.PostAsJsonAsync(
            $"{_settings.BaseUrl}/api/blueprint/apply?name={Uri.EscapeDataString(blueprintName)}",
            delta, JsonOpts, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PushFullBlueprintAsync(string blueprintName, Blueprint blueprint, CancellationToken ct = default)
    {
        var delta = new BlueprintDelta
        {
            Type = Domain.Enums.DeltaType.FullSync,
            FullState = blueprint,
            Version = blueprint.Version
        };
        return await PushDeltaAsync(blueprintName, delta, ct);
    }
}
