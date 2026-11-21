using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Drawing.Rectangle;

namespace dịchMH;

public partial class TextOverlayWindow : Window
{
    private const int ExtendedStyleIndex = -20;
    private const long TransparentExtendedStyle = 0x00000020;
    private const long ToolWindowExtendedStyle = 0x00000080;
    private const long NoActivateExtendedStyle = 0x08000000;
    private const uint ExcludeFromCaptureAffinity = 0x00000011;
    private Rectangle? _displayedArea;
    private TranslatedScreenLine[] _displayedLines = Array.Empty<TranslatedScreenLine>();

    public TextOverlayWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        SourceInitialized += ApplyClickThroughStyles;
    }

    public bool IsExcludedFromCapture { get; private set; }

    public void SetLines(Rectangle selectedArea, IReadOnlyList<TranslatedScreenLine> lines)
    {
        if (HasSameDisplay(selectedArea, lines))
        {
            return;
        }

        Point screenTopLeft = PointToScreen(new Point(0, 0));
        Point screenBottomRight = PointToScreen(new Point(ActualWidth, ActualHeight));
        double dpiScaleX = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double dpiScaleY = VisualTreeHelper.GetDpi(this).DpiScaleY;
        double maximumHeight = Math.Max(40, (screenBottomRight.Y - screenTopLeft.Y - 8) / dpiScaleY);

        for (int index = 0; index < lines.Count; index++)
        {
            TranslatedScreenLine line = lines[index];
            Rect source = new(
                selectedArea.Left + line.Bounds.X,
                selectedArea.Top + line.Bounds.Y,
                line.Bounds.Width,
                line.Bounds.Height);
            double x = Math.Clamp(source.Left - 4, screenTopLeft.X + 4, screenBottomRight.X - 20);
            Point localPosition = PointFromScreen(new Point(x, source.Top));
            double maxWidth = Math.Min(520, ActualWidth - localPosition.X - 8);
            if (maxWidth < 80)
            {
                x = Math.Max(screenTopLeft.X + 4, source.Right - 120);
                localPosition = PointFromScreen(new Point(x, source.Top));
                maxWidth = Math.Max(80, ActualWidth - localPosition.X - 8);
            }

            Border label;
            if (index < OverlayCanvas.Children.Count)
            {
                label = (Border)OverlayCanvas.Children[index];
            }
            else
            {
                label = CreateLabel();
                OverlayCanvas.Children.Add(label);
            }

            var text = (TextBlock)label.Child;
            text.Text = line.Text;
            text.FontSize = Math.Clamp(line.Bounds.Height / dpiScaleY * 0.88, 12, 24);
            label.MinWidth = Math.Min(maxWidth, source.Width / dpiScaleX + 10);
            label.MaxWidth = maxWidth;
            label.MaxHeight = maximumHeight;
            label.Measure(new Size(maxWidth, maximumHeight));

            double heightPixels = Math.Min(label.DesiredSize.Height * dpiScaleY, screenBottomRight.Y - screenTopLeft.Y - 8);
            double y = Math.Clamp(source.Top - 3, screenTopLeft.Y + 4, screenBottomRight.Y - heightPixels - 4);
            Rect placement = new(x, y, label.DesiredSize.Width * dpiScaleX, heightPixels);
            placement.X = Math.Clamp(placement.X, screenTopLeft.X + 4, screenBottomRight.X - placement.Width - 4);
            Point position = PointFromScreen(new Point(placement.X, placement.Y));
            Canvas.SetLeft(label, position.X);
            Canvas.SetTop(label, position.Y);
        }

        while (OverlayCanvas.Children.Count > lines.Count)
        {
            OverlayCanvas.Children.RemoveAt(OverlayCanvas.Children.Count - 1);
        }

        _displayedArea = selectedArea;
        _displayedLines = lines.ToArray();
    }

    public void Clear()
    {
        OverlayCanvas.Children.Clear();
        _displayedArea = null;
        _displayedLines = Array.Empty<TranslatedScreenLine>();
    }

    public void SetAnnotationsVisible(bool visible)
    {
        OverlayCanvas.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
    }

    private bool HasSameDisplay(Rectangle selectedArea, IReadOnlyList<TranslatedScreenLine> lines)
    {
        if (_displayedArea != selectedArea || _displayedLines.Length != lines.Count)
        {
            return false;
        }

        for (int index = 0; index < lines.Count; index++)
        {
            TranslatedScreenLine current = lines[index];
            TranslatedScreenLine previous = _displayedLines[index];
            if (!string.Equals(current.Text, previous.Text, StringComparison.Ordinal)
                || Math.Abs(current.Bounds.X - previous.Bounds.X) > 6
                || Math.Abs(current.Bounds.Y - previous.Bounds.Y) > 6
                || Math.Abs(current.Bounds.Width - previous.Bounds.Width) > 6
                || Math.Abs(current.Bounds.Height - previous.Bounds.Height) > 6)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyClickThroughStyles(object? sender, EventArgs e)
    {
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        IsExcludedFromCapture = SetWindowDisplayAffinity(handle, ExcludeFromCaptureAffinity);
        long styles = GetWindowLongPtr(handle, ExtendedStyleIndex).ToInt64();
        long updatedStyles = styles
            | TransparentExtendedStyle
            | ToolWindowExtendedStyle
            | NoActivateExtendedStyle;
        SetWindowLongPtr(handle, ExtendedStyleIndex, new IntPtr(updatedStyles));
    }

    private static Border CreateLabel()
    {
        var text = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Medium,
            TextWrapping = TextWrapping.Wrap
        };
        return new Border
        {
            Child = text,
            Background = new SolidColorBrush(Color.FromArgb(255, 18, 24, 35)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(220, 100, 145, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5, 2, 5, 2),
            IsHitTestVisible = false
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr windowHandle, uint affinity);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newStyle);
}
