using BlueprintAI.Application.Interfaces;

namespace BlueprintAI.Application.Services;

public class ToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers = new();

    public void Register(IToolHandler handler)
    {
        _handlers[handler.Name] = handler;
    }

    public IToolHandler? GetHandler(string name)
    {
        return _handlers.GetValueOrDefault(name);
    }

    public IReadOnlyList<IToolHandler> GetAll() => _handlers.Values.ToList();

    public List<ToolDefinition> GetToolDefinitions()
    {
        return _handlers.Values.Select(h => new ToolDefinition
        {
            Name = h.Name,
            Description = h.Description,
            Schema = h.ParameterSchema
        }).ToList();
    }
}
