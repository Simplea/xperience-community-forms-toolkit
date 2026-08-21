using Kentico.Xperience.Admin.Base;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.FormSubmissionRemoval;

internal sealed class FormSubmissionRemovalActionComponent
    : ActionComponent<FormSubmissionRemovalActionProperties, FormSubmissionRemovalActionClientProperties>
{
    public override string ClientComponentName => FormSubmissionRemovalConstants.ClientComponent;

    protected override Task ConfigureClientProperties(FormSubmissionRemovalActionClientProperties clientProperties)
    {
        clientProperties.PreviewCommandName = FormSubmissionRemovalConstants.PreviewCommand;
        clientProperties.DeleteCommandName = FormSubmissionRemovalConstants.DeleteCommand;
        clientProperties.ExportTokenCommandName = FormSubmissionExportConstants.CreateTokenCommand;
        return base.ConfigureClientProperties(clientProperties);
    }
}

internal sealed class FormSubmissionRemovalActionProperties : IActionComponentProperties
{
}

internal sealed class FormSubmissionRemovalActionClientProperties : IActionComponentClientProperties
{
    public string ComponentName { get; init; } = FormSubmissionRemovalConstants.ClientComponent;

    public string PreviewCommandName { get; set; } = FormSubmissionRemovalConstants.PreviewCommand;

    public string DeleteCommandName { get; set; } = FormSubmissionRemovalConstants.DeleteCommand;

    public string ExportTokenCommandName { get; set; } = string.Empty;
}
