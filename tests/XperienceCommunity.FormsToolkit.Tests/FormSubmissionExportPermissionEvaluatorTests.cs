using CMS.Membership.Internal;

using Kentico.Membership;
using Kentico.Xperience.Admin.Base.Authentication;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;

using XperienceCommunity.FormsToolkit.FormSubmissionExport;

namespace XperienceCommunity.FormsToolkit.Tests;

public class FormSubmissionExportPermissionEvaluatorTests
{
    [Test]
    public async Task EvaluatesTheExportPermissionForTheFormsApplicationAndAuthenticatedUser()
    {
        var user = new AdminApplicationUser();
        var applicationEvaluator = new CapturingApplicationPermissionEvaluator(succeeded: true);
        var evaluator = new FormSubmissionExportPermissionEvaluator(
            new StubAuthenticatedUserAccessor(user),
            applicationEvaluator);

        bool succeeded = await evaluator.CanExportAsync();

        Assert.Multiple(() =>
        {
            Assert.That(succeeded, Is.True);
            Assert.That(applicationEvaluator.Context, Is.Not.Null);
            Assert.That(applicationEvaluator.Context!.ApplicationName, Is.EqualTo(FormsApplication.IDENTIFIER));
            Assert.That(applicationEvaluator.Context.PermissionName, Is.EqualTo(FormSubmissionExportConstants.Permission));
            Assert.That(applicationEvaluator.Context.User, Is.SameAs(user));
        });
    }

    [Test]
    public async Task ReturnsTheApplicationPermissionDecision()
    {
        var evaluator = new FormSubmissionExportPermissionEvaluator(
            new StubAuthenticatedUserAccessor(new AdminApplicationUser()),
            new CapturingApplicationPermissionEvaluator(succeeded: false));

        Assert.That(await evaluator.CanExportAsync(), Is.False);
    }

    private sealed class StubAuthenticatedUserAccessor(AdminApplicationUser user) : IAuthenticatedUserAccessor
    {
        public Task<AdminApplicationUser> Get() => Task.FromResult(user);
    }

    private sealed class CapturingApplicationPermissionEvaluator(bool succeeded) : IApplicationPermissionEvaluator
    {
        public ApplicationPermissionEvaluationContext? Context { get; private set; }

        public bool Evaluate(ApplicationPermissionEvaluationContext context)
        {
            Context = context;
            return succeeded;
        }
    }
}
