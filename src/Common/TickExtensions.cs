// src/Common/TickExtensions.cs
namespace Common;

/// <summary>
/// Helpers for converting ticks between shapes.
/// </summary>
public static class TickExtensions
{
    /// <summary>
    /// Convert a <see cref="RawTick"/> (produced by the Collector) into the
    /// <see cref="UiTick"/> consumed by the Normaliser and the web front-end.
    /// </summary>
    public static UiTick ToUi(this RawTick raw) =>
        new UiTick
        {
            Symbol = raw.Symbol,
            Bid = raw.Bid,
            Ask = raw.Ask,
            Mid = raw.Mid,
            SpreadPct = raw.SpreadPct,
            TsMs = raw.TsMs,
            Seq = raw.Seq          // pass the running number through!
        };
}
