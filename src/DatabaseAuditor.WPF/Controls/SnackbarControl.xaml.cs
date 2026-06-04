namespace DatabaseAuditor.WPF.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

public enum SnackbarType
{
    Success,
    Error,
    Warning,
    Info
}

public partial class SnackbarControl : UserControl
{
    private DispatcherTimer? _dismissTimer;
    private bool _isShowing;

    public SnackbarControl()
    {
        InitializeComponent();
    }

    public void Show(
        string message,
        SnackbarType type = SnackbarType.Info,
        int durationMs = 3500)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ApplyType(type);
            MessageText.Text = message;
            IsHitTestVisible = true;

            PlaySlideUp();

            _dismissTimer?.Stop();
            _dismissTimer = new DispatcherTimer();
            _dismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _dismissTimer.Tick += (_, _) => Dismiss();
            _dismissTimer.Start();

            _isShowing = true;
        });
    }

    public void Dismiss()
    {
        if (!_isShowing) return;

        _dismissTimer?.Stop();
        _isShowing = false;

        Application.Current.Dispatcher.Invoke(() =>
        {
            PlaySlideDown();
            IsHitTestVisible = false;
        });
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
        => Dismiss();

    private void ApplyType(SnackbarType type)
    {
        switch (type)
        {
            case SnackbarType.Success:
                SnackbarRoot.Background = GetBrush("AddedBgBrush");
                SnackbarRoot.BorderBrush = GetBrush("AddedBrush");
                MessageText.Foreground = GetBrush("AddedBrush");
                IconText.Text = "✓";
                IconText.Foreground = GetBrush("AddedBrush");
                break;

            case SnackbarType.Error:
                SnackbarRoot.Background = GetBrush("DeletedBgBrush");
                SnackbarRoot.BorderBrush = GetBrush("DeletedBrush");
                MessageText.Foreground = GetBrush("DeletedBrush");
                IconText.Text = "✕";
                IconText.Foreground = GetBrush("DeletedBrush");
                break;

            case SnackbarType.Warning:
                SnackbarRoot.Background = GetBrush("ModifiedBgBrush");
                SnackbarRoot.BorderBrush = GetBrush("ModifiedBrush");
                MessageText.Foreground = GetBrush("ModifiedBrush");
                IconText.Text = "⚠";
                IconText.Foreground = GetBrush("ModifiedBrush");
                break;

            case SnackbarType.Info:
            default:
                SnackbarRoot.Background = GetBrush("SurfaceElevatedBrush");
                SnackbarRoot.BorderBrush = GetBrush("AccentBrush");
                MessageText.Foreground = GetBrush("TextPrimaryBrush");
                IconText.Text = "ℹ";
                IconText.Foreground = GetBrush("AccentBrush");
                break;
        }
    }

    private void PlaySlideUp()
    {
        var sb = new Storyboard();

        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(200))
        };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade,
            new PropertyPath(OpacityProperty));

        var slide = new DoubleAnimation
        {
            From = 40,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, this);
        Storyboard.SetTargetProperty(slide,
            new PropertyPath(
                "(UIElement.RenderTransform).(TranslateTransform.Y)"));

        sb.Children.Add(fade);
        sb.Children.Add(slide);
        sb.Begin();
    }

    private void PlaySlideDown()
    {
        var sb = new Storyboard();

        var fade = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(180))
        };
        Storyboard.SetTarget(fade, this);
        Storyboard.SetTargetProperty(fade,
            new PropertyPath(OpacityProperty));

        var slide = new DoubleAnimation
        {
            From = 0,
            To = 40,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(slide, this);
        Storyboard.SetTargetProperty(slide,
            new PropertyPath(
                "(UIElement.RenderTransform).(TranslateTransform.Y)"));

        sb.Children.Add(fade);
        sb.Children.Add(slide);
        sb.Begin();
    }

    private static Brush GetBrush(string key)
    {
        if (Application.Current.Resources[key] is Brush brush)
            return brush;
        return Brushes.Gray;
    }
}