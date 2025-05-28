// src/Common/TickDto.cs
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
        public long Seq { get; init; }          // NEW – gap detector

        // Needed for System.Text.Json
        public RawTick() { }

        /// <summary>Preferred constructor (includes Seq).</summary>
        public RawTick(string symbol, double bid, double ask, long tsMs, long seq)
        {
            Symbol = symbol;
            Bid = bid;
            Ask = ask;
            TsMs = tsMs;
            Seq = seq;

            Mid = (bid + ask) / 2.0;
            SpreadPct = (ask - bid) / Mid * 100.0;
        }

        public RawTick(string symbol, double bid, double ask, long tsMs)
            : this(symbol, bid, ask, tsMs, 0) { }
    }
}
