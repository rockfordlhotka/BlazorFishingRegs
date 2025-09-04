using FishingRegs.Data;
using FishingRegs.Data.Tests.Infrastructure;

namespace FishingRegs.Data.Tests.Infrastructure;

/// <summary>
/// Base test class providing common test infrastructure
/// </summary>
public abstract class BaseRepositoryTest : IDisposable
{
    protected readonly FishingRegsDbContext Context;
    private bool _disposed = false;

    protected BaseRepositoryTest()
    {
        Context = TestDbContextFactory.CreateInMemoryContext();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Context?.Dispose();
        }
        _disposed = true;
    }
}
