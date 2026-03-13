using System.Windows;
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

    public MainWindow()
    {
        InitializeComponent();
        _logService = new LogService("https://localhost:5001"); // default base URL
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
        if (result != null)
        {
                // user feedback removed; just hide and log
                this.Hide();
                // log success via file
                ((App)Application.Current).Log("[Desktop] entry logged successfully");
            }
            else
            {
                ((App)Application.Current).Log("[Desktop] entry logging failed");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }}