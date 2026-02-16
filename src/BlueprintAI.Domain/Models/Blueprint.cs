namespace BlueprintAI.Domain.Models;

public class Blueprint
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "NewBlueprint";
    public List<BlueprintNode> Nodes { get; set; } = new();
    public List<Connection> Connections { get; set; } = new();
    public List<BlueprintComment> Comments { get; set; } = new();
    public List<BlueprintVariable> Variables { get; set; } = new();
    public int Version { get; set; }
}
