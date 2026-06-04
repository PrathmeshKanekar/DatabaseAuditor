namespace DatabaseAuditor.WPF.Controls;

using System.Windows;
using System.Windows.Controls;

public partial class SectionCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string),
            typeof(SectionCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string),
            typeof(SectionCard), new PropertyMetadata(string.Empty,
                OnSubtitleChanged));

    public static readonly DependencyProperty HasSubtitleProperty =
        DependencyProperty.Register(nameof(HasSubtitle), typeof(bool),
            typeof(SectionCard), new PropertyMetadata(false));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object),
            typeof(SectionCard), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public bool HasSubtitle
    {
        get => (bool)GetValue(HasSubtitleProperty);
        set => SetValue(HasSubtitleProperty, value);
    }

    public object HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    private static void OnSubtitleChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is SectionCard card)
            card.HasSubtitle = !string.IsNullOrEmpty(e.NewValue as string);
    }

    public SectionCard()
    {
        InitializeComponent();
    }
}