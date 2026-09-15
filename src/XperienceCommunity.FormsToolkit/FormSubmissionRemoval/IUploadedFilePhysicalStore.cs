namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

/// <summary>
/// Resolves and manipulates an uploaded form file's physical location on disk, isolated from
/// <see cref="FormSubmissionRemovalService"/> so its file-cleanup decision logic can be exercised
/// without a live Xperience installation.
/// </summary>
internal interface IUploadedFilePhysicalStore
{
    public bool Exists(string systemFileName);

    public void Delete(string systemFileName);
}
