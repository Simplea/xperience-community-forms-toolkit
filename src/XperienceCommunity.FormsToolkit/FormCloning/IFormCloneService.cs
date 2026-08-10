namespace XperienceCommunity.FormsToolkit.FormCloning;

public interface IFormCloneService
{
    public Task<string> GetDefaultDisplayNameAsync(int sourceFormId, CancellationToken cancellationToken);

    public Task<FormCloneResult> CloneAsync(
        int sourceFormId,
        string displayName,
        CancellationToken cancellationToken);
}
