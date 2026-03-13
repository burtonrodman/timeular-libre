using Timeular.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Timeular.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfigProvider _configProvider;
    private readonly ICubeListener _listener;
    private readonly IActionLauncher _launcher;
    private readonly EventLogger _eventLogger;
    private TimeularConfig _config = new();

    public Worker(
        ILogger<Worker> logger,
        IConfigProvider configProvider,
        ICubeListener listener,
        IActionLauncher launcher,
        EventLogger eventLogger)
    {
        _logger = logger;
        _configProvider = configProvider;
        _listener = listener;
        _launcher = launcher;
        _eventLogger = eventLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker starting");

        // initial load
        try
        {
            _config = await _configProvider.GetConfigAsync(stoppingToken);
            _listener.Config = _config;
            _logger.LogInformation("Configuration loaded; WebInterfaceUrl={url}", _config?.WebInterfaceUrl);
            if (string.IsNullOrWhiteSpace(_config?.WebInterfaceUrl))
            {
                _logger.LogWarning("WebInterfaceUrl is empty; make sure Config:ConfigUrl is set or a config file contains a valid URL");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
        }

        _listener.FlipOccurred += OnFlip;

        // start refresh loop (fire-and-forget)
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    var newConfig = await _configProvider.GetConfigAsync(stoppingToken);
                    if (newConfig != null && !ReferenceEquals(newConfig, _config))
                    {
                        _config = newConfig;
                        _listener.Config = _config;
                        _logger.LogInformation("Configuration refreshed");
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error refreshing configuration");
                }
            }
        }, stoppingToken);

        await _listener.StartAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Worker stopping");
        await _listener.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private void OnFlip(object? sender, FlipEventArgs e)
    {
        _logger.LogInformation("Flip detected: {side}", e.Label);
        _eventLogger.LogEvent("Flip", e.Label, e.Side);
        var url = _config?.WebInterfaceUrl;
        if (!string.IsNullOrWhiteSpace(url))
        {
            _logger.LogInformation("Launching browser to {url} for action {action}", url, e.Label);
            _launcher.Launch(url, e.Label);
        }
    }
}

