using System.Windows;
using System.Windows.Controls;

namespace DatabaseAuditor.WPF.Helpers;

public static class StackPanelHelper
{
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.RegisterAttached(
            "Spacing",
            typeof(double),
            typeof(StackPanelHelper),
            new FrameworkPropertyMetadata(0.0, OnSpacingChanged));

    public static double GetSpacing(DependencyObject obj)
    {
        return (double)obj.GetValue(SpacingProperty);
    }

    public static void SetSpacing(DependencyObject obj, double value)
    {
        obj.SetValue(SpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StackPanel stackPanel)
        {
            stackPanel.Loaded -= OnStackPanelLoaded;
            stackPanel.Loaded += OnStackPanelLoaded;
            UpdateSpacing(stackPanel);
        }
    }

    private static void OnStackPanelLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is StackPanel stackPanel)
        {
            UpdateSpacing(stackPanel);
        }
    }

    private static void UpdateSpacing(StackPanel stackPanel)
    {
        double spacing = GetSpacing(stackPanel);
        if (spacing <= 0) return;

        bool isHorizontal = stackPanel.Orientation == Orientation.Horizontal;
        var count = stackPanel.Children.Count;

        for (int i = 0; i < count - 1; i++)
        {
            var child = stackPanel.Children[i] as FrameworkElement;
            if (child == null) continue;

            if (isHorizontal)
            {
                child.Margin = new Thickness(child.Margin.Left, child.Margin.Top, spacing, child.Margin.Bottom);
            }
            else
            {
                child.Margin = new Thickness(child.Margin.Left, child.Margin.Top, child.Margin.Right, spacing);
            }
        }
    }
}
