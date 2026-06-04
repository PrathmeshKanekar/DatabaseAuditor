namespace DatabaseAuditor.WPF.Controls;

using DatabaseAuditor.Domain.Enums;
using System.Windows;
using System.Windows.Controls;

public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty ChangeTypeProperty =
        DependencyProperty.Register(
            nameof(ChangeType),
            typeof(ChangeType),
            typeof(StatusBadge),
            new PropertyMetadata(ChangeType.Unchanged));

    public ChangeType ChangeType
    {
        get => (ChangeType)GetValue(ChangeTypeProperty);
        set => SetValue(ChangeTypeProperty, value);
    }

    public StatusBadge()
    {
        InitializeComponent();
    }
}