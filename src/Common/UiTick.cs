// src/Common/UiTick.cs
namespace Common;

/// <summary>
/// Shape sent to the React front-end.  
/// Includes bid / ask, a pre-calculated mid-price & spread, the epoch-ms
/// timestamp **and** the running sequence number so the UI can spot gaps.
/// </summary>
public record UiTick
{
    public string Symbol { get; init; } = string.Empty;
    public double Bid { get; init; }
    public double Ask { get; init; }
    public double Mid { get; init; }
    public double SpreadPct { get; init; }
    public long TsMs { get; init; }          // epoch-ms
    public long Seq { get; init; }          // NEW – gap detector
}
