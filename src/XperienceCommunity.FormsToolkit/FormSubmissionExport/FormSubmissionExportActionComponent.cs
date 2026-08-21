using Kentico.Xperience.Admin.Base;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

internal sealed class FormSubmissionExportActionComponent
    : ActionComponent<FormSubmissionExportActionProperties, FormSubmissionExportActionClientProperties>
{
    public override string ClientComponentName => FormSubmissionExportConstants.ClientComponent;

    protected override Task ConfigureClientProperties(FormSubmissionExportActionClientProperties clientProperties)
    {
        clientProperties.CommandName = FormSubmissionExportConstants.CreateTokenCommand;
        clientProperties.Fields = Properties.Fields;
        return base.ConfigureClientProperties(clientProperties);
    }
}

internal sealed class FormSubmissionExportActionProperties : IActionComponentProperties
{
    public IReadOnlyList<FormSubmissionExportFieldOption> Fields { get; init; } = [];
}

internal sealed class FormSubmissionExportActionClientProperties : IActionComponentClientProperties
{
    public string ComponentName { get; init; } = FormSubmissionExportConstants.ClientComponent;

    public string CommandName { get; set; } = FormSubmissionExportConstants.CreateTokenCommand;

    public IReadOnlyList<FormSubmissionExportFieldOption> Fields { get; set; } = [];
}
