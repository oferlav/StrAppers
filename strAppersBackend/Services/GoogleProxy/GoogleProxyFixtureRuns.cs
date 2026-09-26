using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace strAppersBackend.Services.GoogleProxy
{
    /// <summary>
    /// Fixture run ids switch the proxy to a simulated world (AgentWorlds row with Key = worldId) for Maps and Places calls.
    /// Format: "fx.{worldId}.{issuedUnixSeconds}.{nonce}.{signature}", signed with GoogleProxy:TokenSecret and bound to one board,
    /// so students cannot create them, and they expire (GoogleProxy:FixtureRunTtlMinutes) so a grading run id seen in a student's
    /// logs cannot be replayed later to explore the test world.
    /// The grader creates one per scenario run and sends it to the student backend as X-Run-Id.
    /// </summary>
    public static class GoogleProxyFixtureRuns
    {
        public enum Kind { NotFixture, Valid, Invalid }

        private static readonly Regex WorldIdPattern = new(@"^[a-z0-9-]{1,40}$", RegexOptions.Compiled);
        private static readonly Regex FixtureRunPattern = new(@"^fx\.([a-z0-9-]{1,40})\.(\d{1,12})\.([a-z0-9]{1,16})\.([A-Za-z0-9_-]{22})$", RegexOptions.Compiled);

        public static string Create(string boardId, string worldId, string secret, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("GoogleProxy:TokenSecret is not configured.");
            if (!WorldIdPattern.IsMatch(worldId ?? "")) throw new ArgumentException("Invalid world id.", nameof(worldId));
            var issued = now.ToUnixTimeSeconds().ToString();
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            return $"fx.{worldId}.{issued}.{nonce}.{Sign(boardId, worldId!, issued, nonce, secret)}";
        }

        public static Kind Parse(string? runId, string boardId, string secret, int ttlMinutes, DateTimeOffset now, out string? worldId)
        {
            worldId = null;
            if (runId == null || !runId.StartsWith("fx.", StringComparison.Ordinal)) return Kind.NotFixture;
            var m = FixtureRunPattern.Match(runId);
            if (!m.Success) return Kind.Invalid;

            var issuedText = m.Groups[2].Value;
            var issued = long.Parse(issuedText);
            var nowUnix = now.ToUnixTimeSeconds();
            if (issued > nowUnix + 60 || nowUnix - issued > ttlMinutes * 60L) return Kind.Invalid;

            var expected = Encoding.ASCII.GetBytes(Sign(boardId, m.Groups[1].Value, issuedText, m.Groups[3].Value, secret));
            if (!CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(m.Groups[4].Value))) return Kind.Invalid;

            worldId = m.Groups[1].Value;
            return Kind.Valid;
        }

        private static string Sign(string boardId, string worldId, string issued, string nonce, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"google-proxy:fixture:v1:{boardId}:{worldId}:{issued}:{nonce}"));
            return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_')[..22];
        }
    }
}
