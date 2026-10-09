using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>Ticket 10's production storage provider. Requires an account-key connection string
/// (not a bare Azure AD credential) so <see cref="BlobClient.GenerateSasUri(BlobSasBuilder)"/> can
/// self-sign — generating a user-delegation SAS would need an extra round trip to Azure AD for a
/// delegation key, which this slice's scope doesn't call for.</summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IOptions<StorageSettings> options)
    {
        var settings = options.Value.AzureBlob;
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Missing required configuration \"{StorageSettings.SectionName}:AzureBlob:ConnectionString\".");
        }

        var serviceClient = new BlobServiceClient(settings.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(settings.ContainerName);
    }

    public async Task SaveAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = _containerClient.GetBlobClient(storagePath);
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    public Task<Uri> GetDownloadUrlAsync(
        string storagePath, string downloadFileName, string contentType, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(storagePath);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "The configured Azure Blob client cannot generate SAS URIs; check that the connection string carries an account key.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = storagePath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
            ContentDisposition = $"attachment; filename=\"{downloadFileName}\"",
            ContentType = contentType
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder));
    }
}
