namespace Timeular.Core;

public class FlipEventArgs : EventArgs
{
    public int Side { get; }
    public string Label { get; }

    public FlipEventArgs(int side, string label)
    {
        Side = side;
        Label = label;
    }
}
