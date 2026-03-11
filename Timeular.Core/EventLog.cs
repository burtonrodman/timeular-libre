namespace Timeular.Core;

public class EventLog
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public int? Orientation { get; set; }
}
