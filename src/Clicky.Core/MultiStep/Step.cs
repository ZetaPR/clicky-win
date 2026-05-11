namespace Clicky.Core;

public sealed class Step
{
    public required int Number { get; init; }
    public required string Text { get; init; }
    public int? X { get; init; }
    public int? Y { get; init; }
    public string? Label { get; init; }

    public bool HasCoords => X.HasValue && Y.HasValue;
}
