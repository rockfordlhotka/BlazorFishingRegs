using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FishingRegs.Services.Models;

namespace FishingRegs.Services.Interfaces;

/// <summary>
/// Azure Blob Storage service for document storage
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a document to blob storage
    /// </summary>
    /// <param name="stream">Document stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="contentType">Content type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Blob upload result</returns>
    Task<BlobUploadResult> UploadDocumentAsync(
        Stream stream, 
        string fileName, 
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a document from blob storage
    /// </summary>
    /// <param name="blobName">Blob name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document stream</returns>
    Task<Stream> DownloadDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from blob storage
    /// </summary>
    /// <param name="blobName">Blob name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully</returns>
    Task<bool> DeleteDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a presigned URL for secure access to a blob
    /// </summary>
    /// <param name="blobName">Blob name</param>
    /// <param name="expiry">URL expiry time</param>
    /// <returns>Presigned URL</returns>
    Task<string> GeneratePresignedUrlAsync(
        string blobName, 
        TimeSpan expiry);
}
