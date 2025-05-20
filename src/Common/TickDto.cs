namespace Common
{
    public record RawTick
    {
        public string Symbol { get; init; } = string.Empty;
        public double Bid { get; init; }
        public double Ask { get; init; }
        public long TsMs { get; init; }
    }
}