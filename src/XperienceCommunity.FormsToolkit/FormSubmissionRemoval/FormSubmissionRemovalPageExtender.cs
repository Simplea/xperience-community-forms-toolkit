using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

[assembly: PageExtender(typeof(XperienceCommunity.FormsToolkit.FormSubmissionRemoval.FormSubmissionRemovalPageExtender))]

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

[UIPermission(FormSubmissionRemovalConstants.Permission, "Delete form submissions")]
public sealed class FormSubmissionRemovalPageExtender(
    IUIPermissionEvaluator permissionEvaluator,
    IFormSubmissionExportService exportService,
    IFormSubmissionRemovalService removalService,
    IOptions<FormsToolkitOptions> options,
    ILogger<FormSubmissionRemovalPageExtender> logger) : PageExtender<FormSubmissionsTab>
{
    public override async Task ConfigurePage()
    {
        bool permitted = options.Value.EnableFormSubmissionRemoval
            && (await permissionEvaluator.Evaluate(FormSubmissionRemovalConstants.Permission)).Succeeded;
        if (permitted)
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
            var removalOptions = FormSubmissionRemovalRangeParser.Parse(request);
            int count = await removalService.CountMatchingAsync(Page.FormId, removalOptions, cancellationToken);
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
            var removalOptions = FormSubmissionRemovalRangeParser.Parse(request);
            int deleted = await removalService.DeleteMatchingAsync(Page.FormId, removalOptions, cancellationToken);
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
        // Mirrors the validation FormSubmissionRemovalService applies to the same identifiers,
        // so "requested" and "deleted" are counted on exactly the same set.
        int requestedCount = (identifiers ?? []).Where(id => id > 0).Distinct().Count();

        try
        {
            int deletedCount = await removalService.DeleteByIdsAsync(Page.FormId, identifiers?.ToList() ?? [], cancellationToken);
            var response = ResponseFrom(new MassActionResult(reload: true, refetchAll: true));
            if (deletedCount < requestedCount)
            {
                // The shared deletion service stops the batch the moment a submission's uploaded
                // file cannot be deleted (see FormSubmissionRemovalService.DeleteUploadedFiles), so
                // fewer rows than requested may have been removed. Reporting plain success here
                // would hide that from the administrator; surface it instead of only logging it.
                logger.LogWarning(
                    "Quick delete for form {FormId} deleted {DeletedCount} of {RequestedCount} selected submissions; the rest were left in place after a file-cleanup failure.",
                    Page.FormId,
                    deletedCount,
                    requestedCount);
                response.Messages.Add(new CommandResponseMessage
                {
                    Level = CommandResponseMessageLevel.Error,
                    Message = $"Only {deletedCount} of {requestedCount} selected submissions could be deleted. An uploaded file could not be removed; check the event log for details.",
                });
            }

            return response;
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
