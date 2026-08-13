using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

[assembly: PageExtender(typeof(XperienceCommunity.FormsToolkit.FormSubmissionExport.FormSubmissionsPageExtender))]

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

[UIPermission(FormSubmissionExportConstants.Permission, "Export form submissions")]
public sealed class FormSubmissionsPageExtender(
    IUIPermissionEvaluator permissionEvaluator,
    IFormSubmissionExportUserAccessor userAccessor,
    IFormSubmissionExportTokenService tokenService,
    IFormSubmissionExportService exportService,
    ILogger<FormSubmissionsPageExtender> logger,
    TimeProvider timeProvider) : PageExtender<FormSubmissionsTab>
{
    public override async Task ConfigurePage()
    {
        var permission = await permissionEvaluator.Evaluate(FormSubmissionExportConstants.Permission);
        if (permission.Succeeded)
        {
            try
            {
                var definition = await exportService.GetDefinitionAsync(Page.FormId, CancellationToken.None);
                Page.PageConfiguration.HeaderActions.AddActionWithCustomComponent(
                    new AddActionWithCustomComponentParameters(
                        "Export",
                        new FormSubmissionExportActionComponent
                        {
                            Properties = new FormSubmissionExportActionProperties
                            {
                                CurrentViewDownloadUrl = $"{FormSubmissionExportConstants.CurrentViewDownloadRoute}/{Page.FormId}",
                                Fields = definition.Fields
                                    .Select(field => new FormSubmissionExportFieldOption(
                                        field.Identifier,
                                        field.SourceName,
                                        field.Caption,
                                        field.VisibleInListing))
                                    .ToList(),
                            },
                        })
                    {
                        Icon = Icons.ChevronDown,
                        Title = "Export form submissions",
                    });

                Page.PageConfiguration.MassActions.AddActionWithCustomComponent(
                    new AddActionWithCustomComponentParameters(
                        "Export selected",
                        new FormSubmissionExportSelectedActionComponent
                        {
                            Properties = new FormSubmissionExportSelectedActionProperties
                            {
                                CurrentViewDownloadUrl = $"{FormSubmissionExportConstants.CurrentViewDownloadRoute}/{Page.FormId}",
                                Columns = definition.Fields
                                    .Where(field => field.VisibleInListing)
                                    .Select(field => field.Identifier)
                                    .ToList(),
                            },
                        })
                    {
                        Icon = Icons.ArrowDownLine,
                        Title = "Export selected submissions",
                    });
            }
            catch (FormSubmissionExportNotFoundException exception)
            {
                logger.LogWarning(exception, "Could not configure export for missing form {FormId}.", Page.FormId);
            }
        }

        await base.ConfigurePage();
    }

    [PageCommand(Permission = FormSubmissionExportConstants.Permission)]
    public async Task<ICommandResponse<FormSubmissionExportTokenResponse>> CreateExportToken(
        FormSubmissionExportCommandRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var export = await exportService.PrepareAsync(Page.FormId, request, cancellationToken);
            var payload = new FormSubmissionExportTokenPayload(
                await userAccessor.GetUserIdAsync(),
                Page.FormId,
                FormSubmissionExportConstants.Permission,
                export.Options,
                timeProvider.GetUtcNow().AddMinutes(2));

            string token = tokenService.Create(payload);
            string downloadUrl = QueryHelpers.AddQueryString(FormSubmissionExportConstants.DownloadRoute, "token", token);
            return ResponseFrom(new FormSubmissionExportTokenResponse { DownloadUrl = downloadUrl });
        }
        catch (FormSubmissionExportValidationException exception)
        {
            return ResponseFrom(new FormSubmissionExportTokenResponse { Error = exception.Message });
        }
        catch (FormSubmissionExportNotFoundException)
        {
            return ResponseFrom(new FormSubmissionExportTokenResponse { Error = "The requested form was not found." });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not prepare an export token for form {FormId}.", Page.FormId);
            return ResponseFrom(new FormSubmissionExportTokenResponse { Error = "The export could not be prepared." });
        }
    }
}
