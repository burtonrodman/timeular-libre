namespace TimeularLibre;

public class TimeularConfig
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public Dictionary<int, string> SideLabels { get; set; } = new()
    {
        { 1, "Side 1" },
        { 2, "Side 2" },
        { 3, "Side 3" },
        { 4, "Side 4" },
        { 5, "Side 5" },
        { 6, "Side 6" },
        { 7, "Side 7" },
        { 8, "Side 8" }
    };
}
