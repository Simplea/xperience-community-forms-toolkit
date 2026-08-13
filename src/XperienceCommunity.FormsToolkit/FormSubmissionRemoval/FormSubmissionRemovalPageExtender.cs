using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using Microsoft.Extensions.Logging;

[assembly: PageExtender(typeof(XperienceCommunity.FormsToolkit.FormSubmissionRemoval.FormSubmissionRemovalPageExtender))]

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

[UIPermission(FormSubmissionRemovalConstants.Permission, "Delete form submissions")]
public sealed class FormSubmissionRemovalPageExtender(
    IUIPermissionEvaluator permissionEvaluator,
    IFormSubmissionRemovalService removalService,
    ILogger<FormSubmissionRemovalPageExtender> logger) : PageExtender<FormSubmissionsTab>
{
    public override async Task ConfigurePage()
    {
        var permission = await permissionEvaluator.Evaluate(FormSubmissionRemovalConstants.Permission);
        if (permission.Succeeded)
        {
            Page.PageConfiguration.MassActions.AddCommandWithConfirmation(
                label: "Delete",
                command: nameof(DeleteSelectedSubmissions),
                confirmation: "Delete the selected submissions?",
                confirmationButton: "Delete",
                confirmationDetail: "This cannot be undone.",
                icon: Icons.Bin,
                title: "Delete selected submissions",
                destructive: true);
        }

        await base.ConfigurePage();
    }

    [PageCommand(Permission = FormSubmissionRemovalConstants.Permission)]
    public async Task<ICommandResponse<MassActionResult>> DeleteSelectedSubmissions(
        IEnumerable<int> identifiers,
        CancellationToken cancellationToken)
    {
        try
        {
            await removalService.DeleteByIdsAsync(Page.FormId, identifiers?.ToList() ?? [], cancellationToken);
            return ResponseFrom(new MassActionResult(reload: true, refetchAll: true));
        }
        catch (FormSubmissionRemovalValidationException exception)
        {
            logger.LogWarning(exception, "Quick delete rejected for form {FormId}: {Message}", Page.FormId, exception.Message);
            return ResponseFrom(new MassActionResult(reload: false, refetchAll: false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not delete the selected submissions for form {FormId}.", Page.FormId);
            return ResponseFrom(new MassActionResult(reload: false, refetchAll: false));
        }
    }
}
