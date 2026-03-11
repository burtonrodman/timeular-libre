using System.Text.Json;

namespace Timeular.Core;

public class EventLogger
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new();

    public EventLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public void LogEvent(string eventType, string details, int? orientation = null)
    {
        try
        {
            var logEntry = new EventLog
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                Details = details,
                Orientation = orientation
            };

            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            lock (_lockObject)
            {
                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }
        }
        catch
        {
            // swallow; service should not crash for logging errors
        }
    }

    public List<EventLog> GetRecentEvents(int count = 100)
    {
        var events = new List<EventLog>();
        try
        {
            if (!File.Exists(_logFilePath))
                return events;

            var recentJsonObjects = new Queue<string>(count);
            foreach (var line in File.ReadLines(_logFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (recentJsonObjects.Count >= count)
                    recentJsonObjects.Dequeue();
                recentJsonObjects.Enqueue(line);
            }

            foreach (var json in recentJsonObjects)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<EventLog>(json);
                    if (entry != null)
                        events.Add(entry);
                }
                catch { }
            }
        }
        catch { }
        return events;
    }
}
