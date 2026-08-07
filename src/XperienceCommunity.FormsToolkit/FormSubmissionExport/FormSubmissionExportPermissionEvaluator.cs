using CMS.Membership.Internal;

using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public interface IFormSubmissionExportPermissionEvaluator
{
    public Task<bool> CanExportAsync();
}

internal sealed class FormSubmissionExportPermissionEvaluator(
    IAuthenticatedUserAccessor authenticatedUserAccessor,
    IApplicationPermissionEvaluator applicationPermissionEvaluator)
    : IFormSubmissionExportPermissionEvaluator
{
    public async Task<bool> CanExportAsync()
    {
        var user = await authenticatedUserAccessor.Get();
        return applicationPermissionEvaluator.Evaluate(new ApplicationPermissionEvaluationContext
        {
            ApplicationName = FormsApplication.IDENTIFIER,
            PermissionName = FormSubmissionExportConstants.Permission,
            User = user,
        });
    }
}
