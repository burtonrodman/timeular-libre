using System.Net.Http;

namespace Timeular.Service
{
    // helper for development: probes a small list of well-known localhost
    // endpoints and returns the first one that responds successfully.
    internal static class DevConfigUrlResolver
    {
        private static readonly string[] Candidates = new[]
        {
            "http://localhost:5036/config",
            "https://localhost:7031/config"
        };

        public static string? TryResolve(HttpClient client)
        {
            foreach (var candidate in Candidates)
            {
                try
                {
                    var resp = client.GetAsync(candidate).GetAwaiter().GetResult();
                    if (resp.IsSuccessStatusCode)
                        return candidate;
                }
                catch
                {
                    // ignore and try next
                }
            }

            return null;
        }
    }
}