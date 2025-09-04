using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FishingRegs.Services.Interfaces;
using FishingRegs.Services.Models;
using FishingRegs.Services.Extensions;

namespace FishingRegs.Services.Services;

/// <summary>
/// Azure Blob Storage service implementation
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        var connectionString = configuration.GetSecureValue("ConnectionStrings:AzureStorage")
            ?? throw new InvalidOperationException("Azure Storage connection string not configured. Please add to User Secrets or Key Vault.");
        var containerName = configuration["AzureStorage:ContainerName"] ?? "fishing-regulations";

        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<BlobUploadResult> UploadDocumentAsync(
        Stream stream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure container exists
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

            var blobName = GenerateUniqueBlobName(fileName);
            var blobClient = _containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
                Metadata = new Dictionary<string, string>
                {
                    ["OriginalFileName"] = fileName,
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                    ["ContentType"] = contentType
                }
            };

            _logger.LogInformation("Uploading document {FileName} as blob {BlobName}", fileName, blobName);

            await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

            _logger.LogInformation("Successfully uploaded document {FileName} as blob {BlobName}", fileName, blobName);

            return new BlobUploadResult
            {
                BlobName = blobName,
                BlobUrl = blobClient.Uri.ToString(),
                ContentType = contentType,
                Size = stream.Length
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload document {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream> DownloadDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            _logger.LogInformation("Downloading blob {BlobName}", blobName);

            var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully downloaded blob {BlobName}", blobName);

            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download blob {BlobName}", blobName);
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            _logger.LogInformation("Deleting blob {BlobName}", blobName);

            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (response.Value)
            {
                _logger.LogInformation("Successfully deleted blob {BlobName}", blobName);
            }
            else
            {
                _logger.LogWarning("Blob {BlobName} did not exist", blobName);
            }

            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob {BlobName}", blobName);
            return false;
        }
    }

    public Task<string> GeneratePresignedUrlAsync(string blobName, TimeSpan expiry)
    {
        try
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            // For now, return the direct blob URL
            // In production, you would implement SAS token generation here
            var presignedUrl = blobClient.Uri.ToString();

            _logger.LogInformation("Generated presigned URL for blob {BlobName}", blobName);

            return Task.FromResult(presignedUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for blob {BlobName}", blobName);
            throw;
        }
    }

    private string GenerateUniqueBlobName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        var uniqueId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");

        return $"documents/{timestamp}/{uniqueId}{extension}";
    }
}
