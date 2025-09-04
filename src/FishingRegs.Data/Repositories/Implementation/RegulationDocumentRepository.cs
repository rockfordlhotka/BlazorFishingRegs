using Microsoft.EntityFrameworkCore;
using FishingRegs.Data.Models;
using FishingRegs.Data.Repositories;

namespace FishingRegs.Data.Repositories.Implementation;

/// <summary>
/// Repository implementation for RegulationDocument entity operations
/// </summary>
public class RegulationDocumentRepository : Repository<RegulationDocument>, IRegulationDocumentRepository
{
    private readonly FishingRegsDbContext _fishingRegsContext;

    public RegulationDocumentRepository(FishingRegsDbContext context) : base(context)
    {
        _fishingRegsContext = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetByStateAsync(int stateId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rd => rd.StateId == stateId)
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rd => rd.ProcessingStatus == status)
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetByTypeAsync(string documentType, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rd => rd.DocumentType == documentType)
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetProcessedDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rd => rd.ProcessingStatus == "completed")
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.ProcessingCompletedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetPendingProcessingAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rd => rd.ProcessingStatus == "pending" || rd.ProcessingStatus == "processing")
            .Include(rd => rd.State)
            .OrderBy(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetByUploadDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var startOffset = new DateTimeOffset(startDate, TimeSpan.Zero);
        var endOffset = new DateTimeOffset(endDate, TimeSpan.Zero).AddDays(1).AddTicks(-1);

        return await _dbSet
            .Where(rd => rd.CreatedAt >= startOffset && rd.CreatedAt <= endOffset)
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RegulationDocument?> GetByFileNameAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(rd => rd.State)
            .FirstOrDefaultAsync(rd => rd.FileName == fileName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetWithRelatedDataAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(rd => rd.State)
            .Include(rd => rd.FishingRegulations)
                .ThenInclude(fr => fr.WaterBody)
            .Include(rd => rd.FishingRegulations)
                .ThenInclude(fr => fr.Species)
            .OrderByDescending(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RegulationDocument?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(rd => rd.State)
            .Include(rd => rd.FishingRegulations)
                .ThenInclude(fr => fr.WaterBody)
            .Include(rd => rd.FishingRegulations)
                .ThenInclude(fr => fr.Species)
            .FirstOrDefaultAsync(rd => rd.Id == Guid.Parse(id.ToString()), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RegulationDocument>> GetFailedDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(rd => rd.ProcessingStatus == "failed")
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.ProcessingCompletedAt ?? rd.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<IEnumerable<RegulationDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(rd => rd.State)
            .OrderByDescending(rd => rd.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
