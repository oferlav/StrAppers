namespace strAppersBackend.Models
{
    /// <summary>
    /// Google API proxy for student backends (section "GoogleProxy").
    /// Student backends call Gemini / Maps / Places / Speech through /api/google-proxy with a per-board token
    /// instead of holding raw Google keys, so every call can be audited per run and later served from fixtures.
    /// The real keys are the existing GoogleApis:StudentBackendApiKey (Gemini) and GoogleApis:StudentMapsApiKey (Maps family).
    /// </summary>
    public class GoogleProxyConfig
    {
        /// <summary>Master switch. When false every proxy endpoint returns 503.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>HMAC secret for board tokens. Required when Enabled. Rotating it invalidates every issued token.</summary>
        public string TokenSecret { get; set; } = "";

        /// <summary>Hard cap on Google calls per (board, run id). The Integration Sheet promises 25.</summary>
        public int MaxCallsPerRun { get; set; } = 25;

        /// <summary>Hard cap on Google calls per board per UTC day, including calls without a run id.</summary>
        public int MaxCallsPerBoardPerDay { get; set; } = 2000;

        /// <summary>How long a run's call log stays readable after its last call.</summary>
        public int RunLogRetentionHours { get; set; } = 24;

        /// <summary>Maximum number of runs kept in memory across all boards; the oldest are evicted first.</summary>
        public int MaxRunsInMemory { get; set; } = 20000;

        /// <summary>Request and response bodies longer than this are truncated in the log (forwarding is never truncated).</summary>
        public int MaxLoggedBodyChars { get; set; } = 200_000;

        /// <summary>Secret sent as X-Grader-Key to /api/agent-grading. Grading is disabled while empty.</summary>
        public string GraderKey { get; set; } = "";

        /// <summary>How long a signed fixture run id (grading run) stays valid after the grader creates it.</summary>
        public int FixtureRunTtlMinutes { get; set; } = 60;

        /// <summary>Largest request body accepted from a student backend.</summary>
        public int MaxRequestBodyBytes { get; set; } = 10 * 1024 * 1024;
    }
}
