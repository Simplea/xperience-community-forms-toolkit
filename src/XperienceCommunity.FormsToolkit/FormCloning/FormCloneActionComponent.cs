using Kentico.Xperience.Admin.Base;

namespace XperienceCommunity.FormsToolkit.FormCloning;

internal sealed class FormCloneActionComponent
    : ActionComponent<FormCloneActionProperties, FormCloneActionClientProperties>
{
    public override string ClientComponentName => FormCloneConstants.ClientComponent;

    protected override Task ConfigureClientProperties(FormCloneActionClientProperties clientProperties)
    {
        clientProperties.GetDefaultsCommandName = FormCloneConstants.GetDefaultsCommand;
        clientProperties.CloneCommandName = FormCloneConstants.CloneCommand;
        return base.ConfigureClientProperties(clientProperties);
    }
}

internal sealed class FormCloneActionProperties : IActionComponentProperties;

internal sealed class FormCloneActionClientProperties : IActionComponentClientProperties
{
    public string ComponentName { get; init; } = FormCloneConstants.ClientComponent;

    public string GetDefaultsCommandName { get; set; } = FormCloneConstants.GetDefaultsCommand;

    public string CloneCommandName { get; set; } = FormCloneConstants.CloneCommand;
}
