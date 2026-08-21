using XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionRemovalRangeParserTests
{
    private static FormSubmissionRemovalRangeRequest CreateRequest(
        string? from = null,
        string? to = null,
        string? timeZone = null,
        string? numberOfRecords = null,
        string order = "ascending") =>
        new(from, to, timeZone, numberOfRecords, order);

    [Test]
    public void EmptyRangeHasNoBounds()
    {
        var result = FormSubmissionRemovalRangeParser.Parse(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.Range.FromUtc, Is.Null);
            Assert.That(result.Range.ToExclusiveUtc, Is.Null);
        });
    }

    [Test]
    public void ConvertsInclusiveDatesToUtcExclusiveUpperBound()
    {
        var result = FormSubmissionRemovalRangeParser.Parse(
            CreateRequest(from: "2026-08-06", to: "2026-08-06", timeZone: "America/Bogota"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Range.FromUtc, Is.EqualTo(new DateTime(2026, 8, 6, 5, 0, 0, DateTimeKind.Utc)));
            Assert.That(result.Range.ToExclusiveUtc, Is.EqualTo(new DateTime(2026, 8, 7, 5, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void RejectsReversedRange()
    {
        var exception = Assert.Throws<FormSubmissionRemovalValidationException>(
            () => FormSubmissionRemovalRangeParser.Parse(CreateRequest(from: "2026-08-07", to: "2026-08-06", timeZone: "UTC")))!;

        Assert.That(exception.Message, Is.EqualTo("From must be on or before To."));
    }

    [TestCase("not-a-date")]
    [TestCase("2026/08/06")]
    public void RejectsMalformedDates(string value) => Assert.Throws<FormSubmissionRemovalValidationException>(
        () => FormSubmissionRemovalRangeParser.Parse(CreateRequest(from: value, timeZone: "UTC")));

    [Test]
    public void RequiresTimeZoneWhenADateIsSupplied() => Assert.Throws<FormSubmissionRemovalValidationException>(
        () => FormSubmissionRemovalRangeParser.Parse(CreateRequest(from: "2026-08-06")));

    [TestCase("0")]
    [TestCase("-5")]
    [TestCase("3.5")]
    [TestCase("abc")]
    public void RejectsInvalidNumberOfRecords(string value) => Assert.Throws<FormSubmissionRemovalValidationException>(
        () => FormSubmissionRemovalRangeParser.Parse(CreateRequest(numberOfRecords: value)));

    [Test]
    public void AllowsAnEmptyNumberOfRecords()
    {
        var result = FormSubmissionRemovalRangeParser.Parse(CreateRequest());

        Assert.That(result.MaximumRecords, Is.Null);
    }

    [TestCase("25", 25)]
    [TestCase("1", 1)]
    public void ParsesAPositiveNumberOfRecords(string value, int expected)
    {
        var result = FormSubmissionRemovalRangeParser.Parse(CreateRequest(numberOfRecords: value));

        Assert.That(result.MaximumRecords, Is.EqualTo(expected));
    }

    [TestCase("ascending", FormSubmissionRemovalSortDirection.Ascending)]
    [TestCase("descending", FormSubmissionRemovalSortDirection.Descending)]
    public void ParsesSupportedOrdering(string value, FormSubmissionRemovalSortDirection expected)
    {
        var result = FormSubmissionRemovalRangeParser.Parse(CreateRequest(order: value));

        Assert.That(result.SortDirection, Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("sideways")]
    public void RejectsInvalidOrdering(string? value) => Assert.Throws<FormSubmissionRemovalValidationException>(
        () => FormSubmissionRemovalRangeParser.Parse(CreateRequest(order: value!)));
}
