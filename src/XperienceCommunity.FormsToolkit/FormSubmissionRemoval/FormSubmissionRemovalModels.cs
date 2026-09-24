namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

public enum FormSubmissionRemovalSortDirection
{
    Ascending,
    Descending,
}

public sealed record FormSubmissionRemovalRangeRequest(
    string? From,
    string? To,
    string? TimeZone,
    string? NumberOfRecords,
    string? Order)
{
    /// <summary>
    /// The boundary returned by the preview this request was confirmed against. When set, only
    /// submissions that existed at preview time can be deleted.
    /// </summary>
    public int? UpperSubmissionId { get; init; }
}

public sealed record FormSubmissionRemovalRange(
    DateOnly? From,
    DateOnly? To,
    DateTime? FromUtc,
    DateTime? ToExclusiveUtc);

public sealed record FormSubmissionRemovalOptions(
    FormSubmissionRemovalRange Range,
    int? MaximumRecords,
    FormSubmissionRemovalSortDirection SortDirection)
{
    /// <summary>
    /// The highest submission ID to match. <see langword="null"/> matches up to the newest submission
    /// at the time the operation runs.
    /// </summary>
    public int? UpperSubmissionId { get; init; }
}

public sealed class FormSubmissionRemovalPreviewResponse
{
    public int? MatchingCount { get; init; }

    /// <summary>
    /// The highest submission ID the preview counted. Send it back with the export and delete requests
    /// so they act on the same submissions the preview reported.
    /// </summary>
    public int? UpperSubmissionId { get; init; }

    public string? Error { get; init; }
}

public sealed class FormSubmissionAdvancedDeleteResponse
{
    public int? DeletedCount { get; init; }

    public string? Error { get; init; }
}

public sealed class FormSubmissionRemovalValidationException(string message) : Exception(message);
