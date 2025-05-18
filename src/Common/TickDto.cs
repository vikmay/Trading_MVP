namespace Common
{
    public record struct RawTick(string Symbol, decimal Bid, decimal Ask, long TsMs);
    public record struct UiTick(string Symbol, decimal Bid, decimal Ask, decimal Mid, decimal SpreadPct, long TsMs);
}