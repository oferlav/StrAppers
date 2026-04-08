using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace strAppersBackend.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions in this API. Does not POST to /api/Mentor/runtime-error — that endpoint
    /// is for student-deployed apps (Railway, etc.) only; forwarding platform 500s caused noise and recursion.
    /// </summary>
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred");
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // Error responses must still include CORS headers or the browser reports only "blocked by CORS"
            // and hides the real 500. Mirrors AllowFrontend rules in Program.cs for common origins.
            TryAddCorsHeadersForErrorResponse(context);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                error = "An error occurred while processing your request",
                message = exception.Message,
                stackTrace = _environment.IsDevelopment()
                    ? exception.StackTrace
                    : null
            };

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }

        /// <summary>
        /// When an exception is handled here, the normal CORS pipeline may not attach headers to the
        /// replacement response. Browsers then show a CORS error even though the root issue is server-side.
        /// </summary>
        private static void TryAddCorsHeadersForErrorResponse(HttpContext context)
        {
            if (context.Response.HasStarted) return;

            var origin = context.Request.Headers["Origin"].FirstOrDefault();
            if (string.IsNullOrEmpty(origin)) return;

            try
            {
                var uri = new Uri(origin);
                var host = uri.Host;
                // Keep in sync with AllowFrontend CORS origin rules in Program.cs (incl. skill-in.com).
                var allowed =
                    host.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith(".azurestaticapps.net", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("20.126.90.3", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("preview--skill-in-ce9dcf39.base44.app", StringComparison.OrdinalIgnoreCase) ||
                    host.Equals("skill-in.com", StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith(".skill-in.com", StringComparison.OrdinalIgnoreCase);

                if (!allowed) return;

                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
                context.Response.Headers["Vary"] = "Origin";
            }
            catch (UriFormatException)
            {
                // ignore invalid Origin
            }
        }
    }
}
