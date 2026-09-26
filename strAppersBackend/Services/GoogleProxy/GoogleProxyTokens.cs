using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace strAppersBackend.Services.GoogleProxy
{
    /// <summary>
    /// Stateless per-board proxy tokens: "{boardId}.{signature}", signature = base64url(HMAC-SHA256(secret, "google-proxy:v1:" + boardId)).
    /// Provisioning (BoardsController, where the Google env vars are set on Railway) will call Create and set the result
    /// as GOOGLE_PROXY_TOKEN on the student service. No DB row is needed; rotating GoogleProxy:TokenSecret revokes all tokens.
    /// </summary>
    public static class GoogleProxyTokens
    {
        private static readonly Regex BoardIdPattern = new(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

        public static string Create(string boardId, string secret)
        {
            if (string.IsNullOrWhiteSpace(secret)) throw new InvalidOperationException("GoogleProxy:TokenSecret is not configured.");
            if (boardId == null || !BoardIdPattern.IsMatch(boardId)) throw new ArgumentException("Invalid board id.", nameof(boardId));
            return boardId + "." + Sign(boardId, secret);
        }

        /// <summary>Returns the board id when the token is valid, otherwise null.</summary>
        public static string? Validate(string? token, string secret)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(secret)) return null;
            var dot = token.IndexOf('.');
            if (dot <= 0 || dot == token.Length - 1) return null;
            var boardId = token[..dot];
            if (!BoardIdPattern.IsMatch(boardId)) return null;
            var expected = Encoding.ASCII.GetBytes(Sign(boardId, secret));
            var actual = Encoding.ASCII.GetBytes(token[(dot + 1)..]);
            return CryptographicOperations.FixedTimeEquals(expected, actual) ? boardId : null;
        }

        private static string Sign(string boardId, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes("google-proxy:v1:" + boardId));
            return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
