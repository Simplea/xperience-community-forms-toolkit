using System.Text;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

internal sealed class CsvExportWriter : IFormSubmissionExportWriter
{
    public FormSubmissionExportFormat Format => FormSubmissionExportFormat.Csv;

    public string ContentType => "text/csv; charset=utf-8";

    public async ValueTask<IFormSubmissionExportDocument> OpenAsync(
        Stream output,
        PreparedFormSubmissionExport export,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        await output.WriteAsync(Encoding.UTF8.Preamble.ToArray(), cancellationToken);

        var document = new CsvExportDocument(output, export.Options.CsvDelimiter);
        if (export.Options.IncludeHeader)
        {
            await document.WriteRecordAsync(
                export.Fields.Select(field => new FormSubmissionExportCell(field.Caption, true)).ToList(),
                cancellationToken);
        }

        return document;
    }

    private sealed class CsvExportDocument(Stream output, char delimiter) : IFormSubmissionExportDocument
    {
        private readonly StreamWriter writer = new(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
        private bool completed;

        public async Task WriteRecordAsync(IReadOnlyList<FormSubmissionExportCell> cells, CancellationToken cancellationToken)
        {
            for (int index = 0; index < cells.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (index > 0)
                {
                    await writer.WriteAsync(delimiter);
                }

                string value = cells[index].IsTextual
                    ? FormSubmissionExportValueFormatter.ProtectFromFormulaInjection(cells[index].Value)
                    : cells[index].Value;
                await writer.WriteAsync(Escape(value, delimiter).AsMemory(), cancellationToken);
            }

            await writer.WriteAsync("\r\n".AsMemory(), cancellationToken);
        }

        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            if (completed)
            {
                return;
            }

            await writer.FlushAsync(cancellationToken);
            completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!completed)
            {
                await writer.FlushAsync();
            }

            await writer.DisposeAsync();
        }

        internal static string Escape(string value, char delimiter) => value.IndexOfAny([delimiter, '"', '\r', '\n']) < 0
            ? value
            : '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}
