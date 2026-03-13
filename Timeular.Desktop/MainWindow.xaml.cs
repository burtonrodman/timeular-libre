using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Microsoft.Extensions.Logging;
using Timeular.Core;
using Timeular.Desktop.Models;
using Timeular.Desktop.Services;
using Timeular.Log.Models;
using Application = System.Windows.Application;

namespace Timeular.Desktop;

public partial class MainWindow : Window
{
    private readonly LogService _logService;
    private readonly TimeularConfig _config;
    private readonly Func<Task> _saveConfig;
    private readonly ObservableCollection<FlipHistoryItem> _history = new();
    private FlipHistoryItem? _pendingFlip;

    public MainWindow(string logApiUrl, TimeularConfig config, Func<Task> saveConfig)
    {
        InitializeComponent();
        _logService = new LogService(logApiUrl);
        _config = config;
        _saveConfig = saveConfig;

        HistoryList.ItemsSource = _history;
        AutoCloseCheckbox.IsChecked = config.AutoCloseAfterLog;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        Width  = area.Width  * 0.80;
        Height = area.Height * 0.80;
        Left   = area.Left + (area.Width  - Width)  / 2;
        Top    = area.Top  + (area.Height - Height) / 2;
    }

    public void AddFlip(int side, string label)
    {
        var item = new FlipHistoryItem { Time = DateTime.Now, Side = side, Label = label };
        _history.Add(item);
        _pendingFlip = item;
        ScrollToBottom();

        if (!_config.ConfiguredSides.Contains(side))
        {
            // First time this side has been used — ask user to name it
            SideNameInput.Text = label == $"Side {side}" ? "" : label;
            SideNameRow.Visibility = Visibility.Visible;
            DescriptionRow.IsEnabled = false;
            SideNameInput.Focus();
        }
        else
        {
            SideNameRow.Visibility = Visibility.Collapsed;
            DescriptionRow.IsEnabled = true;
            DescriptionInput.Text = "";
            DescriptionInput.Focus();
        }
    }

    private void ScrollToBottom()
    {
        HistoryScroller.UpdateLayout();
        HistoryScroller.ScrollToBottom();
    }

    private async void SideNameInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            var name = SideNameInput.Text.Trim();
            if (!string.IsNullOrWhiteSpace(name) && _pendingFlip != null)
            {
                var side = _pendingFlip.Side;
                _config.SideLabels[side] = name;
                _config.ConfiguredSides.Add(side);
                _pendingFlip.Label = name;
                await _saveConfig();
            }
            SideNameRow.Visibility = Visibility.Collapsed;
            DescriptionRow.IsEnabled = true;
            DescriptionInput.Text = "";
            DescriptionInput.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Skip naming — mark side as configured with default label
            if (_pendingFlip != null)
                _config.ConfiguredSides.Add(_pendingFlip.Side);
            SideNameRow.Visibility = Visibility.Collapsed;
            DescriptionRow.IsEnabled = true;
            DescriptionInput.Text = "";
            DescriptionInput.Focus();
            e.Handled = true;
        }
    }

    private async void DescriptionInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            if (!string.IsNullOrWhiteSpace(DescriptionInput.Text))
                await SubmitAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private async Task SubmitAsync()
    {
        var description = DescriptionInput.Text.Trim();
        var flip = _pendingFlip;
        if (flip == null) return;

        DescriptionInput.IsEnabled = false;

        var entry = new EventLogEntry
        {
            EventType = flip.Label,
            Description = description,
            Timestamp = flip.Time
        };

        var result = await _logService.PostEntryAsync(entry);

        DescriptionInput.IsEnabled = true;
        DescriptionInput.Text = "";

        if (result != null)
        {
            flip.MarkLogged(description);
            ((App)Application.Current).Logger?.LogInformation(
                "[Desktop] logged: {Label} — {Desc}", flip.Label, description);
            if (_config.AutoCloseAfterLog)
                Hide();
        }
        else
        {
            flip.MarkFailed(description);
            ((App)Application.Current).Logger?.LogWarning(
                "[Desktop] log failed: {Label}", flip.Label);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void AutoCloseCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        _config.AutoCloseAfterLog = AutoCloseCheckbox.IsChecked == true;
        _ = _saveConfig();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // prevent close; just hide so history is preserved
        e.Cancel = true;
        Hide();
    }
}
