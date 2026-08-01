using System.Net;
using System.Text;

namespace XBVault.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(HttpResponseMessage response)
        : this(_ => response)
    {
    }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(request =>
        {
            var resp = new HttpResponseMessage(status);
            if (!string.IsNullOrEmpty(json))
                resp.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return resp;
        });

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
