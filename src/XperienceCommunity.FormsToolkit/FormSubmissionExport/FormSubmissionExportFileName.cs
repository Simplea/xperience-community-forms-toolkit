using System.Globalization;
using System.Text;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public static class FormSubmissionExportFileName
{
    public static string Create(
        string formCodeName,
        FormSubmissionExportFormat format,
        bool preview,
        DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formCodeName);

        var safeName = new StringBuilder(formCodeName.Length);
        foreach (char character in formCodeName)
        {
            safeName.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        string sanitized = safeName.ToString().Trim('-');
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "form";
        }

        string extension = format switch
        {
            FormSubmissionExportFormat.Excel => "xlsx",
            FormSubmissionExportFormat.Csv => "csv",
            FormSubmissionExportFormat.Xml => "xml",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
        string previewSegment = preview ? "preview-" : string.Empty;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{sanitized}-submissions-{previewSegment}{timestamp.UtcDateTime:yyyyMMdd-HHmmss}Z.{extension}");
    }
}
