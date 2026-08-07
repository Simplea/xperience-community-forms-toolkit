using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

namespace XperienceCommunity.FormsToolkit.FormSubmissionExport;

public interface IFormSubmissionExportConcurrencyGate
{
    public IDisposable? TryAcquire(int userId);
}

internal sealed class FormSubmissionExportConcurrencyGate : IFormSubmissionExportConcurrencyGate, IDisposable
{
    private readonly ConcurrentDictionary<int, byte> activeUsers = new();
    private readonly SemaphoreSlim applicationSlots;

    public FormSubmissionExportConcurrencyGate(IOptions<FormsToolkitOptions> options)
    {
        int maximum = options.Value.MaximumConcurrentExports;
        if (maximum <= 0)
        {
            throw new InvalidOperationException("MaximumConcurrentExports must be greater than zero.");
        }

        applicationSlots = new SemaphoreSlim(maximum, maximum);
    }

    public IDisposable? TryAcquire(int userId)
    {
        if (userId <= 0 || !activeUsers.TryAdd(userId, 0))
        {
            return null;
        }

        if (!applicationSlots.Wait(0))
        {
            activeUsers.TryRemove(userId, out _);
            return null;
        }

        return new Lease(this, userId);
    }

    public void Dispose() => applicationSlots.Dispose();

    private void Release(int userId)
    {
        activeUsers.TryRemove(userId, out _);
        applicationSlots.Release();
    }

    private sealed class Lease(FormSubmissionExportConcurrencyGate owner, int userId) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Release(userId);
            }
        }
    }
}
