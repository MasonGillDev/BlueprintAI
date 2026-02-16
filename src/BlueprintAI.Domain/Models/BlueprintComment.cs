namespace BlueprintAI.Domain.Models;

public class BlueprintComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 200;
    public string Color { get; set; } = "#FFFFFF";
}
