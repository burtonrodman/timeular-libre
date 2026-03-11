using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Timeular.Core;
using Xunit;

namespace Timeular.Core.Tests
{
    public class ConfigProviderTests
    {
        [Fact]
        public async Task FileConfigProvider_LoadsDefaultsWhenMissing()
        {
            var path = Path.GetTempFileName();
            File.Delete(path); // ensure missing
            var provider = new FileConfigProvider(path);
            var cfg = await provider.GetConfigAsync();
            Assert.NotNull(cfg);
            // default configuration initializes side labels for eight sides
            Assert.Equal(8, cfg.SideLabels.Count);
        }

        [Fact]
        public async Task HttpConfigProvider_GetsConfigFromHttp()
        {
            var sample = new TimeularConfig { WebInterfaceUrl = "x" };
            var handler = new MockHttpMessageHandler(async request =>
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(sample)
                };
            });
            var client = new HttpClient(handler);
            var provider = new HttpConfigProvider(client, "http://test/config");
            var cfg = await provider.GetConfigAsync();
            Assert.Equal("x", cfg.WebInterfaceUrl);
        }

        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

            public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
            {
                _responder = responder;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _responder(request);
            }
        }
    }
}