using CMS;

using Kentico.Xperience.Admin.Base;

[assembly: AssemblyDiscoverable]
[assembly: RegisterModule(typeof(XperienceCommunity.FormsToolkit.FormsToolkitAdminModule))]

namespace XperienceCommunity.FormsToolkit;

internal sealed class FormsToolkitAdminModule() : AdminModule("XperienceCommunity.FormsToolkit")
{
    protected override void OnInit()
    {
        base.OnInit();
        RegisterClientModule("xperience-community", "forms-toolkit");
    }
}
