using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Tests;

/// <summary>
/// Tests the provider routing in AIService.GenerateTextResponseAsync (~line 2572).
/// Logic: model starts with "claude-" → Anthropic path; otherwise → OpenAI path.
///
/// NOTE: CallAnthropicTextAsync creates its own new HttpClient() rather than using
/// the injected one, so Anthropic HTTP calls cannot be intercepted here.
/// We verify routing by asserting whether the injected OpenAI handler is called.
/// </summary>
public class AIServiceRoutingTests
{
    private const string FakeOpenAIKey = "sk-fake-openai-key";
    private const string FakeAnthropicKey = "sk-ant-fake-key";

    // Minimal valid OpenAI chat completion JSON
    private const string OpenAIJson =
        """{"choices":[{"message":{"content":"hello from openai"}}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""";

    private static AIService BuildService(MockHttpMessageHandler handler, string? fallbackModel = "gpt-4o-mini")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAI:ApiKey"] = FakeOpenAIKey,
                ["Anthropic:ApiKey"] = FakeAnthropicKey,
                // Point Anthropic base URL at a port that is not listening so it fails fast
                ["Anthropic:BaseUrl"] = "http://127.0.0.1:1"
            })
            .Build();

        var aiConfig = Options.Create(new AIConfig
        {
            Model = fallbackModel ?? "gpt-4o-mini",
            MaxTokens = 100,
            Temperature = 0.5
        });

        var sdConfig = Options.Create(new SystemDesignAIAgentConfig());

        var httpClient = new HttpClient(handler);
        return new AIService(
            httpClient,
            config,
            NullLogger<AIService>.Instance,
            aiConfig,
            sdConfig);
    }

    [Fact]
    public async Task GptModel_RoutesToOpenAI_HandlerIsCalled()
    {
        var handler = MockHttpMessageHandler.ReturnOk(OpenAIJson);
        var service = BuildService(handler);

        var result = await service.GenerateTextResponseAsync("test prompt", "gpt-4o-mini");

        Assert.NotNull(result);
        Assert.Equal("hello from openai", result);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("openai.com", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ClaudeModel_RoutesToAnthropicPath_OpenAIHandlerNotCalled()
    {
        // The Anthropic path creates its own HttpClient pointing at http://127.0.0.1:1,
        // which will fail quickly. We verify the injected OpenAI handler was never touched.
        var handler = MockHttpMessageHandler.ReturnOk(OpenAIJson);
        var service = BuildService(handler);

        var result = await service.GenerateTextResponseAsync("test prompt", "claude-sonnet-4-5-20250929");

        Assert.Null(result);              // Anthropic call failed (unreachable URL)
        Assert.Equal(0, handler.CallCount); // OpenAI handler was NOT called
    }

    [Fact]
    public async Task NullModel_FallsBackToAIConfigModel_UsesOpenAI()
    {
        var handler = MockHttpMessageHandler.ReturnOk(OpenAIJson);
        var service = BuildService(handler, fallbackModel: "gpt-4o-mini");

        var result = await service.GenerateTextResponseAsync("test prompt", null);

        // Fallback model is gpt-4o-mini → routes to OpenAI → succeeds
        Assert.NotNull(result);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("openai.com", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task NullModel_FallsBackToClaudeConfig_UsesAnthropicPath()
    {
        var handler = MockHttpMessageHandler.ReturnOk(OpenAIJson);
        var service = BuildService(handler, fallbackModel: "claude-sonnet-4-5-20250929");

        var result = await service.GenerateTextResponseAsync("test prompt", null);

        // Fallback model is claude-* → routes to Anthropic → fails (unreachable URL in tests)
        Assert.Null(result);
        Assert.Equal(0, handler.CallCount); // OpenAI handler was NOT called
    }
}
