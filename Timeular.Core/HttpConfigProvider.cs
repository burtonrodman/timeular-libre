using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Timeular.Core;

public class HttpConfigProvider : IConfigProvider
{
    private readonly HttpClient _client;
    private readonly string _endpointUrl;
    private readonly ILogger<HttpConfigProvider>? _logger;

    public HttpConfigProvider(HttpClient client, string endpointUrl, ILogger<HttpConfigProvider>? logger = null)
    {
        _client = client;
        _endpointUrl = endpointUrl;
        _logger = logger;
    }

    public async Task<TimeularConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _client.GetFromJsonAsync<TimeularConfig>(_endpointUrl, cancellationToken);
            if (config != null)
                return config;
            _logger?.LogWarning("Received null config from {url}", _endpointUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch configuration from {url}", _endpointUrl);
        }
        return new TimeularConfig();
    }
}
