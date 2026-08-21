using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using Microsoft.Extensions.Logging;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

[assembly: PageExtender(typeof(XperienceCommunity.FormsToolkit.FormSubmissionRemoval.FormSubmissionRemovalPageExtender))]

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

[UIPermission(FormSubmissionRemovalConstants.Permission, "Delete form submissions")]
public sealed class FormSubmissionRemovalPageExtender(
    IUIPermissionEvaluator permissionEvaluator,
    IFormSubmissionExportService exportService,
    IFormSubmissionRemovalService removalService,
    ILogger<FormSubmissionRemovalPageExtender> logger) : PageExtender<FormSubmissionsTab>
{
    public override async Task ConfigurePage()
    {
        var permission = await permissionEvaluator.Evaluate(FormSubmissionRemovalConstants.Permission);
        if (permission.Succeeded)
        {
            Page.PageConfiguration.MassActions.AddCommandWithConfirmation(
                label: "Delete selected",
                command: nameof(DeleteSelectedSubmissions),
                confirmation: "Delete the selected submissions?",
                confirmationButton: "Delete",
                confirmationDetail: "This cannot be undone.",
                icon: Icons.Bin,
                title: "Delete selected submissions",
                destructive: true);

            try
            {
                if (await exportService.HasAnySubmissionsAsync(Page.FormId, CancellationToken.None))
                {
                    var headerActions = Page.PageConfiguration.HeaderActions.AddActionWithCustomComponent(
                        new AddActionWithCustomComponentParameters(
                            "Advanced delete",
                            new FormSubmissionRemovalActionComponent
                            {
                                Properties = new FormSubmissionRemovalActionProperties(),
                            })
                        {
                            Icon = Icons.Bin,
                            Title = "Advanced delete",
                            Destructive = true,
                        });
                    headerActions[^1].ButtonColor = ButtonColor.Secondary;
                }
            }
            catch (FormSubmissionExportNotFoundException exception)
            {
                logger.LogWarning(exception, "Could not configure advanced delete for missing form {FormId}.", Page.FormId);
            }
        }

        await base.ConfigurePage();
    }

    [PageCommand(Permission = FormSubmissionRemovalConstants.Permission)]
    public async Task<ICommandResponse<FormSubmissionRemovalPreviewResponse>> PreviewAdvancedDelete(
        FormSubmissionRemovalRangeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = FormSubmissionRemovalRangeParser.Parse(request);
            int count = await removalService.CountMatchingAsync(Page.FormId, options, cancellationToken);
            return ResponseFrom(new FormSubmissionRemovalPreviewResponse { MatchingCount = count });
        }
        catch (FormSubmissionRemovalValidationException exception)
        {
            return ResponseFrom(new FormSubmissionRemovalPreviewResponse { Error = exception.Message });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not preview advanced delete for form {FormId}.", Page.FormId);
            return ResponseFrom(new FormSubmissionRemovalPreviewResponse { Error = "The matching count could not be calculated." });
        }
    }

    [PageCommand(Permission = FormSubmissionRemovalConstants.Permission)]
    public async Task<ICommandResponse<FormSubmissionAdvancedDeleteResponse>> AdvancedDelete(
        FormSubmissionRemovalRangeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = FormSubmissionRemovalRangeParser.Parse(request);
            int deleted = await removalService.DeleteMatchingAsync(Page.FormId, options, cancellationToken);
            return ResponseFrom(new FormSubmissionAdvancedDeleteResponse { DeletedCount = deleted });
        }
        catch (FormSubmissionRemovalValidationException exception)
        {
            return ResponseFrom(new FormSubmissionAdvancedDeleteResponse { Error = exception.Message });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not perform advanced delete for form {FormId}.", Page.FormId);
            return ResponseFrom(new FormSubmissionAdvancedDeleteResponse { Error = "The submissions could not be deleted." });
        }
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
