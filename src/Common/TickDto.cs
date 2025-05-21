namespace Common
{
    public record RawTick
    {
        public string Symbol { get; init; } = string.Empty;
        public double Bid { get; init; }
        public double Ask { get; init; }
        public double Mid { get; init; }
        public double SpreadPct { get; init; }
        public long TsMs { get; init; }

        // 🔹 Back-compat 4-arg constructor so legacy code still works
        public RawTick(string symbol, double bid, double ask, long tsMs)
        {
            Symbol = symbol;
            Bid = bid;
            Ask = ask;
            TsMs = tsMs;

            Mid = (bid + ask) / 2.0;
            SpreadPct = (ask - bid) / Mid * 100.0;
        }
    }
}