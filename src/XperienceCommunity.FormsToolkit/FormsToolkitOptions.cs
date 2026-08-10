namespace XperienceCommunity.FormsToolkit;

public sealed class FormsToolkitOptions
{
    public int MaximumConcurrentExports { get; set; } = 2;

    public bool EnableFormCloning { get; set; } = true;
}
