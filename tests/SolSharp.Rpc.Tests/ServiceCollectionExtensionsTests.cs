using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Polly;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests;

public static class ServiceCollectionExtensionsTests
{
    [TestFixture]
    public sealed class AddSolanaRpc
    {
        [Test]
        public void EndpointOverload_ResolvesClientWithBaseAddress()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSolanaRpc("https://example.com/rpc");
            var provider = services.BuildServiceProvider();

            // Act & Assert
            provider.GetRequiredService<SolanaRpcClient>().Should().NotBeNull();

            var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SolanaRpcClient));
            http.BaseAddress.Should().Be(new Uri("https://example.com/rpc"));
        }

        [Test]
        public void ConfigureOverload_AppliesEndpointToOptions()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSolanaRpc(options => options.Endpoint = "https://node.example/rpc");
            var provider = services.BuildServiceProvider();

            // Act & Assert
            provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value.Endpoint
                .Should().Be("https://node.example/rpc");
        }
    }

    [TestFixture]
    public sealed class AddSolanaWs
    {
        [Test]
        public void ResolvesASingletonClient()
        {
            // Arrange: no logging registered on purpose - the client must tolerate its absence.
            var services = new ServiceCollection();
            services.AddSolanaWs(new SolanaWsClientOptions { AutoReconnect = false });
            var provider = services.BuildServiceProvider();

            // Act
            var first = provider.GetRequiredService<SolanaWsClient>();
            var second = provider.GetRequiredService<SolanaWsClient>();

            // Assert
            first.Should().NotBeNull();
            second.Should().BeSameAs(first);
        }

        [Test]
        public void DefaultOptions_AlsoResolve()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddSolanaWs();

            // Act & Assert
            services.BuildServiceProvider().GetRequiredService<SolanaWsClient>().Should().NotBeNull();
        }
    }

    [TestFixture]
    public sealed class Validation
    {
        [Test]
        public void RejectsNonHttpEndpoint()
        {
            // Arrange
            var provider = ProviderFor("ftp://example.com");

            // Act
            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            // Assert
            act.Should().Throw<OptionsValidationException>();
        }

        [Test]
        public void RejectsEmptyEndpoint()
        {
            // Arrange
            var provider = ProviderFor("");

            // Act
            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            // Assert
            act.Should().Throw<OptionsValidationException>();
        }

        [Test]
        public void AcceptsValidHttpsEndpoint()
        {
            // Arrange
            var provider = ProviderFor("https://api.devnet.solana.com");

            // Act
            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void RejectsNonPositiveResponseLimit()
        {
            var services = new ServiceCollection();
            services.AddSolanaRpc(options =>
            {
                options.Endpoint = "https://api.devnet.solana.com";
                options.MaximumResponseContentLength = 0;
            });
            var provider = services.BuildServiceProvider();

            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            act.Should().Throw<OptionsValidationException>();
        }

        private static ServiceProvider ProviderFor(string endpoint)
        {
            var services = new ServiceCollection();
            services.AddSolanaRpc(options => options.Endpoint = endpoint);
            return services.BuildServiceProvider();
        }
    }

    [TestFixture]
    public sealed class Resilience
    {
        [Test]
        public async Task RetriesTransientFailure()
        {
            // Arrange
            var handler = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                Json("""{"jsonrpc":"2.0","result":123,"id":1}"""));

            var services = new ServiceCollection();
            services
                .AddSolanaRpc(
                    options => options.Endpoint = "https://node.example",
                    resilience =>
                    {
                        resilience.Retry.MaxRetryAttempts = 1;
                        resilience.Retry.Delay = TimeSpan.Zero;
                        resilience.Retry.BackoffType = DelayBackoffType.Constant;
                        resilience.Retry.UseJitter = false;
                    })
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            var client = services.BuildServiceProvider().GetRequiredService<SolanaRpcClient>();

            // Act & Assert
            (await client.GetSlotAsync()).Should().Be(123);
            handler.CallCount.Should().Be(2);
        }

        [Test]
        public async Task DoesNotRetryNonIdempotentAirdrop()
        {
            // Arrange
            var handler = new SequenceHandler(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
                Json("""{"jsonrpc":"2.0","result":"duplicate","id":1}"""));

            var services = new ServiceCollection();
            services
                .AddSolanaRpc(
                    options => options.Endpoint = "https://node.example",
                    resilience =>
                    {
                        resilience.Retry.MaxRetryAttempts = 1;
                        resilience.Retry.Delay = TimeSpan.Zero;
                        resilience.Retry.BackoffType = DelayBackoffType.Constant;
                        resilience.Retry.UseJitter = false;
                    })
                .ConfigurePrimaryHttpMessageHandler(() => handler);

            var client = services.BuildServiceProvider().GetRequiredService<SolanaRpcClient>();
            var account = new PublicKey(new byte[PublicKey.Length]);

            // Act
            var act = async () => await client.RequestAirdropAsync(account, 1);

            // Assert
            await act.Should().ThrowAsync<HttpRequestException>();
            handler.CallCount.Should().Be(1);
        }

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
