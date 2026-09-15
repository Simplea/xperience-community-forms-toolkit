using CMS.Base;

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

internal sealed class UploadedFilePhysicalStore : IUploadedFilePhysicalStore
{
    // Xperience by Kentico's documented default storage location for form file uploads,
    // confirmed against a running instance: <web app physical root>\assets\BizFormFiles.
    // CMS.IO.File.Delete did not resolve the "~/assets/bizformfiles/" virtual form directly
    // (it appears to expect an already-resolved path and silently no-ops otherwise), so the
    // physical path is built explicitly from the web application's physical root instead.
    private static readonly string bizFormFilesPhysicalFolder =
        CMS.IO.Path.Combine(SystemContext.WebApplicationPhysicalPath, "assets", "BizFormFiles");

    public bool Exists(string systemFileName) => CMS.IO.File.Exists(BuildPath(systemFileName));

    public void Delete(string systemFileName) => CMS.IO.File.Delete(BuildPath(systemFileName));

    private static string BuildPath(string systemFileName) =>
        CMS.IO.Path.Combine(bizFormFilesPhysicalFolder, systemFileName);
}
