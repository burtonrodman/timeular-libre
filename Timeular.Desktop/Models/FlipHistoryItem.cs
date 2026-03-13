using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace Timeular.Desktop.Models;

public enum FlipStatus { Pending, Logged, Failed }

public class FlipHistoryItem : INotifyPropertyChanged
{
    private static readonly SolidColorBrush PendingBrush  = new(Color.FromRgb(0xDC, 0xDC, 0xAA));
    private static readonly SolidColorBrush LoggedBrush   = new(Color.FromRgb(0x4E, 0xC9, 0xB0));
    private static readonly SolidColorBrush FailedBrush   = new(Color.FromRgb(0xF4, 0x47, 0x47));

    private string? _description;
    private FlipStatus _status = FlipStatus.Pending;
    private string _label = "";

    public DateTime Time   { get; init; }
    public int      Side   { get; init; }
    public string   Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public string TimeText => Time.ToString("HH:mm:ss");

    public string? Description
    {
        get => _description;
        private set { _description = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDescription)); }
    }

    public bool HasDescription => !string.IsNullOrWhiteSpace(_description);

    public FlipStatus Status
    {
        get => _status;
        private set
        {
            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(StatusBrush));
        }
    }

    public string StatusIcon => Status switch
    {
        FlipStatus.Logged => "✓",
        FlipStatus.Failed => "✗",
        _                 => "●"
    };

    public SolidColorBrush StatusBrush => Status switch
    {
        FlipStatus.Logged => LoggedBrush,
        FlipStatus.Failed => FailedBrush,
        _                 => PendingBrush
    };

    public void MarkLogged(string description) { Description = description; Status = FlipStatus.Logged; }
    public void MarkFailed(string description) { Description = description; Status = FlipStatus.Failed; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
