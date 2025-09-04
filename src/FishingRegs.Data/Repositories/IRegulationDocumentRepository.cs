using FishingRegs.Data.Models;

namespace FishingRegs.Data.Repositories;

/// <summary>
/// Repository interface for RegulationDocument entity operations
/// </summary>
public interface IRegulationDocumentRepository : IRepository<RegulationDocument>
{
    /// <summary>
    /// Gets regulation documents by state
    /// </summary>
    /// <param name="stateId">The state ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of regulation documents for the state</returns>
    Task<IEnumerable<RegulationDocument>> GetByStateAsync(int stateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulation documents by processing status
    /// </summary>
    /// <param name="status">The processing status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of documents with the specified status</returns>
    Task<IEnumerable<RegulationDocument>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulation documents by document type
    /// </summary>
    /// <param name="documentType">The document type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of documents of the specified type</returns>
    Task<IEnumerable<RegulationDocument>> GetByTypeAsync(string documentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets processed regulation documents that are ready for use
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of processed documents</returns>
    Task<IEnumerable<RegulationDocument>> GetProcessedDocumentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulation documents that need processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of documents pending processing</returns>
    Task<IEnumerable<RegulationDocument>> GetPendingProcessingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulation documents uploaded within a date range
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of documents uploaded within the date range</returns>
    Task<IEnumerable<RegulationDocument>> GetByUploadDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a regulation document by its file name
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The document with the specified file name or null</returns>
    Task<RegulationDocument?> GetByFileNameAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets regulation documents with their related regulations
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of documents with related regulations loaded</returns>
    Task<IEnumerable<RegulationDocument>> GetWithRelatedDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a regulation document by ID with related regulations
    /// </summary>
    /// <param name="id">The document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document with related regulations or null if not found</returns>
    Task<RegulationDocument?> GetByIdWithRelatedDataAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documents that have failed processing with error details
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of failed documents</returns>
    Task<IEnumerable<RegulationDocument>> GetFailedDocumentsAsync(CancellationToken cancellationToken = default);
}
