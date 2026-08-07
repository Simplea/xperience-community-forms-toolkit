using System.Text;
using System.Xml;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

internal sealed class XmlExportWriter : IFormSubmissionExportWriter
{
    public FormSubmissionExportFormat Format => FormSubmissionExportFormat.Xml;

    public string ContentType => "application/xml; charset=utf-8";

    public async ValueTask<IFormSubmissionExportDocument> OpenAsync(
        Stream output,
        PreparedFormSubmissionExport export,
        CancellationToken cancellationToken)
    {
        var settings = new XmlWriterSettings
        {
            Async = true,
            CloseOutput = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            OmitXmlDeclaration = false,
        };
        var writer = XmlWriter.Create(output, settings);
        await writer.WriteStartDocumentAsync();
        await writer.WriteStartElementAsync(null, "FormSubmissions", null);
        cancellationToken.ThrowIfCancellationRequested();
        return new XmlExportDocument(writer, export.Fields);
    }

    private sealed class XmlExportDocument(
        XmlWriter writer,
        IReadOnlyList<FormSubmissionExportField> fields) : IFormSubmissionExportDocument
    {
        private bool completed;

        public async Task WriteRecordAsync(IReadOnlyList<FormSubmissionExportCell> cells, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteStartElementAsync(null, "Submission", null);
            for (int index = 0; index < fields.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string elementName = XmlConvert.EncodeLocalName(fields[index].Identifier);
                await writer.WriteElementStringAsync(null, elementName, null, cells[index].Value);
            }

            await writer.WriteEndElementAsync();
        }

        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            if (completed)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteEndElementAsync();
            await writer.WriteEndDocumentAsync();
            await writer.FlushAsync();
            completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (!completed)
            {
                await writer.WriteEndElementAsync();
                await writer.WriteEndDocumentAsync();
                await writer.FlushAsync();
            }

            writer.Dispose();
        }
    }
}
