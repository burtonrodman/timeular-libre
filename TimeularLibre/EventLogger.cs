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
                Timestamp = DateTime.Now,
                EventType = eventType,
                Details = details,
                Orientation = orientation
            };

            var json = JsonSerializer.Serialize(logEntry, new JsonSerializerOptions 
            { 
                WriteIndented = true 
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

            var lines = File.ReadAllLines(_logFilePath);
            var jsonObjects = new List<string>();
            var currentJson = "";

            // Parse multi-line JSON objects
            foreach (var line in lines)
            {
                currentJson += line;
                if (line.TrimEnd().EndsWith("}"))
                {
                    jsonObjects.Add(currentJson);
                    currentJson = "";
                }
            }

            // Take the most recent entries
            foreach (var json in jsonObjects.TakeLast(count))
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
