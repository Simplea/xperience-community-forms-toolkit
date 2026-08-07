using System.Globalization;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public static class FormSubmissionExportRangeParser
{
    public static FormSubmissionExportRange Parse(FormSubmissionExportCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var from = ParseDate(request.From, "From");
        var to = ParseDate(request.To, "To");

        if (from > to)
        {
            throw new FormSubmissionExportValidationException("From must be on or before To.");
        }

        if (from is null && to is null)
        {
            return new FormSubmissionExportRange(null, null, null, null);
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone))
        {
            throw new FormSubmissionExportValidationException("A valid administration time zone is required when a date is supplied.");
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new FormSubmissionExportValidationException("The administration time zone is invalid.");
        }

        DateTime? fromUtc = from is null ? null : ToUtc(from.Value, timeZone);
        DateTime? toExclusiveUtc = to is null ? null : ToUtc(to.Value.AddDays(1), timeZone);

        return new FormSubmissionExportRange(from, to, fromUtc, toExclusiveUtc);
    }

    private static DateOnly? ParseDate(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormSubmissionExportValidationException($"{label} must be a valid date in yyyy-MM-dd format.");
        }

        return date;
    }

    private static DateTime ToUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            throw new FormSubmissionExportValidationException("The selected date does not have a valid start in the administration time zone.");
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
