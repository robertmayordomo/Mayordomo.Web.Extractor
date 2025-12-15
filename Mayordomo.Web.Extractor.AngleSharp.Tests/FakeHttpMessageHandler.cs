using System.Net;

namespace Mayordomo.Web.Extractor.AngleSharp.Tests;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = _handler(request);
        return Task.FromResult(response);
    }

    public static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var messageHandler = new FakeHttpMessageHandler(handler);
        return new HttpClient(messageHandler);
    }

    public static HttpResponseMessage HtmlResponse(string html, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(html)
        };
    }

    public static HttpResponseMessage BinaryResponse(byte[] data, string mediaType = "image/jpeg", HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var content = new ByteArrayContent(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(statusCode)
        {
            Content = content
        };
    }
}