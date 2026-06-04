namespace DatabaseAuditor.WPF.Helpers;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

public class NavigationService
{
    private readonly ContentControl _contentRegion;
    private readonly Stack<UIElement> _history = new();
    private UIElement? _currentPage;

    public event EventHandler<string>? NavigatedTo;

    public string CurrentPageName { get; private set; } = string.Empty;

    public bool CanGoBack => _history.Count > 0;

    public NavigationService(ContentControl contentRegion)
    {
        _contentRegion = contentRegion;
    }

    public void NavigateTo(UIElement page, string pageName, bool addToHistory = true)
    {
        if (_currentPage != null && addToHistory)
            _history.Push(_currentPage);

        _currentPage = page;
        CurrentPageName = pageName;

        // Ensure page has a TranslateTransform for animation
        if (page is FrameworkElement fe)
        {
            fe.RenderTransform = new TranslateTransform();
            fe.RenderTransformOrigin = new Point(0.5, 0.5);
            fe.Opacity = 0;
        }

        _contentRegion.Content = page;

        PlayFadeInAnimation(page);

        NavigatedTo?.Invoke(this, pageName);
    }

    public void GoBack()
    {
        if (!CanGoBack) return;

        var previous = _history.Pop();
        NavigateTo(previous, CurrentPageName, addToHistory: false);
    }

    public void ClearHistory()
        => _history.Clear();

    private static void PlayFadeInAnimation(UIElement element)
    {
        var storyboard = new Storyboard();

        // Fade
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade,
            new PropertyPath(UIElement.OpacityProperty));

        // Slide up
        var slide = new DoubleAnimation
        {
            From = 14,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(slide, element);
        Storyboard.SetTargetProperty(slide,
            new PropertyPath(
                "(UIElement.RenderTransform).(TranslateTransform.Y)"));

        storyboard.Children.Add(fade);
        storyboard.Children.Add(slide);
        storyboard.Begin();
    }
}