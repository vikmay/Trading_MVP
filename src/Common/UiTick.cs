namespace Common;

/// <summary>
/// Shape of the message the front-end actually consumes
/// (mid-price already calculated, so UI doesn't have to).
/// </summary>
public record UiTick
{
    public string Symbol { get; init; } = string.Empty;
    public double Mid    { get; init; }
    public long   TsMs   { get; init; }   // epoch-ms timestamp
}
