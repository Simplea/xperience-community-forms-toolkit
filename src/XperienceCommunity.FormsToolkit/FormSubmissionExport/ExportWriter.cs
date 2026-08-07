using System.Globalization;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public interface IFormSubmissionExportWriter
{
    public FormSubmissionExportFormat Format { get; }

    public string ContentType { get; }

    public ValueTask<IFormSubmissionExportDocument> OpenAsync(
        Stream output,
        PreparedFormSubmissionExport export,
        CancellationToken cancellationToken);
}

public interface IFormSubmissionExportDocument : IAsyncDisposable
{
    public Task WriteRecordAsync(IReadOnlyList<FormSubmissionExportCell> cells, CancellationToken cancellationToken);

    public Task CompleteAsync(CancellationToken cancellationToken);
}

public interface IFormSubmissionExportValueFormatter
{
    public FormSubmissionExportCell Format(object? value, bool textual = false);
}

internal sealed class FormSubmissionExportValueFormatter : IFormSubmissionExportValueFormatter
{
    public FormSubmissionExportCell Format(object? value, bool textual = false)
    {
        if (value is null or DBNull)
        {
            return new FormSubmissionExportCell(string.Empty, textual);
        }

        string formatted = value switch
        {
            bool boolean => boolean ? "true" : "false",
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTime dateTime => FormatDateTime(dateTime),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        };

        return new FormSubmissionExportCell(formatted, textual || value is string or char);
    }

    internal static string ProtectFromFormulaInjection(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        if (value[0] is '\t' or '\r')
        {
            return "'" + value;
        }

        int firstNonWhitespace = 0;
        while (firstNonWhitespace < value.Length && char.IsWhiteSpace(value[firstNonWhitespace]))
        {
            firstNonWhitespace++;
        }

        return firstNonWhitespace < value.Length && value[firstNonWhitespace] is '=' or '+' or '-' or '@'
            ? "'" + value
            : value;
    }

    private static string FormatDateTime(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        return utc.ToString("O", CultureInfo.InvariantCulture);
    }
}
