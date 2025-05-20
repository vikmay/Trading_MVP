namespace Common;

/// <summary>
/// Shape of the message the React front-end consumes.
/// Includes bid/ask, a pre-calculated mid-price and spread % so the UI
/// doesn’t have to do any math.
/// </summary>
public record UiTick
{
    public string Symbol { get; init; } = string.Empty;
    public double Bid { get; init; }
    public double Ask { get; init; }
    public double Mid { get; init; }
    public double SpreadPct { get; init; }
    public long TsMs { get; init; }   // epoch-ms timestamp
}
