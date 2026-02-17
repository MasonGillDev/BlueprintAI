using BlueprintAI.Domain.Models;

namespace BlueprintAI.Application.Interfaces;

public class UEConnectionSettings
{
    public string BaseUrl { get; set; } = "http://localhost:8089";
}

public class UEConnectionStatus
{
    public bool IsConnected { get; set; }
    public string? EngineVersion { get; set; }
    public string? Error { get; set; }
}

public class UEBlueprintInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class UECreateBlueprintResult
{
    public bool Success { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public interface IUEBridgeService
{
    Task<UEConnectionStatus> CheckConnectionAsync(CancellationToken ct = default);
    Task<List<UEBlueprintInfo>> ListBlueprintsAsync(CancellationToken ct = default);
    Task<Blueprint> ImportBlueprintAsync(string name, CancellationToken ct = default);
    Task<bool> PushDeltaAsync(string blueprintName, BlueprintDelta delta, CancellationToken ct = default);
    Task<bool> PushFullBlueprintAsync(string blueprintName, Blueprint blueprint, CancellationToken ct = default);
    Task<UECreateBlueprintResult> CreateBlueprintAsync(string name, string path, string parentClass, Blueprint? initialState, CancellationToken ct = default);
    void Configure(UEConnectionSettings settings);
    UEConnectionSettings GetSettings();
}
