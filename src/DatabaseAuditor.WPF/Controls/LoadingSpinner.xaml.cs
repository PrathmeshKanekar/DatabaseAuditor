namespace DatabaseAuditor.WPF.Controls;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

public partial class LoadingSpinner : UserControl
{
    private Storyboard? _spinStoryboard;

    public static readonly DependencyProperty SpinnerSizeProperty =
        DependencyProperty.Register(nameof(SpinnerSize), typeof(double),
            typeof(LoadingSpinner), new PropertyMetadata(36.0));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string),
            typeof(LoadingSpinner), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HasMessageProperty =
        DependencyProperty.Register(nameof(HasMessage), typeof(bool),
            typeof(LoadingSpinner), new PropertyMetadata(false));

    public double SpinnerSize
    {
        get => (double)GetValue(SpinnerSizeProperty);
        set => SetValue(SpinnerSizeProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set
        {
            SetValue(MessageProperty, value);
            SetValue(HasMessageProperty, !string.IsNullOrEmpty(value));
        }
    }

    public bool HasMessage
    {
        get => (bool)GetValue(HasMessageProperty);
        set => SetValue(HasMessageProperty, value);
    }

    public LoadingSpinner()
    {
        InitializeComponent();
        BuildSpinAnimation();
    }

    private void BuildSpinAnimation()
    {
        var rotation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(0.9)),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(rotation, SpinnerArc);
        Storyboard.SetTargetProperty(rotation,
            new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));

        _spinStoryboard = new Storyboard();
        _spinStoryboard.Children.Add(rotation);
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
            StartSpin();
        else
            StopSpin();
    }

    public void StartSpin() => _spinStoryboard?.Begin();
    public void StopSpin() => _spinStoryboard?.Stop();
}