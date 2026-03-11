using System;
using System.Threading;
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
            // optional: subscribe if needed
            listener.FlipOccurred += (s, e) => { /* no-op */ };

            var eventLogger = new EventLogger(Path.GetTempFileName());

            var worker = new Worker(loggerMock.Object, configProviderMock.Object, listener, launcherMock.Object, eventLogger);

            // call private OnFlip via reflection
            var method = typeof(Worker).GetMethod("OnFlip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            // ensure stub listener configured
            Assert.NotNull(listener.Config);
            Assert.Equal("https://foo", listener.Config.WebInterfaceUrl);

            // set the worker's private _config to allow OnFlip to read it
            var configField = typeof(Worker).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(configField);
            configField.SetValue(worker, cfg);

            // verify internal config
            var workerCfg = configField.GetValue(worker) as TimeularConfig;
            Assert.Equal("https://foo", workerCfg?.WebInterfaceUrl);

            // no diagnostics needed

            method.Invoke(worker, new object?[] { null, new FlipEventArgs(3, "Test") });

            launcherMock.Verify(l => l.Launch("https://foo", "Test"), Times.Once);
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