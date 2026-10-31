using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SQLRecovery.App.Services;
using SQLRecovery.App.Views;
using SQLRecovery.Core;
using SQLRecovery.Core.Schema;
using SQLRecovery.Export;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace SQLRecovery.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const int PageSize = 200;

    private readonly DialogService _dialogs;
    private readonly SettingsService _settings;
    private readonly ExportService _exportService = new();

    private DatabaseService? _database;
    private DatabaseInfo? _info;
    private List<TableListItem> _allTables = new();
    private int _loadGeneration;
    private bool _suppressSelectionReload;

    private bool _hasDatabase;
    private bool _isBusy;
    private bool _isExporting;
    private bool _isLoadingRows;
    private bool _hasMoreRows;
    private bool _hasRows;
    private bool _isDarkTheme = true;
    private double _progress;
    private string _statusMessage = "Ready";
    private string _databaseName = "No database loaded";
    private string _databaseFile = string.Empty;
    private string _databaseVersion = "—";
    private string _databaseSize = "—";
    private string _databaseTables = "—";
    private string _databasePages = "—";
    private string _searchText = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _showSystemTables;
    private TableListItem? _selectedTable;

    public MainViewModel(DialogService dialogs, SettingsService settings)
    {
        _dialogs = dialogs;
        _settings = settings;

        RecentFiles = new ObservableCollection<RecentFileItem>();

        OpenDatabaseCommand = new AsyncRelayCommand(OpenDatabaseAsync, () => !IsBusy);
        OpenRecentCommand = new AsyncRelayCommand<RecentFileItem?>(OpenRecentAsync, _ => !IsBusy);
        CloseDatabaseCommand = new RelayCommand(CloseDatabase, () => HasDatabase && !IsBusy);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => HasDatabase && !IsBusy);
        ExportTableCommand = new AsyncRelayCommand(ExportTableAsync, () => CanExportTable);
        ExportAllCommand = new AsyncRelayCommand(ExportAllAsync, () => CanExportAll);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync, () => CanLoadMore);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ClearRecentCommand = new RelayCommand(ClearRecent, () => RecentFiles.Count > 0);
        DismissErrorCommand = new RelayCommand(() => ErrorMessage = string.Empty);

        _isDarkTheme = settings.IsDarkTheme;
        ApplyTheme(_isDarkTheme);
        RefreshRecentFiles();
    }

    // --- collections --------------------------------------------------------

    public ObservableCollection<TableListItem> Tables { get; } = new();

    public ObservableCollection<RowItem> Rows { get; } = new();

    public ObservableCollection<RecentFileItem> RecentFiles { get; }

    // --- commands -----------------------------------------------------------

    public AsyncRelayCommand OpenDatabaseCommand { get; }

    public AsyncRelayCommand<RecentFileItem?> OpenRecentCommand { get; }

    public RelayCommand CloseDatabaseCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ExportTableCommand { get; }

    public AsyncRelayCommand ExportAllCommand { get; }

    public AsyncRelayCommand LoadMoreCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

    public RelayCommand ClearRecentCommand { get; }

    public RelayCommand DismissErrorCommand { get; }

    // --- state --------------------------------------------------------------

    public bool HasDatabase
    {
        get => _hasDatabase;
        private set
        {
            if (!SetProperty(ref _hasDatabase, value))
                return;

            OnPropertyChanged(nameof(IsWelcomeVisible));
            OnPropertyChanged(nameof(ShowNoTablesState));
            RaiseTableState();
            RefreshCommands();
        }
    }

    public bool IsWelcomeVisible => !HasDatabase;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            OnPropertyChanged(nameof(ShowNoTablesState));
            OnPropertyChanged(nameof(IsProgressVisible));
            RefreshCommands();
        }
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (!SetProperty(ref _isExporting, value))
                return;

            OnPropertyChanged(nameof(IsProgressVisible));
            RefreshCommands();
        }
    }

    public bool IsProgressVisible => IsBusy || IsExporting;

    public bool IsLoadingRows
    {
        get => _isLoadingRows;
        private set
        {
            if (!SetProperty(ref _isLoadingRows, value))
                return;

            OnPropertyChanged(nameof(ShowTableEmptyState));
        }
    }

    public bool HasRows
    {
        get => _hasRows;
        private set
        {
            if (SetProperty(ref _hasRows, value))
                OnPropertyChanged(nameof(ShowTableEmptyState));
        }
    }

    public bool HasMoreRows
    {
        get => _hasMoreRows;
        private set
        {
            if (SetProperty(ref _hasMoreRows, value))
                RefreshCommands();
        }
    }

    public double Progress
    {
        get => _progress;
        private set => SetProperty(ref _progress, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DatabaseName
    {
        get => _databaseName;
        private set => SetProperty(ref _databaseName, value);
    }

    public string DatabaseFile
    {
        get => _databaseFile;
        private set => SetProperty(ref _databaseFile, value);
    }

    public string DatabaseVersion
    {
        get => _databaseVersion;
        private set => SetProperty(ref _databaseVersion, value);
    }

    public string DatabaseSize
    {
        get => _databaseSize;
        private set => SetProperty(ref _databaseSize, value);
    }

    public string DatabaseTables
    {
        get => _databaseTables;
        private set => SetProperty(ref _databaseTables, value);
    }

    public string DatabasePages
    {
        get => _databasePages;
        private set => SetProperty(ref _databasePages, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
                return;

            ApplyFilter();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
                return;

            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowSystemTables
    {
        get => _showSystemTables;
        set
        {
            if (!SetProperty(ref _showSystemTables, value))
                return;

            ApplyFilter();
        }
    }

    public TableListItem? SelectedTable
    {
        get => _selectedTable;
        set
        {
            if (!SetProperty(ref _selectedTable, value))
                return;

            RaiseTableState();
            RefreshCommands();

            if (!_suppressSelectionReload)
                _ = LoadRowsAsync(value);
        }
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        private set
        {
            if (!SetProperty(ref _isDarkTheme, value))
                return;

            OnPropertyChanged(nameof(ThemeLabel));
            OnPropertyChanged(nameof(IsLightTheme));
        }
    }

    public bool IsLightTheme => !IsDarkTheme;

    public string ThemeLabel => IsDarkTheme ? "Dark" : "Light";

    // --- derived UI state ---------------------------------------------------

    public string TableTitle => SelectedTable?.FullName
        ?? (HasDatabase ? "Select a table" : "Welcome to SQL Recovery");

    public string TableSubtitle
    {
        get
        {
            var table = SelectedTable?.Table;

            if (table is null)
                return HasDatabase ? "Choose a table from the sidebar to preview its rows." : string.Empty;

            var storage = table.IsHeap ? "heap" : "clustered index";
            return $"{table.Columns.Count} columns · ~{table.RowCount:N0} rows · {storage}";
        }
    }

    public string RowsLoadedText
    {
        get
        {
            var table = SelectedTable?.Table;

            if (table is null)
                return string.Empty;

            return table.RowCount > 0
                ? $"{Rows.Count:N0} of ~{table.RowCount:N0} rows loaded"
                : $"{Rows.Count:N0} rows loaded";
        }
    }

    public bool ShowTableEmptyState =>
        HasDatabase && SelectedTable is not null && !IsLoadingRows && !HasRows && !IsBusy;

    public bool ShowNoTablesState =>
        HasDatabase && Tables.Count == 0 && !IsBusy;

    public bool CanExportTable => HasDatabase && SelectedTable is not null && !IsBusy && !IsExporting;

    public bool CanExportAll => HasDatabase && !IsBusy && !IsExporting && _allTables.Any(t => !t.IsSystem);

    public bool CanLoadMore => HasDatabase && SelectedTable is not null && HasMoreRows && !IsBusy && !IsLoadingRows;

    // --- entry points -------------------------------------------------------

    /// <summary>Opens the given files, used by drag &amp; drop.</summary>
    public async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        if (IsBusy)
            return;

        var files = paths
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
            return;

        await LoadDatabaseAsync(files);
    }

    // --- open / close -------------------------------------------------------

    private async Task OpenDatabaseAsync()
    {
        if (IsBusy)
            return;

        var files = _dialogs.OpenDatabaseFiles(_settings.LastDatabaseFolder);

        if (files is null || files.Length == 0)
            return;

        await LoadDatabaseAsync(files);
    }

    private async Task OpenRecentAsync(RecentFileItem? item)
    {
        if (item is null || IsBusy)
            return;

        if (!File.Exists(item.FilePath))
        {
            ErrorMessage = $"The file '{item.FilePath}' no longer exists.";
            _settings.RemoveRecentFile(item.FilePath);
            RefreshRecentFiles();
            return;
        }

        await LoadDatabaseAsync(new[] { item.FilePath });
    }

    private async Task LoadDatabaseAsync(string[] files)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        StatusMessage = "Opening database…";
        Progress = 0;

        CloseCurrentDatabase();

        try
        {
            var database = await Task.Run(() => DatabaseService.Open(files));

            _database = database;
            _info = database.Info;

            DatabaseName = _info.Name;
            DatabaseFile = string.Join(", ", files.Select(Path.GetFileName));
            DatabaseVersion = _info.VersionDisplay;
            DatabaseSize = FormatSize(_info.FileSizeBytes);
            DatabaseTables = $"{database.Tables.Count(t => t.IsUserTable):N0} user tables";
            DatabasePages = $"{_info.TotalPages:N0} pages";

            _allTables = database.Tables.Select(t => new TableListItem(t)).ToList();

            HasDatabase = true;

            if (!database.Catalog.IsAvailable && !string.IsNullOrWhiteSpace(database.Catalog.Error))
                ErrorMessage = database.Catalog.Error;
            else if (_allTables.Count == 0)
                ErrorMessage = "No tables were found in the system catalog. The file may be corrupted or incomplete.";

            ApplyFilter();

            _settings.AddRecentFile(files[0]);
            _settings.LastDatabaseFolder = Path.GetDirectoryName(files[0]);
            RefreshRecentFiles();

            StatusMessage = _allTables.Count > 0
                ? $"Opened {_info.Name} — {_allTables.Count(t => !t.IsSystem):N0} user tables"
                : $"Opened {_info.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Could not open the database";
            ErrorMessage = ex.Message;
            _dialogs.ShowError("Could not open database",
                $"{ex.Message}\n\nMake sure the file is a valid SQL Server data file and that all NDF files belonging to the database are provided.");
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private void CloseDatabase()
    {
        CloseCurrentDatabase();

        _allTables = new List<TableListItem>();
        Tables.Clear();
        Rows.Clear();
        HasRows = false;
        HasMoreRows = false;
        SelectedTable = null;

        DatabaseName = "No database loaded";
        DatabaseFile = string.Empty;
        DatabaseVersion = "—";
        DatabaseSize = "—";
        DatabaseTables = "—";
        DatabasePages = "—";
        ErrorMessage = string.Empty;
        StatusMessage = "Ready";

        HasDatabase = false;
        RaiseTableState();
        RefreshCommands();
    }

    private void CloseCurrentDatabase()
    {
        _loadGeneration++;
        _database?.Dispose();
        _database = null;
        _info = null;

        _suppressSelectionReload = true;
        SelectedTable = null;
        _suppressSelectionReload = false;

        Tables.Clear();
        Rows.Clear();
        HasRows = false;
        HasMoreRows = false;
        IsLoadingRows = false;
    }

    private async Task RefreshAsync()
    {
        if (_database is null)
            return;

        var files = _database.Database.DataFiles.Select(f => f.Path).ToArray();

        if (files.Length == 0)
            return;

        await LoadDatabaseAsync(files);
    }

    // --- table list ---------------------------------------------------------

    private void ApplyFilter()
    {
        if (!HasDatabase)
            return;

        int? selectedObjectId = SelectedTable?.Table.ObjectId;
        string query = SearchText.Trim();

        var matches = _allTables
            .Where(t => ShowSystemTables || !t.IsSystem)
            .Where(t => query.Length == 0
                        || t.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || t.SchemaName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (selectedObjectId is not null && matches.All(t => t.Table.ObjectId != selectedObjectId))
            selectedObjectId = null;

        _suppressSelectionReload = true;

        Tables.Clear();
        foreach (var item in matches)
            Tables.Add(item);

        var restored = selectedObjectId is null
            ? null
            : matches.FirstOrDefault(t => t.Table.ObjectId == selectedObjectId);

        bool needsLoad = false;

        if (restored is null && matches.Count > 0 && selectedObjectId is null)
        {
            restored = matches[0];
            needsLoad = true;
        }

        SelectedTable = restored;
        _suppressSelectionReload = false;

        OnPropertyChanged(nameof(ShowNoTablesState));

        if (needsLoad)
            _ = LoadRowsAsync(restored);
        else if (restored is null)
            ClearRows();
    }

    private void ClearRows()
    {
        Rows.Clear();
        HasRows = false;
        HasMoreRows = false;
        OnPropertyChanged(nameof(RowsLoadedText));
    }

    private async Task LoadRowsAsync(TableListItem? item)
    {
        int generation = ++_loadGeneration;

        ClearRows();
        RaiseTableState();

        if (item is null || _database is null)
            return;

        var table = item.Table;

        IsLoadingRows = true;
        StatusMessage = $"Reading {table.FullName}…";

        try
        {
            var result = await Task.Run(() => ReadRows(_database, table, 0, PageSize));

            if (generation != _loadGeneration)
                return;

            foreach (var row in result.Rows)
                Rows.Add(row);

            HasRows = Rows.Count > 0;
            HasMoreRows = result.HasMore;
            OnPropertyChanged(nameof(RowsLoadedText));

            StatusMessage = HasRows
                ? $"Loaded {Rows.Count:N0} rows from {table.FullName}"
                : $"{table.FullName} contains no readable rows";
        }
        catch (Exception ex)
        {
            if (generation == _loadGeneration)
            {
                ErrorMessage = $"Failed to read {table.FullName}: {ex.Message}";
                StatusMessage = "Row reading failed";
            }
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoadingRows = false;
                RefreshCommands();
            }
        }
    }

    private async Task LoadMoreAsync()
    {
        if (_database is null || SelectedTable is null)
            return;

        int generation = _loadGeneration;
        var table = SelectedTable.Table;
        int skip = Rows.Count;

        IsLoadingRows = true;
        StatusMessage = $"Loading more rows from {table.FullName}…";

        try
        {
            var result = await Task.Run(() => ReadRows(_database, table, skip, PageSize));

            if (generation != _loadGeneration)
                return;

            foreach (var row in result.Rows)
                Rows.Add(row);

            HasRows = Rows.Count > 0;
            HasMoreRows = result.HasMore;
            OnPropertyChanged(nameof(RowsLoadedText));
            StatusMessage = HasMoreRows
                ? $"Loaded {Rows.Count:N0} rows — more available"
                : $"Loaded all {Rows.Count:N0} readable rows";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to read {table.FullName}: {ex.Message}";
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoadingRows = false;
                RefreshCommands();
            }
        }
    }

    private static (List<RowItem> Rows, bool HasMore) ReadRows(
        DatabaseService database, TableSchema table, int skip, int take)
    {
        var rows = new List<RowItem>(take);
        int index = 0;
        bool hasMore = false;

        foreach (var values in database.ReadRows(table))
        {
            if (index++ < skip)
                continue;

            if (rows.Count == take)
            {
                hasMore = true;
                break;
            }

            rows.Add(new RowItem(values, skip + rows.Count + 1));
        }

        return (rows, hasMore);
    }

    // --- export -------------------------------------------------------------

    private async Task ExportTableAsync()
    {
        if (_database is null || SelectedTable is null)
            return;

        var table = SelectedTable.Table;
        var window = new ExportWindow(false, table, _settings.LastExportFolder)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.DestinationPath))
            return;

        _settings.LastExportFolder = window.ExportAll
            ? window.DestinationPath
            : Path.GetDirectoryName(window.DestinationPath);

        await RunExportAsync(
            new[] { (table, window.DestinationPath!) },
            window.SelectedFormat,
            window.BuildOptions());
    }

    private async Task ExportAllAsync()
    {
        if (_database is null)
            return;

        var tables = _allTables.Where(t => !t.IsSystem).Select(t => t.Table).ToList();

        if (tables.Count == 0)
        {
            _dialogs.ShowInfo("Nothing to export", "This database does not contain any user tables.");
            return;
        }

        var window = new ExportWindow(true, tables[0], _settings.LastExportFolder)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true || string.IsNullOrWhiteSpace(window.DestinationPath))
            return;

        string folder = window.DestinationPath!;
        _settings.LastExportFolder = folder;
        Directory.CreateDirectory(folder);

        string extension = ExportService.GetDefaultExtension(window.SelectedFormat);
        var items = new List<(TableSchema Table, string Path)>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            string baseName = SanitizeFileName($"{table.SchemaName}.{table.Name}");
            string candidate = baseName;
            int suffix = 2;

            while (!usedNames.Add(candidate))
                candidate = $"{baseName}_{suffix++}";

            items.Add((table, Path.Combine(folder, candidate + extension)));
        }

        await RunExportAsync(items, window.SelectedFormat, window.BuildOptions());
    }

    private async Task RunExportAsync(
        IReadOnlyList<(TableSchema Table, string Path)> items,
        ExportFormat format,
        ExportOptions options)
    {
        if (_database is null || items.Count == 0)
            return;

        IsExporting = true;
        Progress = 0;
        StatusMessage = items.Count == 1
            ? $"Exporting {items[0].Table.FullName}…"
            : $"Exporting {items.Count} tables…";

        try
        {
            var progress = new Progress<double>(value =>
            {
                // Slot the table progress into the overall progress range.
                Progress = Math.Clamp(value, 0, 1);
            });

            await Task.Run(async () =>
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var (table, path) = items[i];

                    await _exportService.ExportTableAsync(
                        format,
                        table,
                        _database.ReadRows(table),
                        path,
                        options,
                        progress);
                }
            });

            Progress = 1;
            StatusMessage = items.Count == 1
                ? $"Exported {items[0].Table.FullName} to {Path.GetFileName(items[0].Path)}"
                : $"Exported {items.Count} tables to {Path.GetDirectoryName(items[0].Path)}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Export failed: {ex.Message}";
            StatusMessage = "Export failed";
            _dialogs.ShowError("Export failed", ex.Message);
        }
        finally
        {
            IsExporting = false;
            Progress = 0;
            RefreshCommands();
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(name.Length);

        foreach (char c in name)
            builder.Append(invalid.Contains(c) ? '_' : c);

        return builder.ToString();
    }

    // --- theme / recent -----------------------------------------------------

    public void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyTheme(IsDarkTheme);
        _settings.IsDarkTheme = IsDarkTheme;
    }

    private static void ApplyTheme(bool dark)
    {
        try
        {
            ApplicationThemeManager.Apply(
                dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
                WindowBackdropType.None,
                false);
        }
        catch
        {
            // Theme application is non-critical.
        }
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();

        foreach (var path in _settings.RecentFiles.Where(File.Exists))
            RecentFiles.Add(new RecentFileItem(path));

        ClearRecentCommand.NotifyCanExecuteChanged();
    }

    private void ClearRecent()
    {
        _settings.ClearRecentFiles();
        RefreshRecentFiles();
    }

    // --- helpers ------------------------------------------------------------

    private void RaiseTableState()
    {
        OnPropertyChanged(nameof(TableTitle));
        OnPropertyChanged(nameof(TableSubtitle));
        OnPropertyChanged(nameof(RowsLoadedText));
        OnPropertyChanged(nameof(ShowTableEmptyState));
        OnPropertyChanged(nameof(ShowNoTablesState));
        OnPropertyChanged(nameof(CanExportTable));
        OnPropertyChanged(nameof(CanExportAll));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    private void RefreshCommands()
    {
        OpenDatabaseCommand.NotifyCanExecuteChanged();
        OpenRecentCommand.NotifyCanExecuteChanged();
        CloseDatabaseCommand.NotifyCanExecuteChanged();
        RefreshCommand.NotifyCanExecuteChanged();
        ExportTableCommand.NotifyCanExecuteChanged();
        ExportAllCommand.NotifyCanExecuteChanged();
        LoadMoreCommand.NotifyCanExecuteChanged();
        ClearRecentCommand.NotifyCanExecuteChanged();
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{size:0} {units[unit]}" : $"{size:0.##} {units[unit]}";
    }

    public void Dispose()
    {
        CloseCurrentDatabase();
    }
}
