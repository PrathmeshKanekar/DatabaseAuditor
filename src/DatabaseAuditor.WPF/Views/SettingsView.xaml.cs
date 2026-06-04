namespace DatabaseAuditor.WPF.Views;

using DatabaseAuditor.WPF.ViewModels;
using System.Windows.Controls;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
