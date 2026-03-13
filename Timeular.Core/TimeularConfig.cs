namespace Timeular.Core;

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

    // Sides the user has explicitly named (others will prompt on first flip)
    public HashSet<int> ConfiguredSides { get; set; } = new();

    // new members for action and web interface
    public Dictionary<int, string> SideActions { get; set; } = new();
    public string WebInterfaceUrl { get; set; } = string.Empty;
    public bool AutoCloseAfterLog { get; set; } = true;
}
