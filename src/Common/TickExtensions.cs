namespace Common;

/// <summary>
/// Helpers for converting ticks between shapes.
/// </summary>
public static class TickExtensions
{
    /// <summary>
    /// Convert a <see cref="RawTick"/> from the Collector into the
    /// <see cref="UiTick"/> consumed by the Normaliser / web-UI.
    /// </summary>
    public static UiTick ToUi(this RawTick raw)
    {
        var mid = (raw.Bid + raw.Ask) / 2;
        var spreadPct = (raw.Ask - raw.Bid) / mid * 100;

        // NEW: object-initializer – no constructor needed
        return new UiTick
        {
            Symbol = raw.Symbol,
            Bid = raw.Bid,
            Ask = raw.Ask,
            Mid = mid,
            SpreadPct = spreadPct,
            TsMs = raw.TsMs
        };
    }
}
