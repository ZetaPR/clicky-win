namespace Clicky.Core;

public sealed record PointTagParseResult(
    string SpokenText,
    int? X,
    int? Y,
    string? Label,
    int? ScreenNumber)
{
    public bool HasPoint => X.HasValue && Y.HasValue;
}
