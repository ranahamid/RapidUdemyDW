using System.Net;
using RapidUdemyDW.Services;

namespace RapidUdemyDW.Tests;

public class HttpRetryHandlerTests
{
    /// <summary>
    /// A test handler that records how many times SendAsync was called
    /// and returns configurable responses.
    /// </summary>
    private class MockInnerHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public int CallCount { get; private set; }

        public MockInnerHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_responses.Count > 0)
                return Task.FromResult(_responses.Dequeue());

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task SuccessfulRequest_NoRetry()
    {
        var inner = new MockInnerHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new HttpRetryHandler { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ServerError_RetriesAndSucceeds()
    {
        var inner = new MockInnerHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new HttpRetryHandler { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task PersistentServerError_ReturnsAfterMaxRetries()
    {
        var inner = new MockInnerHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); // 4 = 1 initial + 3 retries
        var handler = new HttpRetryHandler { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(4, inner.CallCount); // 1 initial + 3 retries
    }

    [Fact]
    public async Task NonRetryableError_NoRetry()
    {
        var inner = new MockInnerHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var handler = new HttpRetryHandler { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task CancellationRespected()
    {
        var inner = new MockInnerHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var handler = new HttpRetryHandler { InnerHandler = inner };
        var client = new HttpClient(handler);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => client.GetAsync("https://example.com", cts.Token));
    }

    [Fact]
    public async Task TooManyRequests_Retries()
    {
        var inner = new MockInnerHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new HttpRetryHandler { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public void AuthenticationExpiredException_HasMessage()
    {
        var ex = new AuthenticationExpiredException();
        Assert.Contains("expired", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
