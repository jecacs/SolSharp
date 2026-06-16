using System.Net;
using System.Text;

namespace SolSharp.Rpc.Tests;

/// <summary>Captures the outgoing request body and returns a canned response.</summary>
internal sealed class FakeHttpMessageHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    : HttpMessageHandler
{
    public string? CapturedRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
            CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }
}
