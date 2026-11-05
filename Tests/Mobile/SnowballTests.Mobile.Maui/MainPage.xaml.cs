using System.Collections.ObjectModel;
using SqliteFtsSnowball.TestKit;

namespace SnowballTests.Mobile.Maui;

public partial class MainPage : ContentPage
{
    private readonly ObservableCollection<string> _lines = new();
    private bool _running;
    private bool _autoRan;

    public MainPage()
    {
        InitializeComponent();
        LogView.ItemsSource = _lines;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_autoRan)
        {
            _autoRan = true;
            await RunAsync();
        }
    }

    private async void OnRunClicked(object? sender, EventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        RunButton.IsEnabled = false;
        Spinner.IsRunning = true;
        _lines.Clear();
        SummaryLabel.Text = "Running…";

        TestOutcome outcome;
        try
        {
            outcome = await MobileTestDriver.RunAsync(line => MainThread.BeginInvokeOnMainThread(() =>
            {
                _lines.Add(line);
                if (_lines.Count > 6000)
                {
                    _lines.RemoveAt(0);
                }
            }));
        }
        finally
        {
            Spinner.IsRunning = false;
            RunButton.IsEnabled = true;
            _running = false;
        }

        SummaryLabel.Text = $"{(outcome.Success ? "PASS" : "FAIL")} — {outcome.Passed}/{outcome.Total} passed, "
            + $"{outcome.Failed} failed in {outcome.Duration.TotalSeconds:F1}s";

        if (AppState.CiMode)
        {
            await Task.Delay(500);
            AppState.ExitProcess(outcome.Success ? 0 : 1);
        }
    }
}

