using Kentico.Xperience.Admin.Base.Authentication;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public interface IFormSubmissionExportUserAccessor
{
    public Task<int> GetUserIdAsync();
}

internal sealed class FormSubmissionExportUserAccessor(IAuthenticatedUserAccessor accessor)
    : IFormSubmissionExportUserAccessor
{
    public async Task<int> GetUserIdAsync() => (await accessor.Get()).UserID;
}
