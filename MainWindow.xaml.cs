using System.Windows;
using System.Windows.Controls;
using Rectangle = System.Drawing.Rectangle;

namespace dịchMH;

public partial class MainWindow : Window
{
    private readonly ScreenTranslationService _translationService = new();
    private Rectangle? _selectedArea;
    private string? _selectedLanguage;
    private OverlayWindow? _overlay;
    private TextOverlayWindow? _textOverlay;
    private bool _isTranslating;

    public MainWindow()
    {
        InitializeComponent();
        LoadOcrLanguages();
    }

    private void LoadOcrLanguages()
    {
        try
        {
            string[] languages = _translationService.AvailableOcrLanguages.ToArray();
            LanguageComboBox.ItemsSource = languages;

            if (languages.Length == 0)
            {
                SelectAreaButton.IsEnabled = false;
                StatusText.Text = "Windows chưa cài gói OCR nào. Cài gói ngôn ngữ có tính năng nhận dạng chữ viết tay/nhập rồi mở lại ứng dụng.";
                return;
            }

            LanguageComboBox.SelectedItem = languages.Contains("en-US", StringComparer.OrdinalIgnoreCase)
                ? languages.First(language => string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase))
                : languages[0];
            StatusText.Text = $"Đã tìm thấy {languages.Length} ngôn ngữ OCR của Windows. Cài thêm gói ngôn ngữ trong Windows để nhận diện ngôn ngữ khác.";
        }
        catch (Exception exception)
        {
            SelectAreaButton.IsEnabled = false;
            StatusText.Text = $"Không thể khởi tạo OCR của Windows: {exception.Message}";
        }
    }

    private async void SelectAreaButton_Click(object sender, RoutedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is not string selectedLanguage)
        {
            StatusText.Text = "Hãy chọn một ngôn ngữ OCR đã được cài đặt trong Windows.";
            return;
        }

        SelectAreaButton.IsEnabled = false;
        Hide();

        try
        {
            var selectionWindow = new SelectionWindow();
            bool? selected = selectionWindow.ShowDialog();
            if (selected != true || selectionWindow.SelectedPixels is not Rectangle area)
            {
                Show();
                Activate();
                return;
            }

            _selectedArea = area;
            _selectedLanguage = selectedLanguage;
            _overlay = new OverlayWindow
            {
                RefreshRequested = TranslateSelectedAreaAsync,
                Left = Math.Max(SystemParameters.WorkArea.Left + 12, SystemParameters.WorkArea.Right - 428),
                Top = Math.Max(SystemParameters.WorkArea.Top + 12, SystemParameters.WorkArea.Bottom - 72)
            };
            _overlay.Closed += Overlay_Closed;
            _overlay.SetRefreshInterval(GetSelectedRefreshInterval());
            _overlay.StartAutoRefresh(AutoRefreshCheckBox.IsChecked == true);
            _textOverlay = new TextOverlayWindow();
            _textOverlay.Show();
            _overlay.Show();
            if (!_textOverlay.IsExcludedFromCapture || !_overlay.IsExcludedFromCapture)
            {
                _overlay.SetCaptureFallback();
            }

            await TranslateSelectedAreaAsync();
        }
        catch (Exception exception)
        {
            Show();
            Activate();
            StatusText.Text = $"Không thể mở vùng chọn: {exception.Message}";
        }
        finally
        {
            SelectAreaButton.IsEnabled = true;
        }
    }

    private async Task TranslateSelectedAreaAsync()
    {
        if (_isTranslating || _selectedArea is not Rectangle area || _selectedLanguage is null)
        {
            return;
        }

        _isTranslating = true;
        _overlay?.SetBusy();

        try
        {
            byte[] image;
            bool hideOverlaysForCapture =
                _textOverlay?.IsExcludedFromCapture != true
                || _overlay?.IsExcludedFromCapture != true;
            if (hideOverlaysForCapture)
            {
                _textOverlay?.SetAnnotationsVisible(false);
                _overlay?.Hide();
                try
                {
                    await Task.Delay(35);
                    image = await _translationService.CaptureScreenAreaAsync(area);
                }
                finally
                {
                    _textOverlay?.SetAnnotationsVisible(true);
                    _overlay?.Show();
                }
            }
            else
            {
                image = await _translationService.CaptureScreenAreaAsync(area);
            }

            IReadOnlyList<TranslatedScreenLine> translations =
                await _translationService.TranslateCapturedImageAsync(image, _selectedLanguage);
            _textOverlay?.SetLines(area, translations);
            _overlay?.SetTranslation(translations.Count);
        }
        catch (Exception exception)
        {
            _overlay?.SetError($"Không dịch được: {exception.Message}");
        }
        finally
        {
            _isTranslating = false;
        }
    }

    private void Overlay_Closed(object? sender, EventArgs e)
    {
        _overlay = null;
        _textOverlay?.Close();
        _textOverlay = null;
        Show();
        Activate();
    }

    private TimeSpan GetSelectedRefreshInterval()
    {
        if (ScanIntervalComboBox.SelectedItem is ComboBoxItem selected
            && int.TryParse(selected.Tag?.ToString(), out int seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(2);
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        UpdatePageWidth();
        MainScrollViewer.ScrollToTop();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePageWidth();
    }

    private void MainScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePageWidth();
    }

    private void UpdatePageWidth()
    {
        if (PageWidthGrid is null)
        {
            return;
        }

        double availableWidth = MainScrollViewer.ActualWidth;
        if (MainScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
        {
            availableWidth -= SystemParameters.VerticalScrollBarWidth;
        }

        PageWidthGrid.Width = Math.Min(Math.Max(0, availableWidth), PageWidthGrid.MaxWidth);
    }
}
