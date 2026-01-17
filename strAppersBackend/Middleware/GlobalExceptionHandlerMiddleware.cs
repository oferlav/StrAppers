using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace strAppersBackend.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
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
            // Get the API base URL from configuration
            var apiBaseUrl = _configuration["ApiBaseUrl"];
            
            // If ApiBaseUrl is configured, construct the error endpoint URL and send error details
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                // Construct the full endpoint URL
                var errorEndpointUrl = $"{apiBaseUrl.TrimEnd('/')}/api/Mentor/runtime-error";
                
                try
                {
                    await SendErrorToEndpointAsync(errorEndpointUrl, context, exception);
                }
                catch (Exception sendEx)
                {
                    _logger.LogError(sendEx, "Failed to send error to endpoint: {Endpoint}", errorEndpointUrl);
                    // Don't fail the request if error reporting fails
                }
            }

            // Return error response to client
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                error = "An error occurred while processing your request",
                message = exception.Message,
                // Only include stack trace in development
                stackTrace = _environment.IsDevelopment()
                    ? exception.StackTrace
                    : null
            };

            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }

        private async Task SendErrorToEndpointAsync(string endpointUrl, HttpContext context, Exception exception)
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout to avoid blocking

            // Extract board ID from request path or headers if available
            var boardId = ExtractBoardId(context);

            var errorPayload = new
            {
                boardId = boardId,
                timestamp = DateTime.UtcNow,
                file = exception.Source,
                line = GetLineNumber(exception),
                stackTrace = exception.StackTrace,
                message = exception.Message,
                exceptionType = exception.GetType().Name,
                requestPath = context.Request.Path.ToString(),
                requestMethod = context.Request.Method,
                userAgent = context.Request.Headers["User-Agent"].ToString(),
                // Include inner exception if present
                innerException = exception.InnerException != null ? new
                {
                    message = exception.InnerException.Message,
                    type = exception.InnerException.GetType().Name,
                    stackTrace = exception.InnerException.StackTrace
                } : null
            };

            var json = JsonSerializer.Serialize(errorPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Fire and forget - don't wait for response to avoid blocking
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await httpClient.PostAsync(endpointUrl, content);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully sent runtime error to endpoint: {Endpoint}", endpointUrl);
                    }
                    else
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("Error endpoint returned non-success status: {StatusCode}, Response: {Response}", 
                            response.StatusCode, responseContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while sending error to endpoint: {Endpoint}", endpointUrl);
                }
            });
        }

        private string? ExtractBoardId(HttpContext context)
        {
            // Try to extract board ID from various sources:
            // 1. Route data (e.g., /api/test/{boardId})
            if (context.Request.RouteValues.TryGetValue("boardId", out var boardIdObj))
            {
                return boardIdObj?.ToString();
            }

            // 2. Query string
            if (context.Request.Query.TryGetValue("boardId", out var boardIdQuery))
            {
                return boardIdQuery.ToString();
            }

            // 3. Custom header
            if (context.Request.Headers.TryGetValue("X-Board-Id", out var boardIdHeader))
            {
                return boardIdHeader.ToString();
            }

            // 4. Try to extract from path pattern (e.g., /api/boards/{boardId}/...)
            var path = context.Request.Path.ToString();
            var pathMatch = System.Text.RegularExpressions.Regex.Match(
                path,
                @"/boards/([^/]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (pathMatch.Success && pathMatch.Groups.Count > 1)
            {
                return pathMatch.Groups[1].Value;
            }

            return null;
        }

        private int? GetLineNumber(Exception exception)
        {
            // Try to extract line number from stack trace
            var stackTrace = exception.StackTrace;
            if (string.IsNullOrEmpty(stackTrace))
                return null;

            // Look for patterns like "in file.cs:line 123" or "at file.cs:123"
            var match = System.Text.RegularExpressions.Regex.Match(
                stackTrace,
                @":line\s+(\d+)|:(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                // Try group 1 first (":line 123"), then group 2 (":123")
                var lineStr = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (int.TryParse(lineStr, out var line))
                {
                    return line;
                }
            }

            return null;
        }
    }
}
