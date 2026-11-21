using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Rectangle = System.Drawing.Rectangle;

namespace dịchMH;

public partial class SelectionWindow : Window
{
    private Point _dragStart;

    public Rectangle? SelectedPixels { get; private set; }

    public SelectionWindow()
    {
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(SelectionCanvas);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        SelectionRectangle.Visibility = Visibility.Visible;
        SelectionCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (!SelectionCanvas.IsMouseCaptured)
        {
            return;
        }

        Point current = e.GetPosition(SelectionCanvas);
        double left = Math.Min(_dragStart.X, current.X);
        double top = Math.Min(_dragStart.Y, current.Y);
        Canvas.SetLeft(SelectionRectangle, left);
        Canvas.SetTop(SelectionRectangle, top);
        SelectionRectangle.Width = Math.Abs(current.X - _dragStart.X);
        SelectionRectangle.Height = Math.Abs(current.Y - _dragStart.Y);
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!SelectionCanvas.IsMouseCaptured)
        {
            return;
        }

        SelectionCanvas.ReleaseMouseCapture();
        Point end = e.GetPosition(SelectionCanvas);
        Point firstPixel = SelectionCanvas.PointToScreen(_dragStart);
        Point lastPixel = SelectionCanvas.PointToScreen(end);
        int left = (int)Math.Round(Math.Min(firstPixel.X, lastPixel.X));
        int top = (int)Math.Round(Math.Min(firstPixel.Y, lastPixel.Y));
        int width = (int)Math.Round(Math.Abs(lastPixel.X - firstPixel.X));
        int height = (int)Math.Round(Math.Abs(lastPixel.Y - firstPixel.Y));

        if (width >= 8 && height >= 8)
        {
            SelectedPixels = new Rectangle(left, top, width, height);
            DialogResult = true;
        }
        else
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            InstructionsText.Text = "Vùng quá nhỏ — hãy kéo lại hoặc nhấn Esc để hủy.";
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}
