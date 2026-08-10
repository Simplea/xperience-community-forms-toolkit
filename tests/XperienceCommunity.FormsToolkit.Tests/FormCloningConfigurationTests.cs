using CMS.Base;
using CMS.Membership;
using CMS.OnlineForms;

using Kentico.Xperience.Admin.Base;

using XperienceCommunity.FormsToolkit.FormCloning;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormCloningConfigurationTests
{
    [Test]
    public void CloningIsEnabledByDefault() =>
        Assert.That(new FormsToolkitOptions().EnableFormCloning, Is.True);

    [TestCase(nameof(FormListPageExtender.GetFormCloneDefaults))]
    [TestCase(nameof(FormListPageExtender.CloneForm))]
    public void CloneCommandsRequireCreatePermission(string methodName)
    {
        var attribute = typeof(FormListPageExtender)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(PageCommandAttribute), inherit: true)
            .Cast<PageCommandAttribute>()
            .Single();

        Assert.That(attribute.Permission, Is.EqualTo(SystemPermissions.CREATE));
    }

    [Test]
    public void CloneClientComponentUsesPackagedModule()
    {
        var component = new FormCloneActionComponent();

        Assert.That(component.ClientComponentName, Is.EqualTo("@xperience-community/forms-toolkit/FormClone"));
    }

    [Test]
    public async Task CloneActionReceivesFormIdFromItsListingRow()
    {
        var action = new ActionConfiguration();
        var row = new DataContainer
        {
            [nameof(BizFormInfo.FormID)] = 42,
        };

        await FormListPageExtender.SetCloneActionParameter(action, row, CancellationToken.None);

        Assert.That(action.Parameter, Is.EqualTo("42"));
    }
}
