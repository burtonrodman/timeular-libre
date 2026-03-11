using Timeular.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Timeular.Service;

Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices((ctx, services) =>
    {
        // event logger
        services.AddSingleton<EventLogger>(sp =>
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TimeularLibre",
                "events.json");
            return new EventLogger(path);
        });

        // config provider
        services.AddSingleton<IConfigProvider>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfiguration>();
            var url = cfg["Config:ConfigUrl"];
            if (!string.IsNullOrWhiteSpace(url))
            {
                return new HttpConfigProvider(new HttpClient(), url);
            }
            else
            {
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TimeularLibre",
                    "config.json");
                return new FileConfigProvider(configPath);
            }
        });

        services.AddSingleton<IActionLauncher, DefaultActionLauncher>();
        services.AddSingleton<ICubeListener>(sp =>
        {
            var cfgProv = sp.GetRequiredService<IConfigProvider>();
            var cfg = cfgProv.GetConfigAsync().GetAwaiter().GetResult();
            var logger = sp.GetRequiredService<EventLogger>();
            return new BluetoothCubeListener(cfg, logger);
        });

        services.AddHostedService<Worker>();
    })
    .Build()
    .Run();
