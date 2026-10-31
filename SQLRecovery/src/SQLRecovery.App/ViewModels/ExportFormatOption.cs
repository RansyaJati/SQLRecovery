using SQLRecovery.Export;

namespace SQLRecovery.App.ViewModels;

public sealed class ExportFormatOption
{
    public ExportFormatOption(ExportFormat format, string displayName, string description)
    {
        Format = format;
        DisplayName = displayName;
        Description = description;
    }

    public ExportFormat Format { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public override string ToString() => DisplayName;
}

public sealed class DelimiterOption
{
    public DelimiterOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public string Value { get; }

    public string Label { get; }

    public override string ToString() => Label;
}
