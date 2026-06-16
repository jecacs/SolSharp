using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace SolSharp.Rpc.Tests;

public static class ServiceCollectionExtensionsTests
{
    [TestFixture]
    public sealed class AddSolanaRpc
    {
        [Test]
        public void EndpointOverload_ResolvesClientWithBaseAddress()
        {
            var services = new ServiceCollection();
            services.AddSolanaRpc("https://example.com/rpc");
            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<SolanaRpcClient>().Should().NotBeNull();

            var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SolanaRpcClient));
            http.BaseAddress.Should().Be(new Uri("https://example.com/rpc"));
        }

        [Test]
        public void ConfigureOverload_AppliesEndpointToOptions()
        {
            var services = new ServiceCollection();
            services.AddSolanaRpc(options => options.Endpoint = "https://node.example/rpc");
            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value.Endpoint
                .Should().Be("https://node.example/rpc");
        }
    }

    [TestFixture]
    public sealed class Validation
    {
        [Test]
        public void RejectsNonHttpEndpoint()
        {
            var provider = ProviderFor("ftp://example.com");

            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            act.Should().Throw<OptionsValidationException>();
        }

        [Test]
        public void RejectsEmptyEndpoint()
        {
            var provider = ProviderFor("");

            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            act.Should().Throw<OptionsValidationException>();
        }

        [Test]
        public void AcceptsValidHttpsEndpoint()
        {
            var provider = ProviderFor("https://api.devnet.solana.com");

            Action act = () => _ = provider.GetRequiredService<IOptions<SolanaRpcOptions>>().Value;

            act.Should().NotThrow();
        }

        private static ServiceProvider ProviderFor(string endpoint)
        {
            var services = new ServiceCollection();
            services.AddSolanaRpc(options => options.Endpoint = endpoint);
            return services.BuildServiceProvider();
        }
    }
}
