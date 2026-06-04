namespace DatabaseAuditor.WPF.Views.Dialogs;

using DatabaseAuditor.Domain.Entities;
using DatabaseAuditor.WPF.ViewModels.Dialogs;
using System.Windows;

public partial class AddConnectionDialog : Window
{
    private readonly AddConnectionViewModel _viewModel;

    public AddConnectionDialog(AddConnectionViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public void InitializeForAdd()
    {
        _viewModel.Name = string.Empty;
        _viewModel.Server = string.Empty;
        _viewModel.Port = 1433;
        _viewModel.DatabaseName = string.Empty;
        _viewModel.Username = string.Empty;
        _viewModel.Password = string.Empty;
        PasswordInput.Password = string.Empty;
        _viewModel.DialogTitle = "Add Connection";
    }

    public void InitializeForEdit(ConnectionProfile profile)
    {
        _viewModel.LoadFromProfile(profile);
        PasswordInput.Password = profile.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Copy secure password back to ViewModel
        _viewModel.Password = PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(_viewModel.Name))
        {
            MessageBox.Show("Please enter a profile name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.Server))
        {
            MessageBox.Show("Please enter a server host.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
