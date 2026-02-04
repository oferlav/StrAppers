using strAppersBackend.Models;

namespace strAppersBackend.Services
{
    /// <summary>
    /// Shared chat completion (OpenAI/Anthropic) for Mentor and Customer respond flows.
    /// </summary>
    public interface IChatCompletionService
    {
        /// <summary>
        /// Calls the appropriate AI API (OpenAI or Anthropic) with system prompt, user prompt, and optional chat history.
        /// </summary>
        Task<(string Response, int InputTokens, int OutputTokens)> GetChatCompletionAsync(
            AIModel aiModel,
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<ChatMessageEntry>? chatHistory = null);
    }

    /// <summary>
    /// Single chat message for completion context (role + content).
    /// </summary>
    public class ChatMessageEntry
    {
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        public string Message { get; set; } = string.Empty;
    }
}
