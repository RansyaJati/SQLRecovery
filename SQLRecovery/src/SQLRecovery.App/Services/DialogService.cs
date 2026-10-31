using System.IO;
using Microsoft.Win32;
using SQLRecovery.Export;

namespace SQLRecovery.App.Services;

/// <summary>Thin wrapper around the Win32 common dialogs used by the app.</summary>
public sealed class DialogService
{
    public const string DatabaseFilter =
        "SQL Server database files (*.mdf;*.ndf;*.ldf)|*.mdf;*.ndf;*.ldf|" +
        "Primary data files (*.mdf)|*.mdf|" +
        "Secondary data files (*.ndf)|*.ndf|" +
        "Log files (*.ldf)|*.ldf|" +
        "All files (*.*)|*.*";

    /// <summary>Lets the user pick one or more MDF/NDF/LDF files.</summary>
    public string[]? OpenDatabaseFiles(string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open SQL Server database files",
            Filter = DatabaseFilter,
            Multiselect = true,
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog() == true ? dialog.FileNames : null;
    }

    /// <summary>Asks for an export destination for a single table.</summary>
    public string? SaveExportFile(ExportFormat format, string suggestedFileName, string? initialDirectory)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export table",
            FileName = suggestedFileName,
            DefaultExt = ExportService.GetDefaultExtension(format),
            Filter = ExportService.GetFileFilter(format) + "|All files (*.*)|*.*",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose an output folder",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public bool Confirm(string title, string message) =>
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

    public void ShowError(string title, string message) =>
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error);

    public void ShowInfo(string title, string message) =>
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
}
