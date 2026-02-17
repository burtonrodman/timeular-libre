using System.Text.Json;

namespace TimeularLibre;

public class EventLog
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int? Orientation { get; set; }
}

public class EventLogger
{
    private readonly string _logFilePath;
    private readonly object _lockObject = new();

    public EventLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
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
                WriteIndented = false  // JSON Lines format - one compact JSON per line
            });

            lock (_lockObject)
            {
                File.AppendAllText(_logFilePath, json + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to log event: {ex.Message}");
        }
    }

    public List<EventLog> GetRecentEvents(int count = 100)
    {
        var events = new List<EventLog>();
        
        try
        {
            if (!File.Exists(_logFilePath))
                return events;

            // Use a queue to keep only the most recent entries while streaming
            var recentJsonObjects = new Queue<string>(count);

            // Parse JSON Lines format - one JSON object per line
            foreach (var line in File.ReadLines(_logFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Maintain a sliding window of recent entries
                if (recentJsonObjects.Count >= count)
                    recentJsonObjects.Dequeue();
                
                recentJsonObjects.Enqueue(line);
            }

            // Deserialize the most recent entries
            foreach (var json in recentJsonObjects)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<EventLog>(json);
                    if (entry != null)
                        events.Add(entry);
                }
                catch
                {
                    // Skip malformed entries
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to read events: {ex.Message}");
        }

        return events;
    }
}
