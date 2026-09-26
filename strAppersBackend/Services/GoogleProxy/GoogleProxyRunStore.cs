using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;

namespace strAppersBackend.Services.GoogleProxy
{
    public class GoogleProxyCallLog
    {
        public DateTime At { get; set; }
        public string Service { get; set; } = "";
        public string Method { get; set; } = "";
        public string Path { get; set; } = "";
        public int Status { get; set; }
        public long DurationMs { get; set; }
        public string RequestBody { get; set; } = "";
        public string ResponseBody { get; set; } = "";
    }

    /// <summary>
    /// In-memory call log per (board, run id) plus the per-run and per-board-per-day call counters.
    /// Singleton. Logs are lost on app pool recycle; acceptable while grading reads a run right after it finishes.
    /// </summary>
    public class GoogleProxyRunStore : IDisposable
    {
        private class RunLog
        {
            public readonly List<GoogleProxyCallLog> Calls = new();
            public int Reserved;
        }

        private class DailyCounter { public int Count; }

        private readonly MemoryCache _cache;
        private readonly GoogleProxyConfig _config;
        private readonly object _createLock = new();

        public GoogleProxyRunStore(IOptions<GoogleProxyConfig> config)
        {
            _config = config.Value;
            _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = Math.Max(1, _config.MaxRunsInMemory) });
        }

        public enum ReserveResult { Ok, RunLimit, DailyLimit }

        /// <summary>Counts one call against the board's daily cap and, when runId is given, the run cap. Atomic per board/run.</summary>
        public ReserveResult TryReserve(string boardId, string? runId)
        {
            var daily = GetOrCreate("day:" + boardId + ":" + DateTime.UtcNow.ToString("yyyyMMdd"),
                () => new DailyCounter(), TimeSpan.FromHours(25), sliding: false);
            RunLog? run = runId == null ? null : GetOrCreate(RunKey(boardId, runId),
                () => new RunLog(), TimeSpan.FromHours(_config.RunLogRetentionHours), sliding: true);

            lock (daily)
            {
                if (daily.Count >= _config.MaxCallsPerBoardPerDay) return ReserveResult.DailyLimit;
                if (run != null)
                {
                    lock (run)
                    {
                        if (run.Reserved >= _config.MaxCallsPerRun) return ReserveResult.RunLimit;
                        run.Reserved++;
                    }
                }
                daily.Count++;
            }
            return ReserveResult.Ok;
        }

        public void Record(string boardId, string runId, GoogleProxyCallLog call)
        {
            if (!_cache.TryGetValue(RunKey(boardId, runId), out RunLog? run) || run == null) return;
            lock (run) run.Calls.Add(call);
        }

        /// <summary>A snapshot of the run's calls in call order, or null when the run is unknown or expired.</summary>
        public List<GoogleProxyCallLog>? GetRun(string boardId, string runId)
        {
            if (!_cache.TryGetValue(RunKey(boardId, runId), out RunLog? run) || run == null) return null;
            lock (run) return run.Calls.OrderBy(c => c.At).ToList();
        }

        private static string RunKey(string boardId, string runId) => "run:" + boardId + ":" + runId;

        private T GetOrCreate<T>(string key, Func<T> factory, TimeSpan expiry, bool sliding) where T : class
        {
            if (_cache.TryGetValue(key, out T? existing) && existing != null) return existing;
            lock (_createLock)
            {
                if (_cache.TryGetValue(key, out existing) && existing != null) return existing;
                var created = factory();
                var options = new MemoryCacheEntryOptions { Size = 1 };
                if (sliding) options.SlidingExpiration = expiry; else options.AbsoluteExpirationRelativeToNow = expiry;
                _cache.Set(key, created, options);
                return created;
            }
        }

        public void Dispose() => _cache.Dispose();
    }
}
