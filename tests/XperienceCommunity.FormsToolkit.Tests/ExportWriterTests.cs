using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class ExportWriterTests
{
    [Test]
    public async Task CsvWritesBomSelectedDelimiterEscapingAndCrlf()
    {
        var export = ExportTestData.CreatePrepared(FormSubmissionExportFormat.Csv, includeHeader: true, delimiter: ';');
        await using var stream = new MemoryStream();
        var writer = new CsvExportWriter();
        await using var document = await writer.OpenAsync(stream, export, CancellationToken.None);

        await document.WriteRecordAsync(
            [new("plain", true), new("semi;value", true), new("say \"hello\"", true)],
            CancellationToken.None);
        await document.CompleteAsync(CancellationToken.None);

        byte[] bytes = stream.ToArray();
        Assert.That(bytes.Take(3), Is.EqualTo(Encoding.UTF8.Preamble.ToArray()));
        Assert.That(
            Encoding.UTF8.GetString(bytes[3..]),
            Is.EqualTo("Submission ID;Submitted;Name\r\nplain;\"semi;value\";\"say \"\"hello\"\"\"\r\n"));
    }

    [TestCase("=1+1", "'=1+1")]
    [TestCase("  +SUM(A1)", "'  +SUM(A1)")]
    [TestCase("\t@command", "'\t@command")]
    [TestCase("safe", "safe")]
    public void ProtectsTextFromSpreadsheetFormulaInjection(string input, string expected) =>
        Assert.That(FormSubmissionExportValueFormatter.ProtectFromFormulaInjection(input), Is.EqualTo(expected));

    [Test]
    public async Task XmlWritesStableElementNamesAndEscapedValues()
    {
        var export = ExportTestData.CreatePrepared(FormSubmissionExportFormat.Xml, includeHeader: false);
        await using var stream = new MemoryStream();
        var writer = new XmlExportWriter();
        await using var document = await writer.OpenAsync(stream, export, CancellationToken.None);

        await document.WriteRecordAsync(
            [new("7", false), new("2026-08-06T12:00:00.0000000Z", false), new("A&B <test>", true)],
            CancellationToken.None);
        await document.CompleteAsync(CancellationToken.None);

        var xml = XDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        var submission = xml.Root!.Element("Submission")!;
        Assert.Multiple(() =>
        {
            Assert.That(xml.Root.Name.LocalName, Is.EqualTo("FormSubmissions"));
            Assert.That(submission.Element("__submissionId")?.Value, Is.EqualTo("7"));
            Assert.That(submission.Element("Name")?.Value, Is.EqualTo("A&B <test>"));
        });
    }

    [Test]
    public async Task ExcelProducesAValidStreamingPackageAndRollsWorksheets()
    {
        var export = ExportTestData.CreatePrepared(FormSubmissionExportFormat.Excel, includeHeader: true);
        await using var stream = new MemoryStream();
        var writer = new ExcelExportWriter(maximumRowsPerWorksheet: 3);
        await using var document = await writer.OpenAsync(stream, export, CancellationToken.None);

        for (int index = 1; index <= 3; index++)
        {
            await document.WriteRecordAsync(
                [new(index.ToString(), false), new("2026-08-06T12:00:00.0000000Z", false), new($"Name {index}", true)],
                CancellationToken.None);
        }

        await document.CompleteAsync(CancellationToken.None);
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        Assert.Multiple(() =>
        {
            Assert.That(archive.GetEntry("[Content_Types].xml"), Is.Not.Null);
            Assert.That(archive.GetEntry("xl/workbook.xml"), Is.Not.Null);
            Assert.That(archive.GetEntry("xl/worksheets/sheet1.xml"), Is.Not.Null);
            Assert.That(archive.GetEntry("xl/worksheets/sheet2.xml"), Is.Not.Null);
        });

        using var workbookReader = new StreamReader(archive.GetEntry("xl/workbook.xml")!.Open());
        string workbookXml = await workbookReader.ReadToEndAsync();
        Assert.That(workbookXml, Does.Contain("Submissions 2"));
    }

    [Test]
    public async Task ExcelCanWriteToANonSeekableResponseStream()
    {
        var export = ExportTestData.CreatePrepared(FormSubmissionExportFormat.Excel, includeHeader: true);
        await using var underlying = new MemoryStream();
        await using var responseStream = new NonSeekableWriteStream(underlying);
        var writer = new ExcelExportWriter();
        await using var document = await writer.OpenAsync(responseStream, export, CancellationToken.None);

        await document.WriteRecordAsync([new("1", false), new("date", false), new("safe", true)], CancellationToken.None);
        await document.CompleteAsync(CancellationToken.None);

        Assert.That(underlying.Length, Is.GreaterThan(0));
    }

    [Test]
    public void ExcelTruncationDoesNotSplitASurrogatePair()
    {
        string value = new string('a', ExcelExportWriter.ExcelMaximumCellCharacters - 1) + "😀tail";
        string result = InvokeExcelTruncation(value);

        Assert.Multiple(() =>
        {
            Assert.That(result.Length, Is.EqualTo(ExcelExportWriter.ExcelMaximumCellCharacters - 1));
            Assert.That(char.IsHighSurrogate(result[^1]), Is.False);
        });
    }

    private static string InvokeExcelTruncation(string value)
    {
        var nested = typeof(ExcelExportWriter).GetNestedType("ExcelExportDocument", System.Reflection.BindingFlags.NonPublic)!;
        var method = nested.GetMethod("TruncateForExcel", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        return (string)method.Invoke(null, [value])!;
    }

    private sealed class NonSeekableWriteStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => inner.WriteAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
        }
    }
}

internal static class ExportTestData
{
    public static PreparedFormSubmissionExport CreatePrepared(
        FormSubmissionExportFormat format,
        bool includeHeader,
        char delimiter = ',')
    {
        FormSubmissionExportField[] fields =
        [
            new(FormSubmissionExportFieldIdentifiers.SubmissionId, "ContactUsID", "Submission ID", false, FormSubmissionExportFieldKind.SubmissionId),
            new(FormSubmissionExportFieldIdentifiers.Submitted, "FormInserted", "Submitted", false, FormSubmissionExportFieldKind.Submitted),
            new("Name", "Name", "Name", false, FormSubmissionExportFieldKind.FormField),
        ];
        var definition = new FormSubmissionExportDefinition(1, "ContactUs", "Form.ContactUs", "ContactUsID", fields);
        var options = new FormSubmissionExportOptions(
            format,
            FormSubmissionExportOperation.Export,
            new FormSubmissionExportRange(null, null, null, null),
            null,
            null,
            includeHeader,
            delimiter,
            FormSubmissionExportSortDirection.Ascending,
            fields.Select(field => field.Identifier).ToList());

        return new PreparedFormSubmissionExport(
            definition,
            fields,
            0,
            options,
            "ContactUs-submissions-20260806-120000Z." + format.ToString().ToLowerInvariant(),
            "application/octet-stream");
    }
}
