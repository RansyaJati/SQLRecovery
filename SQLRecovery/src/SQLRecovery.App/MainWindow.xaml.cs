using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SQLRecovery.App.Converters;
using SQLRecovery.App.ViewModels;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace SQLRecovery.App;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly IValueConverter _cellConverter;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        FitToScreen();

        _viewModel = viewModel;
        DataContext = viewModel;

        _cellConverter = Application.Current.TryFindResource("CellTextConverter") as IValueConverter
                         ?? new CellTextConverter();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += OnWindowClosed;

        RebuildColumns();
    }

    private void FitToScreen()
    {
        var workArea = SystemParameters.WorkArea;

        double width = Math.Min(Width, workArea.Width - 32);
        double height = Math.Min(Height, workArea.Height - 32);

        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);

        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTable))
            RebuildColumns();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }

    private void RebuildColumns()
    {
        RowGrid.Columns.Clear();

        var table = _viewModel.SelectedTable?.Table;

        if (table is null)
            return;

        var primary = ResolveBrush("BrushTextPrimary", Brushes.White);
        var secondary = ResolveBrush("BrushTextSecondary", Brushes.Gray);
        var muted = ResolveBrush("BrushTextMuted", Brushes.Gray);

        var ordinalColumn = new DataGridTextColumn
        {
            Header = new TextBlock
            {
                Text = "#",
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5,
                Foreground = secondary
            },
            Binding = new Binding(nameof(RowItem.Ordinal)),
            Width = new DataGridLength(58),
            IsReadOnly = true,
            ElementStyle = CreateOrdinalStyle(muted)
        };

        RowGrid.Columns.Add(ordinalColumn);

        for (int i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];

            var header = new StackPanel();

            header.Children.Add(new TextBlock
            {
                Text = column.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5,
                Foreground = primary,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            header.Children.Add(new TextBlock
            {
                Text = column.TypeDisplayName,
                FontSize = 10,
                Foreground = muted,
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            var gridColumn = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding($"[{i}]")
                {
                    Converter = _cellConverter,
                    Mode = BindingMode.OneWay
                },
                Width = new DataGridLength(180),
                MinWidth = 90,
                MaxWidth = 560,
                IsReadOnly = true
            };

            RowGrid.Columns.Add(gridColumn);
        }
    }

    private static Style CreateOrdinalStyle(Brush brush)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.ForegroundProperty, brush));
        style.Setters.Add(new Setter(TextBlock.FontSizeProperty, 11.5));
        style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
        style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0, 0, 4, 0)));
        return style;
    }

    private static Brush ResolveBrush(string key, Brush fallback) =>
        Application.Current.TryFindResource(key) as Brush ?? fallback;

    private void Window_OnDragOver(object sender, DragEventArgs e)
    {
        bool hasFiles = e.Data.GetDataPresent(DataFormats.FileDrop);

        e.Effects = hasFiles && !_viewModel.IsBusy
            ? DragDropEffects.Copy
            : DragDropEffects.None;

        e.Handled = true;
    }

    private async void Window_OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            await _viewModel.OpenFilesAsync(files);
    }
}
