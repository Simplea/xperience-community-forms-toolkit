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
    string? Order);

public sealed record FormSubmissionRemovalRange(
    DateOnly? From,
    DateOnly? To,
    DateTime? FromUtc,
    DateTime? ToExclusiveUtc);

public sealed record FormSubmissionRemovalOptions(
    FormSubmissionRemovalRange Range,
    int? MaximumRecords,
    FormSubmissionRemovalSortDirection SortDirection);

public sealed class FormSubmissionRemovalPreviewResponse
{
    public int? MatchingCount { get; init; }

    public string? Error { get; init; }
}

public sealed class FormSubmissionAdvancedDeleteResponse
{
    public int? DeletedCount { get; init; }

    public string? Error { get; init; }
}

public sealed class FormSubmissionRemovalValidationException(string message) : Exception(message);
