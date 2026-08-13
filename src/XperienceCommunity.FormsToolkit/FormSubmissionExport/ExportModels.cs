namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public enum FormSubmissionExportFormat
{
    Excel,
    Csv,
    Xml,
}

public enum FormSubmissionExportOperation
{
    Export,
    Preview,
}

public enum FormSubmissionExportSortDirection
{
    Ascending,
    Descending,
}

public enum FormSubmissionExportFieldKind
{
    SubmissionId,
    Submitted,
    FormField,
}

public static class FormSubmissionExportFieldIdentifiers
{
    public const string SubmissionId = "__submissionId";
    public const string Submitted = "__submitted";
}

public sealed record FormSubmissionExportCommandRequest(
    string? Format,
    string? Operation,
    string? From,
    string? To,
    string? TimeZone,
    string? NumberOfRecords,
    bool? IncludeHeader,
    string? Delimiter,
    string? Order,
    IReadOnlyList<string>? Columns);

public sealed record FormSubmissionCurrentViewExportRequest(
    string? Format,
    IReadOnlyList<int>? SubmissionIds,
    IReadOnlyList<string>? Columns);

public sealed record FormSubmissionExportRange(
    DateOnly? From,
    DateOnly? To,
    DateTime? FromUtc,
    DateTime? ToExclusiveUtc);

public sealed record FormSubmissionExportOptions(
    FormSubmissionExportFormat Format,
    FormSubmissionExportOperation Operation,
    FormSubmissionExportRange Range,
    int? MaximumRecords,
    int? EffectiveMaximumRecords,
    bool IncludeHeader,
    char CsvDelimiter,
    FormSubmissionExportSortDirection SortDirection,
    IReadOnlyList<string> ColumnIdentifiers);

public sealed record FormSubmissionExportTokenPayload(
    int UserId,
    int FormId,
    string Permission,
    FormSubmissionExportOptions Options,
    DateTimeOffset ExpiresUtc);

public sealed class FormSubmissionExportTokenResponse
{
    public string? DownloadUrl { get; init; }

    public string? Error { get; init; }
}


public sealed record FormSubmissionExportFieldOption(
    string Identifier,
    string SourceName,
    string Caption,
    bool VisibleInListing);

public sealed record FormSubmissionExportField(
    string Identifier,
    string SourceName,
    string Caption,
    bool IsUploadedFile,
    FormSubmissionExportFieldKind Kind,
    bool VisibleInListing = true);

public sealed record FormSubmissionExportDefinition(
    int FormId,
    string FormCodeName,
    string FormClassName,
    string ItemIdColumn,
    IReadOnlyList<FormSubmissionExportField> Fields);

public sealed record PreparedFormSubmissionExport(
    FormSubmissionExportDefinition Definition,
    IReadOnlyList<FormSubmissionExportField> Fields,
    int UpperSubmissionId,
    FormSubmissionExportOptions Options,
    string FileName,
    string ContentType,
    IReadOnlyList<int>? CurrentViewSubmissionIds = null);

public readonly record struct FormSubmissionExportCell(string Value, bool IsTextual);

public sealed class FormSubmissionExportValidationException(string message) : Exception(message);

public sealed class FormSubmissionExportNotFoundException : Exception;
