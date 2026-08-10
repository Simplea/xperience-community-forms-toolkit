using System.Globalization;

using CMS.Base;
using CMS.Membership;
using CMS.OnlineForms;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: PageExtender(typeof(XperienceCommunity.FormsToolkit.FormCloning.FormListPageExtender))]

namespace XperienceCommunity.FormsToolkit.FormCloning;

public sealed class FormListPageExtender(
    IFormCloneService cloneService,
    IUIPermissionEvaluator permissionEvaluator,
    IOptions<FormsToolkitOptions> options,
    ILogger<FormListPageExtender> logger) : PageExtender<FormList>
{
    public override async Task ConfigurePage()
    {
        if (options.Value.EnableFormCloning)
        {
            bool canCreate = (await permissionEvaluator.Evaluate(SystemPermissions.CREATE)).Succeeded;
            Page.PageConfiguration.TableActions.AddActionWithCustomComponent(
                new AddActionWithCustomComponentParameters(
                    "Clone form",
                    new FormCloneActionComponent
                    {
                        Properties = new FormCloneActionProperties(),
                    })
                {
                    Icon = Icons.DocCopy,
                    Title = "Clone form",
                    Disabled = !canCreate,
                    ActionStateEvaluator = SetCloneActionParameter,
                });
        }

        await base.ConfigurePage();
    }

    internal static Task SetCloneActionParameter(
        ActionConfiguration action,
        IDataContainer row,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        action.Parameter = Convert.ToString(
            row.GetValue(nameof(BizFormInfo.FormID)),
            CultureInfo.InvariantCulture);
        return Task.CompletedTask;
    }

    [PageCommand(Permission = SystemPermissions.CREATE)]
    public async Task<ICommandResponse<FormCloneDefaultsResponse>> GetFormCloneDefaults(
        int sourceFormId,
        CancellationToken cancellationToken)
    {
        try
        {
            string displayName = await cloneService.GetDefaultDisplayNameAsync(sourceFormId, cancellationToken);
            return ResponseFrom(new FormCloneDefaultsResponse { DisplayName = displayName });
        }
        catch (FormCloneNotFoundException)
        {
            return ResponseFrom(new FormCloneDefaultsResponse { Error = "The requested form was not found." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not prepare cloning for form {FormId}.", sourceFormId);
            return ResponseFrom(new FormCloneDefaultsResponse { Error = "The form could not be loaded." });
        }
    }

    [PageCommand(Permission = SystemPermissions.CREATE)]
    public async Task<ICommandResponse<FormCloneCommandResponse>> CloneForm(
        FormCloneCommandRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await cloneService.CloneAsync(
                request.SourceFormId,
                request.DisplayName,
                cancellationToken);
            return ResponseFrom(new FormCloneCommandResponse { ClonedFormId = result.FormId });
        }
        catch (FormCloneValidationException exception)
        {
            return ResponseFrom(new FormCloneCommandResponse { Error = exception.Message });
        }
        catch (FormCloneNotFoundException)
        {
            return ResponseFrom(new FormCloneCommandResponse { Error = "The requested form was not found." });
        }
        catch (FormCloneOperationException exception)
        {
            logger.LogError(exception, "Could not clone form {FormId}.", request.SourceFormId);
            return ResponseFrom(new FormCloneCommandResponse { Error = "The form could not be cloned." });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unexpected error while cloning form {FormId}.", request.SourceFormId);
            return ResponseFrom(new FormCloneCommandResponse { Error = "The form could not be cloned." });
        }
    }
}
