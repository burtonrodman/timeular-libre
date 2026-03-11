using Timeular.Core;

namespace TimeularLibre;

class Program
{
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimeularLibre",
        "events.json"
    );

    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TimeularLibre",
        "config.json"
    );

    static async Task Main(string[] args)
    {
        Console.WriteLine("Timeular Libre - CLI mode (legacy)");

        var logger = new EventLogger(LogFilePath);
        var provider = new FileConfigProvider(ConfigFilePath);
        var config = await provider.GetConfigAsync();

        var listener = new BluetoothCubeListener(config, logger);
        listener.FlipOccurred += (s, e) =>
        {
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Flip detected: {e.Label}");
        };

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await listener.StartAsync(cts.Token);

        Console.WriteLine("Listener stopped. Goodbye.");
    }
}
