using System.Globalization;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public static class FormSubmissionExportOptionsParser
{
    public static FormSubmissionExportOptions Parse(
        FormSubmissionExportCommandRequest request,
        IReadOnlyList<FormSubmissionExportField> availableFields)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(availableFields);

        var format = ParseEnum<FormSubmissionExportFormat>(request.Format, "export format");
        var operation = ParseEnum<FormSubmissionExportOperation>(request.Operation, "export operation");
        var sortDirection = ParseOrder(request.Order);
        var range = FormSubmissionExportRangeParser.Parse(request);
        int? maximumRecords = ParseMaximumRecords(request.NumberOfRecords);
        int? effectiveMaximum = operation == FormSubmissionExportOperation.Preview
            ? Math.Min(maximumRecords ?? FormSubmissionExportConstants.PreviewRecordLimit, FormSubmissionExportConstants.PreviewRecordLimit)
            : maximumRecords;

        char delimiter = ParseDelimiter(request.Delimiter, format);
        bool includeHeader = format != FormSubmissionExportFormat.Xml && (request.IncludeHeader ?? true);
        IReadOnlyList<string> columns = ParseColumns(request.Columns, availableFields);

        return new FormSubmissionExportOptions(
            format,
            operation,
            range,
            maximumRecords,
            effectiveMaximum,
            includeHeader,
            delimiter,
            sortDirection,
            columns);
    }

    public static void ValidateResolved(
        FormSubmissionExportOptions options,
        IReadOnlyList<FormSubmissionExportField> availableFields)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(availableFields);

        if (!Enum.IsDefined(options.Format) || !Enum.IsDefined(options.Operation) || !Enum.IsDefined(options.SortDirection))
        {
            throw new FormSubmissionExportValidationException("The export options are invalid.");
        }

        if (options.MaximumRecords is <= 0 || options.EffectiveMaximumRecords is <= 0)
        {
            throw new FormSubmissionExportValidationException("Number of records must be a positive whole number.");
        }

        int? expectedEffectiveMaximum = options.Operation == FormSubmissionExportOperation.Preview
            ? Math.Min(options.MaximumRecords ?? FormSubmissionExportConstants.PreviewRecordLimit, FormSubmissionExportConstants.PreviewRecordLimit)
            : options.MaximumRecords;

        if (options.EffectiveMaximumRecords != expectedEffectiveMaximum)
        {
            throw new FormSubmissionExportValidationException("The export record limit is invalid.");
        }

        if (options.Format == FormSubmissionExportFormat.Csv && options.CsvDelimiter is not ',' and not ';')
        {
            throw new FormSubmissionExportValidationException("The CSV delimiter is invalid.");
        }

        _ = ParseColumns(options.ColumnIdentifiers, availableFields);
    }

    private static TEnum ParseEnum<TEnum>(string? value, string label)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var result) || !Enum.IsDefined(result))
        {
            throw new FormSubmissionExportValidationException($"The {label} is invalid.");
        }

        return result;
    }

    private static FormSubmissionExportSortDirection ParseOrder(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "ascending" => FormSubmissionExportSortDirection.Ascending,
            "descending" => FormSubmissionExportSortDirection.Descending,
            _ => throw new FormSubmissionExportValidationException("The export ordering is invalid."),
        };

    private static int? ParseMaximumRecords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) || result <= 0)
        {
            throw new FormSubmissionExportValidationException("Number of records must be a positive whole number.");
        }

        return result;
    }

    private static char ParseDelimiter(string? value, FormSubmissionExportFormat format)
    {
        if (format != FormSubmissionExportFormat.Csv)
        {
            return ',';
        }

        return value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "comma" or "," => ',',
            "semicolon" or ";" => ';',
            _ => throw new FormSubmissionExportValidationException("The CSV delimiter is invalid."),
        };
    }

    private static IReadOnlyList<string> ParseColumns(
        IReadOnlyList<string>? requestedColumns,
        IReadOnlyList<FormSubmissionExportField> availableFields)
    {
        IReadOnlyList<string> columns = requestedColumns ?? availableFields.Select(field => field.Identifier).ToList();
        if (columns.Count == 0)
        {
            throw new FormSubmissionExportValidationException("Select at least one column to export.");
        }

        var available = availableFields.Select(field => field.Identifier).ToHashSet(StringComparer.Ordinal);
        if (columns.Any(string.IsNullOrWhiteSpace)
            || columns.Any(column => !available.Contains(column))
            || columns.Distinct(StringComparer.Ordinal).Count() != columns.Count)
        {
            throw new FormSubmissionExportValidationException("The selected export columns are invalid.");
        }

        return columns.ToList();
    }
}
