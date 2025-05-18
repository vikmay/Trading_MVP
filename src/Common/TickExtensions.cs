namespace Common
{
    public static class TickExtensions
    {
        public static UiTick ToUi(this RawTick rawTick)
        {
            var mid = (rawTick.Bid + rawTick.Ask) / 2;
            var spreadPct = (rawTick.Ask - rawTick.Bid) / rawTick.Bid * 100;
            return new UiTick(rawTick.Symbol, rawTick.Bid, rawTick.Ask, mid, spreadPct, rawTick.TsMs);
        }
    }
}
