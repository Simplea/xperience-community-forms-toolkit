using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionExportOptionsParserTests
{
    private static readonly IReadOnlyList<FormSubmissionExportField> fields =
    [
        new(FormSubmissionExportFieldIdentifiers.SubmissionId, "ContactUsID", "Submission ID", false, FormSubmissionExportFieldKind.SubmissionId),
        new(FormSubmissionExportFieldIdentifiers.Submitted, "FormInserted", "Submitted", false, FormSubmissionExportFieldKind.Submitted),
        new("Name", "Name", "Name", false, FormSubmissionExportFieldKind.FormField),
    ];

    [Test]
    public void EmptyRangeHasNoBounds()
    {
        var result = FormSubmissionExportRangeParser.Parse(CreateRequest());

        Assert.Multiple(() =>
        {
            Assert.That(result.FromUtc, Is.Null);
            Assert.That(result.ToExclusiveUtc, Is.Null);
        });
    }

    [Test]
    public void ConvertsInclusiveDatesToUtcExclusiveUpperBound()
    {
        var result = FormSubmissionExportRangeParser.Parse(
            CreateRequest(from: "2026-08-06", to: "2026-08-06", timeZone: "America/Bogota"));

        Assert.Multiple(() =>
        {
            Assert.That(result.FromUtc, Is.EqualTo(new DateTime(2026, 8, 6, 5, 0, 0, DateTimeKind.Utc)));
            Assert.That(result.ToExclusiveUtc, Is.EqualTo(new DateTime(2026, 8, 7, 5, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public void RejectsReversedRange()
    {
        var exception = Assert.Throws<FormSubmissionExportValidationException>(
            () => FormSubmissionExportRangeParser.Parse(CreateRequest(from: "2026-08-07", to: "2026-08-06", timeZone: "UTC")))!;

        Assert.That(exception.Message, Is.EqualTo("From must be on or before To."));
    }

    [TestCase(null, null, null)]
    [TestCase("25", FormSubmissionExportOperation.Export, 25)]
    [TestCase("25", FormSubmissionExportOperation.Preview, 25)]
    [TestCase("500", FormSubmissionExportOperation.Preview, 100)]
    public void CalculatesEffectiveRecordLimit(string? requested, FormSubmissionExportOperation? operation, int? expected)
    {
        string operationName = (operation ?? FormSubmissionExportOperation.Export).ToString();
        var result = FormSubmissionExportOptionsParser.Parse(
            CreateRequest(operation: operationName, numberOfRecords: requested),
            fields);

        Assert.That(result.EffectiveMaximumRecords, Is.EqualTo(expected));
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("1.5")]
    [TestCase("2147483648")]
    [TestCase("invalid")]
    public void RejectsInvalidRecordLimits(string value) =>
        Assert.Throws<FormSubmissionExportValidationException>(() =>
            FormSubmissionExportOptionsParser.Parse(CreateRequest(numberOfRecords: value), fields));

    [Test]
    public void RejectsUnknownOrDuplicateColumns() =>
        Assert.Multiple(() =>
        {
            Assert.Throws<FormSubmissionExportValidationException>(() =>
                FormSubmissionExportOptionsParser.Parse(CreateRequest(columns: ["Unknown"]), fields));
            Assert.Throws<FormSubmissionExportValidationException>(() =>
                FormSubmissionExportOptionsParser.Parse(CreateRequest(columns: ["Name", "Name"]), fields));
            Assert.Throws<FormSubmissionExportValidationException>(() =>
                FormSubmissionExportOptionsParser.Parse(CreateRequest(columns: []), fields));
        });

    [Test]
    public void XmlIgnoresHeaderAndCsvDelimiterOptions()
    {
        var result = FormSubmissionExportOptionsParser.Parse(
            CreateRequest(format: "xml", includeHeader: true, delimiter: "invalid"),
            fields);

        Assert.Multiple(() =>
        {
            Assert.That(result.IncludeHeader, Is.False);
            Assert.That(result.CsvDelimiter, Is.EqualTo(','));
        });
    }

    internal static FormSubmissionExportCommandRequest CreateRequest(
        string format = "csv",
        string operation = "export",
        string? from = null,
        string? to = null,
        string? timeZone = null,
        string? numberOfRecords = null,
        bool? includeHeader = true,
        string? delimiter = "comma",
        string order = "ascending",
        IReadOnlyList<string>? columns = null) =>
        new(format, operation, from, to, timeZone, numberOfRecords, includeHeader, delimiter, order, columns);
}
