using System.Globalization;

using CMS.OnlineForms;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public static class UploadedFileName
{
    public static string Extract(object? value)
    {
        if (value is BizFormUploadFile uploadedFile)
        {
            return GetBaseName(uploadedFile.OriginalFileName);
        }

        var (_, originalFileName) = ParseRawValue(value);
        return originalFileName ?? string.Empty;
    }

    public static string ExtractSystemFileName(object? value)
    {
        if (value is BizFormUploadFile uploadedFile)
        {
            return GetBaseName(uploadedFile.SystemFileName);
        }

        var (systemFileName, _) = ParseRawValue(value);
        return systemFileName ?? string.Empty;
    }

    /// <summary>
    /// The File Uploader form component (identifier "Kentico.FileUploader") stores its raw column
    /// value as a single string in the form "{systemFileName}/{originalFileName}" — for example,
    /// "9fa205b3-caf8-4501-9b93-ca3fac72834d.pdf/report.pdf" — rather than as a strongly typed
    /// BizFormUploadFile object. Confirmed directly against the underlying database column; the
    /// system file name segment is what the file is actually stored under on disk.
    /// </summary>
    private static (string? SystemFileName, string? OriginalFileName) ParseRawValue(object? value)
    {
        string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.Length == 0)
        {
            return (null, null);
        }

        int separator = text.IndexOf('/');
        if (separator < 0)
        {
            string baseName = GetBaseName(text);
            return (baseName, baseName);
        }

        return (GetBaseName(text[..separator]), GetBaseName(text[(separator + 1)..]));
    }

    private static string GetBaseName(string? value)
    {
        string text = value ?? string.Empty;
        text = text.Replace('\\', '/');
        int separator = text.LastIndexOf('/');
        return separator >= 0 ? text[(separator + 1)..] : text;
    }
}
