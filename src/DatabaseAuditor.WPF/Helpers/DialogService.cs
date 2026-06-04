namespace DatabaseAuditor.WPF.Helpers;

using Microsoft.Win32;
using System.Windows;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message);
    void ShowError(string title, string message);
    void ShowSuccess(string title, string message);
    void ShowInfo(string title, string message);
    string? ShowOpenFileDialog(string title, string filter);
    string? ShowSaveFileDialog(string title, string filter, string defaultFileName);
    string? ShowFolderBrowserDialog(string title);
    Task<T?> ShowDialogAsync<T>(Window dialog) where T : class;
}

public class DialogService : IDialogService
{
    private static Window MainWindow
        => Application.Current.MainWindow;

    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = MessageBox.Show(
            MainWindow,
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    public void ShowError(string title, string message)
        => MessageBox.Show(
            MainWindow,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);

    public void ShowSuccess(string title, string message)
        => MessageBox.Show(
            MainWindow,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    public void ShowInfo(string title, string message)
        => MessageBox.Show(
            MainWindow,
            message,
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    public string? ShowOpenFileDialog(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            Multiselect = false
        };

        return dialog.ShowDialog(MainWindow) == true
            ? dialog.FileName
            : null;
    }

    public string? ShowSaveFileDialog(
        string title,
        string filter,
        string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName,
            OverwritePrompt = true
        };

        return dialog.ShowDialog(MainWindow) == true
            ? dialog.FileName
            : null;
    }

    public string? ShowFolderBrowserDialog(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        return dialog.ShowDialog(MainWindow) == true
            ? dialog.FolderName
            : null;
    }

    public Task<T?> ShowDialogAsync<T>(Window dialog) where T : class
    {
        dialog.Owner = MainWindow;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var result = dialog.ShowDialog();

        if (result == true && dialog.DataContext is T vm)
            return Task.FromResult<T?>(vm);

        return Task.FromResult<T?>(null);
    }
}