using System.Windows;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application; // disambiguate from WinForms
using Timeular.Desktop.Services;
using Timeular.Log.Models;

namespace Timeular.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly LogService _logService;

    public MainWindow(string logApiUrl)
    {
        InitializeComponent();
        _logService = new LogService(logApiUrl);
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var entry = new EventLogEntry
        {
            EventType = EventTypeText.Text,
            Description = DescriptionText.Text,
            AdditionalData = AdditionalDataText.Text
        };

        var result = await _logService.PostEntryAsync(entry);
        this.Hide();
        if (result != null)
            ((App)Application.Current).Logger?.LogInformation("[Desktop] entry logged successfully");
        else
            ((App)Application.Current).Logger?.LogWarning("[Desktop] entry logging failed");
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }}