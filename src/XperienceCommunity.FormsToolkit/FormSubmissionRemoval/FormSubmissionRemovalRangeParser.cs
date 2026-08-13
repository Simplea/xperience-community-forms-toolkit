using System.Globalization;

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

public static class FormSubmissionRemovalRangeParser
{
    public static FormSubmissionRemovalOptions Parse(FormSubmissionRemovalRangeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var range = ParseRange(request);
        int? maximumRecords = ParseMaximumRecords(request.NumberOfRecords);
        var sortDirection = ParseOrder(request.Order);

        return new FormSubmissionRemovalOptions(range, maximumRecords, sortDirection);
    }

    private static FormSubmissionRemovalRange ParseRange(FormSubmissionRemovalRangeRequest request)
    {
        var from = ParseDate(request.From, "From");
        var to = ParseDate(request.To, "To");

        if (from > to)
        {
            throw new FormSubmissionRemovalValidationException("From must be on or before To.");
        }

        if (from is null && to is null)
        {
            return new FormSubmissionRemovalRange(null, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone))
        {
            throw new FormSubmissionRemovalValidationException("A valid administration time zone is required when a date is supplied.");
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new FormSubmissionRemovalValidationException("The administration time zone is invalid.");
        }

        DateTime? fromUtc = from is null ? null : ToUtc(from.Value, timeZone);
        DateTime? toExclusiveUtc = to is null ? null : ToUtc(to.Value.AddDays(1), timeZone);

        return new FormSubmissionRemovalRange(from, to, fromUtc, toExclusiveUtc);
    }

    private static DateOnly? ParseDate(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormSubmissionRemovalValidationException($"{label} must be a valid date in yyyy-MM-dd format.");
        }

        return date;
    }

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            throw new FormSubmissionRemovalValidationException("The selected date does not have a valid start in the administration time zone.");
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    private static int? ParseMaximumRecords(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result) || result <= 0)
        {
            throw new FormSubmissionRemovalValidationException("Number of records must be a positive whole number.");
        }

        return result;
    }

    private static FormSubmissionRemovalSortDirection ParseOrder(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "ascending" => FormSubmissionRemovalSortDirection.Ascending,
            "descending" => FormSubmissionRemovalSortDirection.Descending,
            _ => throw new FormSubmissionRemovalValidationException("The ordering is invalid."),
        };
}
