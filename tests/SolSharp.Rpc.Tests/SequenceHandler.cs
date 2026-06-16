using System.Net;

namespace SolSharp.Rpc.Tests;

/// <summary>Returns canned responses in order and counts how many times it was invoked.</summary>
internal sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new(responses);

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(_responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.OK));
    }
}
