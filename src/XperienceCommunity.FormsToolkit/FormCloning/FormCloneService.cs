using CMS.DataEngine;
using CMS.FormEngine;
using CMS.OnlineForms;

using Microsoft.Extensions.Logging;

namespace XperienceCommunity.FormsToolkit.FormCloning;

public sealed class FormCloneService(
    IInfoProvider<BizFormInfo> formProvider,
    IInfoProvider<BizFormRoleInfo> formRoleProvider,
    ILogger<FormCloneService> logger) : IFormCloneService
{
    public async Task<string> GetDefaultDisplayNameAsync(
        int sourceFormId,
        CancellationToken cancellationToken)
    {
        var source = await GetSourceAsync(sourceFormId, cancellationToken);
        return FormCloneName.GetDefault(source.FormDisplayName);
    }

    public async Task<FormCloneResult> CloneAsync(
        int sourceFormId,
        string displayName,
        CancellationToken cancellationToken)
    {
        string normalizedDisplayName = FormCloneName.Normalize(displayName);
        var source = await GetSourceAsync(sourceFormId, cancellationToken);
        int[] authorizedRoleIds = BizFormInfoProvider.GetFormAuthorizedRoles(source.FormID)
            .Select(role => role.RoleID)
            .Distinct()
            .ToArray();

        BizFormInfo? clone = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            clone = BizFormHelper.Create(
                normalizedDisplayName,
                InfoHelper.CODENAME_AUTOMATIC,
                InfoHelper.CODENAME_AUTOMATIC);

            CopyFormDefinition(source, clone);
            CopyFormSettings(source, clone);
            await formProvider.SetAsync(clone, cancellationToken);

            foreach (int roleId in authorizedRoleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await formRoleProvider.SetAsync(
                    new BizFormRoleInfo
                    {
                        FormID = clone.FormID,
                        RoleID = roleId,
                    },
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var persistedClone = await formProvider.GetAsync(clone.FormID, cancellationToken)
                ?? throw new FormCloneOperationException("The cloned form could not be verified.");
            if (persistedClone.FormItems != 0)
            {
                throw new FormCloneOperationException("The cloned form did not start with zero submissions.");
            }

            return new FormCloneResult(persistedClone.FormID, persistedClone.FormDisplayName);
        }
        catch (Exception exception) when (exception is not FormCloneValidationException
            and not FormCloneNotFoundException)
        {
            await CleanupAsync(clone);

            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw exception is FormCloneOperationException
                ? exception
                : new FormCloneOperationException("The form could not be cloned.", exception);
        }
    }

    private async Task<BizFormInfo> GetSourceAsync(int sourceFormId, CancellationToken cancellationToken)
    {
        if (sourceFormId <= 0)
        {
            throw new FormCloneNotFoundException();
        }

        return await formProvider.GetAsync(sourceFormId, cancellationToken)
            ?? throw new FormCloneNotFoundException();
    }

    private static void CopyFormDefinition(BizFormInfo source, BizFormInfo clone)
    {
        var sourceClass = DataClassInfoProvider.GetDataClassInfo(source.FormClassID)
            ?? throw new FormCloneOperationException("The source form definition could not be loaded.");
        var cloneClass = DataClassInfoProvider.GetDataClassInfo(clone.FormClassID)
            ?? throw new FormCloneOperationException("The cloned form definition could not be loaded.");

        var sourceForm = new FormInfo(sourceClass.ClassFormDefinition);
        var cloneForm = new FormInfo(cloneClass.ClassFormDefinition);
        var sourcePrimaryKey = sourceForm.GetFields(true, true, onlyPrimaryKeys: true).SingleOrDefault()
            ?? throw new FormCloneOperationException("The source form primary key could not be identified.");
        var clonePrimaryKey = cloneForm.GetFields(true, true, onlyPrimaryKeys: true).SingleOrDefault()
            ?? throw new FormCloneOperationException("The cloned form primary key could not be identified.");

        sourceForm.RemoveFormField(sourcePrimaryKey.Name);
        sourceForm.AddFormItem(clonePrimaryKey, 0);

        cloneClass.ClassFormDefinition = sourceForm.GetXmlDefinition();
        cloneClass.ClassContactMapping = sourceClass.ClassContactMapping;
        DataClassInfoProvider.SetDataClassInfo(cloneClass);
    }

    private static void CopyFormSettings(BizFormInfo source, BizFormInfo clone)
    {
        clone.FormBuilderLayout = source.FormBuilderLayout;
        clone.FormSubmitButtonText = source.FormSubmitButtonText;
        clone.FormSubmitButtonImage = source.FormSubmitButtonImage;
        clone.FormLogActivity = source.FormLogActivity;
        clone.FormAccess = source.FormAccess;
        clone.FormReportFields = source.FormReportFields;
        clone.FormItems = 0;
    }

    private async Task CleanupAsync(BizFormInfo? clone)
    {
        try
        {
            if (clone is not null && clone.FormID > 0)
            {
                await formProvider.DeleteAsync(clone, CancellationToken.None);
            }
        }
        catch (Exception cleanupException)
        {
            logger.LogError(
                cleanupException,
                "Could not clean up a failed form clone. Clone form ID: {CloneFormId}.",
                clone?.FormID);
        }
    }
}
