using System;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using strAppersBackend;

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
        private readonly IConfiguration _configuration;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
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
        private void TryAddCorsHeadersForErrorResponse(HttpContext context)
        {
            if (context.Response.HasStarted) return;

            var origin = context.Request.Headers["Origin"].FirstOrDefault();
            if (string.IsNullOrEmpty(origin)) return;

            Uri? apiBase = null;
            var apiBaseStr = _configuration["ApiBaseUrl"];
            if (!string.IsNullOrWhiteSpace(apiBaseStr) && Uri.TryCreate(apiBaseStr.Trim(), UriKind.Absolute, out var u))
            {
                apiBase = u;
            }

            var extra = CorsOriginHelper.GetExtraOrigins(_configuration);
            if (!CorsOriginHelper.IsOriginAllowed(origin, apiBase, extra)) return;

            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
            context.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            context.Response.Headers["Vary"] = "Origin";
        }
    }
}
