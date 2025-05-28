// src/Common/TickDto.cs
using System.Text.Json.Serialization;

namespace Common;

/// <summary>
/// Raw tick as received from the exchange‐stub WebSocket.
/// </summary>
public record RawTick
{
    // ── data ────────────────────────────────────────────────────────────────
    public string Symbol { get; init; } = string.Empty;
    public double Bid { get; init; }
    public double Ask { get; init; }
    public double Mid { get; init; }
    public double SpreadPct { get; init; }
    public long TsMs { get; init; }         // epoch-ms
    public long Seq { get; init; }         // monotonic seq-no for gap detection

    // ── required by System.Text.Json ---------------------------------------
    /// <summary>
    /// **Do not remove.**  System.Text.Json needs a parameter-less ctor for
    /// reflection-based deserialisation in <see cref="Collector.Worker"/>.
    /// </summary>
    public RawTick() { }

    // ── preferred usage inside Collector -----------------------------------
    [JsonConstructor]                                    // explicit – just in case
    public RawTick(string symbol,
                   double bid,
                   double ask,
                   long tsMs,
                   long seq)
    {
        Symbol = symbol;
        Bid = bid;
        Ask = ask;
        TsMs = tsMs;
        Seq = seq;

        Mid = (bid + ask) / 2.0;
        SpreadPct = (ask - bid) / Mid * 100.0;
    }

    /// <summary>
    /// Convenience ctor when the exchange doesn’t provide <c>Seq</c>.
    /// </summary>
    public RawTick(string symbol,
                   double bid,
                   double ask,
                   long tsMs)
        : this(symbol, bid, ask, tsMs, 0) { }
}
