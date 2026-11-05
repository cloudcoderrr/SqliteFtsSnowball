using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SqliteFtsSnowball.TestKit;

namespace SnowballTests.Desktop.Avalonia;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _lines = new();
    private bool _running;

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _lines;

        Opened += async (_, _) =>
        {
            if (AppState.CiMode)
            {
                await RunAsync();
            }
        };
    }

    private async void OnRunClick(object? sender, RoutedEventArgs e) => await RunAsync();

    private async Task RunAsync()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        RunButton.IsEnabled = false;
        Progress.IsIndeterminate = true;
        _lines.Clear();
        SummaryText.Text = "Running…";

        string directory = Environment.GetEnvironmentVariable("SNOWBALL_LOG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "test-logs");
        Directory.CreateDirectory(directory);
        string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string logPath = Path.Combine(directory, $"desktop-avalonia-{arch}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        LogPathText.Text = logPath;

        TestOutcome outcome = await Task.Run(() =>
        {
            using var fileLog = new FileTestLog(logPath, mirrorToConsole: true);
            var uiLog = new InMemoryTestLog(line => Dispatcher.UIThread.Post(() =>
            {
                _lines.Add(line);
                if (_lines.Count > 8000)
                {
                    _lines.RemoveAt(0);
                }

                LogList.ScrollIntoView(_lines[^1]);
            }));
            return TestRunner.RunAll(new CompositeTestLog(fileLog, uiLog));
        });

        Progress.IsIndeterminate = false;
        Progress.Value = 100;
        SummaryText.Text = $"{(outcome.Success ? "PASS" : "FAIL")} — {outcome.Passed}/{outcome.Total} passed, "
            + $"{outcome.Failed} failed in {outcome.Duration.TotalSeconds:F1}s";
        RunButton.IsEnabled = true;
        _running = false;

        if (AppState.CiMode)
        {
            int exitCode = outcome.Success ? 0 : 1;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(exitCode);
            }
        }
    }
}
