using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace dịchMH;

public partial class OverlayWindow : Window
{
    private const uint ExcludeFromCaptureAffinity = 0x00000011;
    private readonly DispatcherTimer _refreshTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private bool _hasTranslation;
    private bool _captureFallback;

    public Func<Task>? RefreshRequested { get; set; }
    public bool IsExcludedFromCapture { get; private set; }

    public OverlayWindow()
    {
        InitializeComponent();
        _refreshTimer.Tick += RefreshTimer_Tick;
        SourceInitialized += (_, _) =>
        {
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            IsExcludedFromCapture = SetWindowDisplayAffinity(handle, ExcludeFromCaptureAffinity);
        };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    public void StartAutoRefresh(bool enabled)
    {
        AutoRefreshCheckBox.IsChecked = enabled;
    }

    public void SetRefreshInterval(TimeSpan interval)
    {
        int seconds = Math.Clamp((int)Math.Round(interval.TotalSeconds), 1, 5);
        RefreshIntervalComboBox.SelectedValue = seconds.ToString();
        _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
    }

    public void SetBusy()
    {
        if (!_hasTranslation)
        {
            StatusText.Text = "Đang nhận diện và dịch...";
        }
    }

    public void SetTranslation(int lineCount)
    {
        _hasTranslation = true;
        string status = _captureFallback
            ? $"Đã dịch {lineCount} dòng · chế độ tương thích"
            : $"Đã đặt bản dịch cho {lineCount} dòng";
        if (!string.Equals(StatusText.Text, status, StringComparison.Ordinal))
        {
            StatusText.Text = status;
        }
    }

    public void SetCaptureFallback()
    {
        _captureFallback = true;
        StatusText.Text = "Chế độ tương thích: có thể nháy nhẹ khi quét";
    }

    public void SetError(string message)
    {
        StatusText.Text = message;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested is not null)
        {
            await RefreshRequested();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void AutoRefreshCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshCheckBox.IsChecked == true)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    private void RefreshIntervalComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RefreshIntervalComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selected
            && int.TryParse(selected.Tag?.ToString(), out int seconds))
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(seconds);
        }
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (RefreshRequested is not null)
        {
            await RefreshRequested();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr windowHandle, uint affinity);
}
