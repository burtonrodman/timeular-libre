namespace Timeular.Core;

public interface ICubeListener
{
    TimeularConfig Config { get; set; }
    event EventHandler<FlipEventArgs>? FlipOccurred;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
