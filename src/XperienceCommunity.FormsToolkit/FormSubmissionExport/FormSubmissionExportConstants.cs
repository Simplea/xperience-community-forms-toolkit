namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public static class FormSubmissionExportConstants
{
    public const int PreviewRecordLimit = 100;
    public const string Permission = "XperienceCommunity.FormsToolkit.ExportSubmissions";
    public const string ControllerRoute = "xperience-community/forms-toolkit/form-submissions";
    public const string DownloadRoute = "/" + ControllerRoute + "/export";
    public const string CurrentViewDownloadRoute = "/" + ControllerRoute + "/current-view";

    internal const string ClientComponent = "@xperience-community/forms-toolkit/FormSubmissionExport";
    internal const string CreateTokenCommand = "CreateExportToken";
}
