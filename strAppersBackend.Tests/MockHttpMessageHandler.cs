using System.Net;

namespace strAppersBackend.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public HttpRequestMessage? LastRequest { get; private set; }
    public int CallCount { get; private set; }

    public MockHttpMessageHandler(HttpResponseMessage response)
    {
        _responder = _ => response;
    }

    /// <summary>
    /// Responds per request. Needed when a single call under test issues several HTTP requests: one
    /// shared HttpResponseMessage cannot be read more than once.
    /// </summary>
    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static MockHttpMessageHandler ReturnOk(string json) =>
        new(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        CallCount++;
        return Task.FromResult(_responder(request));
    }
}
