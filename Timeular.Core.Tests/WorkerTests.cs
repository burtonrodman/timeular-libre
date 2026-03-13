using System;
using System.Threading;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Timeular.Core;
using Timeular.Service;
using Xunit;

namespace Timeular.Core.Tests
{
    public class WorkerTests
    {
        [Fact]
        public void OnFlip_InvokesLauncherWithCurrentUrl()
        {
            var cfg = new TimeularConfig { WebInterfaceUrl = "https://foo" };
            var launcherMock = new Mock<IActionLauncher>();
            var loggerMock = new Mock<ILogger<Worker>>();
            var configProviderMock = new Mock<IConfigProvider>();
            configProviderMock.Setup(p => p.GetConfigAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cfg);

            // cube listener: simple stub that implements interface
            var listenerStub = new StubListener { Config = cfg };
            ICubeListener listener = listenerStub;
            listener.FlipOccurred += (s, e) => { /* no-op */ };

            var eventLogger = new EventLogger(Path.GetTempFileName());
            var worker = new Worker(loggerMock.Object, configProviderMock.Object, listener, launcherMock.Object, eventLogger);

            var method = typeof(Worker).GetMethod("OnFlip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            Assert.NotNull(listener.Config);
            Assert.Equal("https://foo", listener.Config.WebInterfaceUrl);

            var configField = typeof(Worker).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(configField);
            configField.SetValue(worker, cfg);

            var workerCfg = configField.GetValue(worker) as TimeularConfig;
            Assert.Equal("https://foo", workerCfg?.WebInterfaceUrl);

            method.Invoke(worker, new object?[] { null, new FlipEventArgs(3, "Test") });

            launcherMock.Verify(l => l.Launch("https://foo", "Test"), Times.Once);

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Launching browser to")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task WarningLogged_WhenUrlMissing()
        {
            var cfg = new TimeularConfig { WebInterfaceUrl = string.Empty };
            var launcherMock = new Mock<IActionLauncher>();
            var loggerMock = new Mock<ILogger<Worker>>();
            var configProviderMock = new Mock<IConfigProvider>();
            configProviderMock.Setup(p => p.GetConfigAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cfg);

            var listenerStub = new StubListener { Config = cfg };
            ICubeListener listener = listenerStub;
            var eventLogger = new EventLogger(Path.GetTempFileName());
            var worker = new Worker(loggerMock.Object, configProviderMock.Object, listener, launcherMock.Object, eventLogger);

            // start the worker once to trigger config load
            var stopping = new CancellationTokenSource(TimeSpan.Zero).Token;
            await worker.StartAsync(stopping);

            loggerMock.Verify(l => l.Log(LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v,t) => v.ToString().Contains("WebInterfaceUrl is empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [Fact]
        public void DevResolver_SelectsFirstOK()
        {
            var handler = new MockHttpMessageHandler(req =>
            {
                var uri = req.RequestUri!.ToString();
                if (uri.StartsWith("http://localhost:5036"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });
            var client = new HttpClient(handler);
            var url = DevConfigUrlResolver.TryResolve(client);
            Assert.Equal("http://localhost:5036/config", url);
        }

        [Fact]
        public void DevResolver_NoneAvailable_ReturnsNull()
        {
            var handler = new MockHttpMessageHandler(req =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
            var client = new HttpClient(handler);
            var url = DevConfigUrlResolver.TryResolve(client);
            Assert.Null(url);
        }

        private EventHandler<FlipEventArgs>? _flipHandler;

        private class StubListener : ICubeListener
        {
            public TimeularConfig Config { get; set; } = new();
            public event EventHandler<FlipEventArgs>? FlipOccurred;
            public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}