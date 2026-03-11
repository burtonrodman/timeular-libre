using System.Text.Json;

namespace Timeular.Core;

public class FileConfigProvider : IConfigProvider
{
    private readonly string _configPath;

    public FileConfigProvider(string configPath)
    {
        _configPath = configPath;
    }

    public async Task<TimeularConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
                var config = JsonSerializer.Deserialize<TimeularConfig>(json);
                if (config != null)
                    return config;
            }
        }
        catch { }

        return new TimeularConfig();
    }
}
