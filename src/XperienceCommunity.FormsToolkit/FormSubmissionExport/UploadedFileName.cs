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

        string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return GetBaseName(text);
    }

    private static string GetBaseName(string? value)
    {
        string text = value ?? string.Empty;
        text = text.Replace('\\', '/');
        int separator = text.LastIndexOf('/');
        return separator >= 0 ? text[(separator + 1)..] : text;
    }
}
