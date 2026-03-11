using System.Net.Http.Json;

namespace Timeular.Core;

public class HttpConfigProvider : IConfigProvider
{
    private readonly HttpClient _client;
    private readonly string _endpointUrl;

    public HttpConfigProvider(HttpClient client, string endpointUrl)
    {
        _client = client;
        _endpointUrl = endpointUrl;
    }

    public async Task<TimeularConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await _client.GetFromJsonAsync<TimeularConfig>(_endpointUrl, cancellationToken);
            if (config != null)
                return config;
        }
        catch
        {
            // ignore and return default
        }
        return new TimeularConfig();
    }
}
