using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SQLRecovery.App.Services;
using SQLRecovery.App.ViewModels;
using SQLRecovery.Core.Schema;
using SQLRecovery.Export;
using Wpf.Ui.Controls;

namespace SQLRecovery.App.Views;

public partial class ExportWindow : FluentWindow
{
    private readonly TableSchema _table;
    private readonly DialogService _dialogs = new();
    private readonly string _initialFolder;
    private readonly List<ExportFormatOption> _formats;
    private bool _pathIsAuto = true;

    public ExportWindow(bool exportAll, TableSchema table)
        : this(exportAll, table, null)
    {
    }

    public ExportWindow(bool exportAll, TableSchema table, string? initialFolder)
    {
        InitializeComponent();

        ExportAll = exportAll;
        _table = table;
        _initialFolder = ResolveInitialFolder(initialFolder);

        _formats = new List<ExportFormatOption>
        {
            new(ExportFormat.Csv, "CSV — comma separated values",
                "Plain text table, ideal for spreadsheets and bulk import tools."),
            new(ExportFormat.SqlScript, "SQL script — CREATE TABLE + INSERT",
                "Generates DDL and batch INSERT statements for SQL Server, MySQL, or PostgreSQL."),
            new(ExportFormat.Json, "JSON",
                "One JSON document containing every row as an object with named properties."),
            new(ExportFormat.Excel, "Excel workbook (.xlsx)",
                "Formatted spreadsheet with bold headers, filters and frozen panes."),
            new(ExportFormat.Sqlite, "SQLite database (.db)",
                "Single-file database that can be queried with any SQLite client.")
        };

        FormatCombo.ItemsSource = _formats;

        DialectCombo.ItemsSource = new List<DialectOption>
        {
            new(SqlDialect.SqlServer, "SQL Server (T-SQL)"),
            new(SqlDialect.MySql, "MySQL / MariaDB"),
            new(SqlDialect.PostgreSql, "PostgreSQL")
        };
        DialectCombo.SelectedIndex = 0;

        DelimiterCombo.ItemsSource = new List<DelimiterOption>
        {
            new(",", "Comma  ,"),
            new(";", "Semicolon  ;"),
            new("\t", "Tab"),
            new("|", "Pipe  |")
        };
        DelimiterCombo.SelectedIndex = 0;

        HeaderText.Text = exportAll ? "Export all tables" : "Export table";
        SubHeaderText.Text = exportAll
            ? "Every user table in the database will be written to its own file inside the selected folder."
            : $"{table.FullName}   ·   {table.Columns.Count} columns   ·   ~{table.RowCount:N0} rows";

        FormatCombo.SelectedIndex = 0;
    }

    public bool ExportAll { get; }

    public ExportFormat SelectedFormat =>
        (FormatCombo.SelectedItem as ExportFormatOption)?.Format ?? ExportFormat.Csv;

    public string? DestinationPath =>
        string.IsNullOrWhiteSpace(DestinationBox.Text) ? null : DestinationBox.Text.Trim();

    public ExportOptions BuildOptions()
    {
        int batchSize = int.TryParse(BatchSizeBox.Text.Trim(), out int parsed) && parsed > 0
            ? parsed
            : 1000;

        string delimiter = (DelimiterCombo.SelectedItem as DelimiterOption)?.Value ?? ",";
        var dialect = (DialectCombo.SelectedItem as DialectOption)?.Dialect ?? SqlDialect.SqlServer;

        return new ExportOptions
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            CsvDelimiter = delimiter,
            IncludeHeaders = HeadersCheck.IsChecked == true,
            IncludeCreateTable = CreateTableCheck.IsChecked == true,
            JsonLines = JsonLinesCheck.IsChecked == true,
            WriteIndented = IndentedCheck.IsChecked == true,
            InsertBatchSize = batchSize,
            SqlDialect = dialect,
            NewLine = "\r\n"
        };
    }

    private void FormatCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_formats is null || FormatCombo.SelectedItem is not ExportFormatOption option)
            return;

        FormatHint.Text = option.Description;
        UpdateEnabledOptions();
        UpdateSuggestedPath();
    }

    private void UpdateEnabledOptions()
    {
        var format = SelectedFormat;

        bool isCsv = format == ExportFormat.Csv;
        bool isSql = format == ExportFormat.SqlScript;
        bool isJson = format == ExportFormat.Json;

        DelimiterCombo.IsEnabled = isCsv;
        DialectCombo.IsEnabled = isSql;
        BatchSizeBox.IsEnabled = isSql;
        CreateTableCheck.IsEnabled = isSql;
        JsonLinesCheck.IsEnabled = isJson;
        IndentedCheck.IsEnabled = isJson;
    }

    private void UpdateSuggestedPath()
    {
        string extension = ExportService.GetDefaultExtension(SelectedFormat);

        if (ExportAll)
        {
            DestinationBox.Text = _initialFolder;
            return;
        }

        if (!_pathIsAuto)
            return;

        string name = Sanitize($"{_table.SchemaName}.{_table.Name}") + extension;
        DestinationBox.Text = Path.Combine(_initialFolder, name);
    }

    private void Browse_OnClick(object sender, RoutedEventArgs e)
    {
        if (ExportAll)
        {
            var folder = _dialogs.PickFolder(DestinationBox.Text);

            if (!string.IsNullOrWhiteSpace(folder))
            {
                DestinationBox.Text = folder;
                _pathIsAuto = false;
            }

            return;
        }

        string suggestedName = Path.GetFileName(DestinationBox.Text);
        string extension = ExportService.GetDefaultExtension(SelectedFormat);

        if (string.IsNullOrWhiteSpace(suggestedName))
            suggestedName = Sanitize($"{_table.SchemaName}.{_table.Name}") + extension;

        if (!suggestedName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            suggestedName = Path.GetFileNameWithoutExtension(suggestedName) + extension;

        string? initial = Path.GetDirectoryName(DestinationBox.Text);

        var file = _dialogs.SaveExportFile(SelectedFormat, suggestedName, initial);

        if (!string.IsNullOrWhiteSpace(file))
        {
            DestinationBox.Text = file;
            _pathIsAuto = false;
        }
    }

    private void Export_OnClick(object sender, RoutedEventArgs e)
    {
        string? path = DestinationPath;

        if (string.IsNullOrWhiteSpace(path))
        {
            System.Windows.MessageBox.Show(this, "Choose a destination first.", "Export data",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        try
        {
            if (ExportAll)
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                string? directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"The destination is not usable:\n\n{ex.Message}", "Export data",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private static string ResolveInitialFolder(string? folder)
    {
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            return folder;

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return Directory.Exists(documents)
            ? documents
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string Sanitize(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);

        foreach (char c in name)
            builder.Append(invalid.Contains(c) ? '_' : c);

        return builder.ToString();
    }
}

public sealed record DialectOption(SqlDialect Dialect, string Label)
{
    public override string ToString() => Label;
}
