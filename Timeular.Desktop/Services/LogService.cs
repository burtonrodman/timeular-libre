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
            try
            {
                var response = await _client.PostAsJsonAsync("/logs", entry);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadFromJsonAsync<EventLogEntry>();
                return null;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<List<EventLogEntry>> GetRecentEntriesAsync(int limit = 20)
        {
            try
            {
                var entries = await _client.GetFromJsonAsync<List<EventLogEntry>>($"/logs?limit={limit}");
                return entries ?? [];
            }
            catch (HttpRequestException)
            {
                return [];
            }
        }
    }
}