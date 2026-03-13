using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Timeular.Log.Models;

namespace Timeular.Desktop.Services
{
    public class LogService
    {
        private readonly HttpClient _client;

        public LogService(string baseUrl)
        {
            _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        public async Task<EventLogEntry?> PostEntryAsync(EventLogEntry entry)
        {
            var response = await _client.PostAsJsonAsync("/logs", entry);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<EventLogEntry>();
            }

            return null;
        }
    }
}