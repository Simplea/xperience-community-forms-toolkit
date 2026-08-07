using System.IO.Compression;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

internal sealed class ExcelExportWriter : IFormSubmissionExportWriter
{
    internal const int ExcelMaximumRowsPerWorksheet = 1_048_576;
    internal const int ExcelMaximumCellCharacters = 32_767;

    private readonly ILogger<ExcelExportWriter> logger;
    private readonly int maximumRowsPerWorksheet;

    public ExcelExportWriter()
        : this(NullLogger<ExcelExportWriter>.Instance, ExcelMaximumRowsPerWorksheet)
    {
    }

    public ExcelExportWriter(ILogger<ExcelExportWriter> logger)
        : this(logger, ExcelMaximumRowsPerWorksheet)
    {
    }

    internal ExcelExportWriter(int maximumRowsPerWorksheet)
        : this(NullLogger<ExcelExportWriter>.Instance, maximumRowsPerWorksheet)
    {
    }

    private ExcelExportWriter(ILogger<ExcelExportWriter> logger, int maximumRowsPerWorksheet)
    {
        if (maximumRowsPerWorksheet < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRowsPerWorksheet));
        }

        this.logger = logger;
        this.maximumRowsPerWorksheet = maximumRowsPerWorksheet;
    }

    public FormSubmissionExportFormat Format => FormSubmissionExportFormat.Excel;

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async ValueTask<IFormSubmissionExportDocument> OpenAsync(
        Stream output,
        PreparedFormSubmissionExport export,
        CancellationToken cancellationToken)
    {
        var document = new ExcelExportDocument(output, export, logger, maximumRowsPerWorksheet);
        await document.InitializeAsync(cancellationToken);
        return document;
    }

    private sealed class ExcelExportDocument(
        Stream output,
        PreparedFormSubmissionExport export,
        ILogger<ExcelExportWriter> logger,
        int maximumRowsPerWorksheet) : IFormSubmissionExportDocument
    {
        private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string OfficeRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";

        private readonly ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true);
        private XmlWriter? sheetWriter;
        private int sheetCount;
        private int currentRow;
        private bool completed;

        public async Task InitializeAsync(CancellationToken cancellationToken) => await StartWorksheetAsync(cancellationToken);

        public async Task WriteRecordAsync(IReadOnlyList<FormSubmissionExportCell> cells, CancellationToken cancellationToken)
        {
            if (currentRow >= maximumRowsPerWorksheet)
            {
                await CloseWorksheetAsync(cancellationToken);
                await StartWorksheetAsync(cancellationToken);
            }

            await WriteRowAsync(cells, isHeader: false, cancellationToken);
        }

        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            if (completed)
            {
                return;
            }

            await CloseWorksheetAsync(cancellationToken);
            await WriteContentTypesAsync(cancellationToken);
            await WriteRootRelationshipsAsync(cancellationToken);
            await WriteWorkbookAsync(cancellationToken);
            await WriteWorkbookRelationshipsAsync(cancellationToken);
            archive.Dispose();
            completed = true;
        }

        public ValueTask DisposeAsync()
        {
            sheetWriter?.Dispose();
            if (!completed)
            {
                archive.Dispose();
            }

            return ValueTask.CompletedTask;
        }

        private async Task StartWorksheetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sheetCount++;
            currentRow = 0;

            var entry = archive.CreateEntry($"xl/worksheets/sheet{sheetCount}.xml", CompressionLevel.Fastest);
            sheetWriter = XmlWriter.Create(entry.Open(), CreateXmlSettings());
            await sheetWriter.WriteStartDocumentAsync();
            await sheetWriter.WriteStartElementAsync(null, "worksheet", SpreadsheetNamespace);
            await sheetWriter.WriteStartElementAsync(null, "sheetData", SpreadsheetNamespace);

            if (export.Options.IncludeHeader)
            {
                await WriteRowAsync(
                    export.Fields.Select(field => new FormSubmissionExportCell(field.Caption, true)).ToList(),
                    isHeader: true,
                    cancellationToken);
            }
        }

        private async Task CloseWorksheetAsync(CancellationToken cancellationToken)
        {
            if (sheetWriter is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await sheetWriter.WriteEndElementAsync();
            await sheetWriter.WriteEndElementAsync();
            await sheetWriter.WriteEndDocumentAsync();
            await sheetWriter.FlushAsync();
            sheetWriter.Dispose();
            sheetWriter = null;
        }

        private async Task WriteRowAsync(
            IReadOnlyList<FormSubmissionExportCell> cells,
            bool isHeader,
            CancellationToken cancellationToken)
        {
            currentRow++;
            await sheetWriter!.WriteStartElementAsync(null, "row", SpreadsheetNamespace);
            await sheetWriter.WriteAttributeStringAsync(null, "r", null, currentRow.ToString(System.Globalization.CultureInfo.InvariantCulture));

            for (int index = 0; index < cells.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string originalValue = cells[index].Value;
                string value = TruncateForExcel(originalValue);
                if (!isHeader && value.Length != originalValue.Length)
                {
                    string fieldId = index < export.Fields.Count ? export.Fields[index].Identifier : $"column{index + 1}";
                    logger.LogWarning(
                        "Excel export truncated a cell for form {FormId}, field {FieldId}, worksheet {Worksheet}, row {Row} to Excel's character limit.",
                        export.Definition.FormId,
                        fieldId,
                        sheetCount,
                        currentRow);
                }

                string cellReference = GetColumnName(index + 1) + currentRow.ToString(System.Globalization.CultureInfo.InvariantCulture);

                await sheetWriter.WriteStartElementAsync(null, "c", SpreadsheetNamespace);
                await sheetWriter.WriteAttributeStringAsync(null, "r", null, cellReference);
                await sheetWriter.WriteAttributeStringAsync(null, "t", null, "inlineStr");
                await sheetWriter.WriteStartElementAsync(null, "is", SpreadsheetNamespace);
                await sheetWriter.WriteStartElementAsync(null, "t", SpreadsheetNamespace);
                if (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])))
                {
                    await sheetWriter.WriteAttributeStringAsync("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                }

                await sheetWriter.WriteStringAsync(value);
                await sheetWriter.WriteEndElementAsync();
                await sheetWriter.WriteEndElementAsync();
                await sheetWriter.WriteEndElementAsync();
            }

            await sheetWriter.WriteEndElementAsync();
        }

        private async Task WriteContentTypesAsync(CancellationToken cancellationToken)
        {
            await WriteEntryAsync("[Content_Types].xml", async writer =>
            {
                await writer.WriteStartElementAsync(null, "Types", ContentTypesNamespace);
                await WriteElementWithAttributesAsync(writer, "Default", ContentTypesNamespace, ("Extension", "rels"), ("ContentType", "application/vnd.openxmlformats-package.relationships+xml"));
                await WriteElementWithAttributesAsync(writer, "Default", ContentTypesNamespace, ("Extension", "xml"), ("ContentType", "application/xml"));
                await WriteElementWithAttributesAsync(writer, "Override", ContentTypesNamespace, ("PartName", "/xl/workbook.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"));
                for (int index = 1; index <= sheetCount; index++)
                {
                    await WriteElementWithAttributesAsync(writer, "Override", ContentTypesNamespace, ("PartName", $"/xl/worksheets/sheet{index}.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"));
                }

                await writer.WriteEndElementAsync();
            }, cancellationToken);
        }

        private Task WriteRootRelationshipsAsync(CancellationToken cancellationToken) => WriteEntryAsync("_rels/.rels", async writer =>
        {
            await writer.WriteStartElementAsync(null, "Relationships", PackageRelationshipsNamespace);
            await WriteElementWithAttributesAsync(writer, "Relationship", PackageRelationshipsNamespace,
                ("Id", "rId1"),
                ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                ("Target", "xl/workbook.xml"));
            await writer.WriteEndElementAsync();
        }, cancellationToken);

        private Task WriteWorkbookAsync(CancellationToken cancellationToken) => WriteEntryAsync("xl/workbook.xml", async writer =>
        {
            await writer.WriteStartElementAsync(null, "workbook", SpreadsheetNamespace);
            await writer.WriteAttributeStringAsync("xmlns", "r", null, OfficeRelationshipsNamespace);
            await writer.WriteStartElementAsync(null, "sheets", SpreadsheetNamespace);
            for (int index = 1; index <= sheetCount; index++)
            {
                await writer.WriteStartElementAsync(null, "sheet", SpreadsheetNamespace);
                await writer.WriteAttributeStringAsync(null, "name", null, index == 1 ? "Submissions" : $"Submissions {index}");
                await writer.WriteAttributeStringAsync(null, "sheetId", null, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteAttributeStringAsync("r", "id", OfficeRelationshipsNamespace, $"rId{index}");
                await writer.WriteEndElementAsync();
            }

            await writer.WriteEndElementAsync();
            await writer.WriteEndElementAsync();
        }, cancellationToken);

        private Task WriteWorkbookRelationshipsAsync(CancellationToken cancellationToken) => WriteEntryAsync("xl/_rels/workbook.xml.rels", async writer =>
        {
            await writer.WriteStartElementAsync(null, "Relationships", PackageRelationshipsNamespace);
            for (int index = 1; index <= sheetCount; index++)
            {
                await WriteElementWithAttributesAsync(writer, "Relationship", PackageRelationshipsNamespace,
                    ("Id", $"rId{index}"),
                    ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    ("Target", $"worksheets/sheet{index}.xml"));
            }

            await writer.WriteEndElementAsync();
        }, cancellationToken);

        private async Task WriteEntryAsync(string name, Func<XmlWriter, Task> writeContent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
            await using var stream = entry.Open();
            using var writer = XmlWriter.Create(stream, CreateXmlSettings());
            await writer.WriteStartDocumentAsync();
            await writeContent(writer);
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();
        }

        private static async Task WriteElementWithAttributesAsync(
            XmlWriter writer,
            string name,
            string xmlNamespace,
            params (string Name, string Value)[] attributes)
        {
            await writer.WriteStartElementAsync(null, name, xmlNamespace);
            foreach (var attribute in attributes)
            {
                await writer.WriteAttributeStringAsync(null, attribute.Name, null, attribute.Value);
            }

            await writer.WriteEndElementAsync();
        }

        private static XmlWriterSettings CreateXmlSettings() => new()
        {
            Async = true,
            CloseOutput = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            OmitXmlDeclaration = false,
        };

        internal static string TruncateForExcel(string value)
        {
            if (value.Length <= ExcelMaximumCellCharacters)
            {
                return value;
            }

            int length = ExcelMaximumCellCharacters;
            if (char.IsHighSurrogate(value[length - 1]))
            {
                length--;
            }

            return value[..length];
        }

        private static string GetColumnName(int columnNumber)
        {
            var result = new StringBuilder();
            while (columnNumber > 0)
            {
                columnNumber--;
                result.Insert(0, (char)('A' + (columnNumber % 26)));
                columnNumber /= 26;
            }

            return result.ToString();
        }
    }
}
