using System.Collections.Concurrent;
using Common;

namespace Gateway.Services
{
    public interface IPriceCache
    {
        void Add(RawTick tick);
        IEnumerable<RawTick> GetSince(long lastSeq);
    }

    /// <summary>
    /// Simple in-memory ring buffer for the last N ticks.
    /// </summary>
    public sealed class PriceCache : IPriceCache
    {
        private const int MaxSize = 1_000;

        // Key = Seq, keeps natural order
        private readonly SortedDictionary<long, RawTick> _store = new();

        private readonly object _lock = new();

        public void Add(RawTick tick)
        {
            lock (_lock)
            {
                _store[tick.Seq] = tick;
                // trim old entries
                while (_store.Count > MaxSize)
                    _store.Remove(_store.Keys.First());
            }
        }

        public IEnumerable<RawTick> GetSince(long lastSeq)
        {
            lock (_lock)
            {
                return _store.Where(kv => kv.Key > lastSeq)
                             .Select(kv => kv.Value)
                             .ToList();
            }
        }
    }
}
