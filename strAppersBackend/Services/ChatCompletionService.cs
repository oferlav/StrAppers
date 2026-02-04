using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using strAppersBackend.Models;

namespace strAppersBackend.Services
{
    /// <summary>
    /// Shared chat completion (OpenAI/Anthropic) for Mentor and Customer respond flows.
    /// </summary>
    public class ChatCompletionService : IChatCompletionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatCompletionService> _logger;

        public ChatCompletionService(IConfiguration configuration, ILogger<ChatCompletionService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<(string Response, int InputTokens, int OutputTokens)> GetChatCompletionAsync(
            AIModel aiModel,
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<ChatMessageEntry>? chatHistory = null)
        {
            if (aiModel.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                return await CallOpenAIAsync(aiModel, systemPrompt, userPrompt, chatHistory);
            }
            if (aiModel.Provider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
            {
                return await CallAnthropicAsync(aiModel, systemPrompt, userPrompt, chatHistory);
            }
            throw new NotSupportedException($"Unsupported AI provider: {aiModel.Provider}");
        }

        private async Task<(string Response, int InputTokens, int OutputTokens)> CallOpenAIAsync(
            AIModel aiModel,
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<ChatMessageEntry>? chatHistory)
        {
            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("OpenAI API key not configured");

            var baseUrl = aiModel.BaseUrl ?? "https://api.openai.com/v1";
            var maxTokens = aiModel.MaxTokens ?? 16384;
            var temperature = aiModel.DefaultTemperature ?? 0.2;

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            var messages = new List<object> { new { role = "system", content = systemPrompt } };
            if (chatHistory != null && chatHistory.Count > 0)
            {
                foreach (var h in chatHistory)
                {
                    var role = h.Role == "assistant" ? "assistant" : "user";
                    messages.Add(new { role = role, content = h.Message });
                }
            }
            messages.Add(new { role = "user", content = userPrompt });

            var requestBody = new
            {
                model = aiModel.Name,
                messages = messages.ToArray(),
                max_tokens = maxTokens,
                temperature = temperature
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling OpenAI API with model {Model}", aiModel.Name);
            var response = await httpClient.PostAsync($"{baseUrl}/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"OpenAI API error: {response.StatusCode}. {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var openAIResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            int inputTokens = 0, outputTokens = 0;
            if (openAIResponse.TryGetProperty("usage", out var usageProp))
            {
                if (usageProp.TryGetProperty("prompt_tokens", out var promptTokensProp))
                    inputTokens = promptTokensProp.GetInt32();
                if (usageProp.TryGetProperty("completion_tokens", out var completionTokensProp))
                    outputTokens = completionTokensProp.GetInt32();
            }

            if (openAIResponse.TryGetProperty("choices", out var choicesProp) && choicesProp.ValueKind == JsonValueKind.Array && choicesProp.GetArrayLength() > 0)
            {
                var firstChoice = choicesProp[0];
                if (firstChoice.TryGetProperty("message", out var messageProp) && messageProp.TryGetProperty("content", out var contentProp))
                {
                    var responseText = contentProp.GetString() ?? "";
                    return (responseText, inputTokens, outputTokens);
                }
            }

            throw new Exception("Failed to parse OpenAI response");
        }

        private async Task<(string Response, int InputTokens, int OutputTokens)> CallAnthropicAsync(
            AIModel aiModel,
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<ChatMessageEntry>? chatHistory)
        {
            var apiKey = _configuration["Anthropic:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("Anthropic API key not configured");

            var baseUrl = aiModel.BaseUrl ?? "https://api.anthropic.com/v1";
            var apiVersion = aiModel.ApiVersion ?? "2023-06-01";
            var maxTokens = aiModel.MaxTokens ?? 200000;
            var temperature = aiModel.DefaultTemperature ?? 0.3;

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", apiVersion);
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            var messages = new List<object>();
            if (chatHistory != null && chatHistory.Count > 0)
            {
                foreach (var h in chatHistory)
                {
                    var role = h.Role == "assistant" ? "assistant" : "user";
                    messages.Add(new { role = role, content = h.Message });
                }
            }
            messages.Add(new { role = "user", content = userPrompt });

            var requestBody = new
            {
                model = aiModel.Name,
                max_tokens = maxTokens,
                system = systemPrompt,
                messages = messages.ToArray(),
                temperature = temperature
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Anthropic API with model {Model}", aiModel.Name);
            var response = await httpClient.PostAsync($"{baseUrl}/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Anthropic API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new Exception($"Anthropic API error: {response.StatusCode}. {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var anthropicResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            int inputTokens = 0, outputTokens = 0;
            if (anthropicResponse.TryGetProperty("usage", out var usageProp))
            {
                if (usageProp.TryGetProperty("input_tokens", out var inputTokensProp))
                    inputTokens = inputTokensProp.GetInt32();
                if (usageProp.TryGetProperty("output_tokens", out var outputTokensProp))
                    outputTokens = outputTokensProp.GetInt32();
            }

            if (anthropicResponse.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.Array && contentProp.GetArrayLength() > 0)
            {
                var firstContent = contentProp[0];
                if (firstContent.TryGetProperty("text", out var textProp))
                {
                    var responseText = textProp.GetString() ?? "";
                    return (responseText, inputTokens, outputTokens);
                }
            }

            throw new Exception("Failed to parse Anthropic response");
        }
    }
}
