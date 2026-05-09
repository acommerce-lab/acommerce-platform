using ACommerce.Files.Abstractions.Enums;
using ACommerce.Files.Abstractions.Exceptions;
using ACommerce.Files.Abstractions.Helpers;
using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.GoogleCloud.Configuration;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACommerce.Files.Storage.GoogleCloud.Providers;

public class GoogleCloudStorageProvider : IStorageProvider
{
    private readonly GoogleCloudStorageOptions _options;
    private readonly ILogger<GoogleCloudStorageProvider> _logger;
    private readonly StorageClient _client;

    public string ProviderName => "GoogleCloudStorage";
    public StorageType StorageType => StorageType.GoogleCloud;

    public GoogleCloudStorageProvider(
        IOptions<GoogleCloudStorageOptions> options,
        ILogger<GoogleCloudStorageProvider> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _client = StorageClient.Create();

        _logger.LogInformation("Google Cloud Storage Provider initialized for bucket: {Bucket}", _options.BucketName);
    }

    public async Task<string> SaveAsync(
        Stream stream,
        string fileName,
        string? directory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var uniqueFileName = FileNameHelper.GenerateUniqueFileName(fileName);
            var objectName = BuildObjectName(uniqueFileName, directory);

            Stream uploadStream;
            bool disposeStream = false;

            if (stream.CanSeek)
            {
                stream.Position = 0;
                uploadStream = stream;
            }
            else
            {
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                uploadStream = memoryStream;
                disposeStream = true;
            }

            try
            {
                var contentType = GetContentType(fileName);
                
                var obj = await _client.UploadObjectAsync(
                    _options.BucketName,
                    objectName,
                    contentType,
                    uploadStream,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("File uploaded to GCS: {Key}, Generation: {Generation}", 
                    objectName, obj.Generation);
            }
            finally
            {
                if (disposeStream)
                {
                    await uploadStream.DisposeAsync();
                }
            }

            return objectName;
        }
        catch (Google.GoogleApiException ex)
        {
            _logger.LogError(ex, "GCS error uploading file: {FileName}, Code: {Code}", fileName, ex.Error?.Code);
            throw new StorageException("GCS_UPLOAD_FAILED", $"Failed to upload file: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file to GCS: {FileName}", fileName);
            throw new StorageException("SAVE_FAILED", "Failed to save file to GCS", ex);
        }
    }

    public async Task<Stream?> GetAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryStream = new MemoryStream();
            await _client.DownloadObjectAsync(_options.BucketName, filePath, memoryStream, cancellationToken: cancellationToken);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found in GCS: {Path}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file from GCS: {Path}", filePath);
            throw new StorageException("GET_FAILED", "Failed to retrieve file from GCS", ex);
        }
    }

    public async Task<bool> DeleteAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_options.BucketName, filePath, cancellationToken: cancellationToken);
            _logger.LogInformation("File deleted from GCS: {Path}", filePath);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found for deletion in GCS: {Path}", filePath);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from GCS: {Path}", filePath);
            throw new StorageException("DELETE_FAILED", "Failed to delete file from GCS", ex);
        }
    }

    public async Task<bool> ExistsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var obj = await _client.GetObjectAsync(_options.BucketName, filePath, cancellationToken: cancellationToken);
            return obj != null;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence in GCS: {Path}", filePath);
            return false;
        }
    }

    public Task<string> GetPublicUrlAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://storage.googleapis.com/{_options.BucketName}/{filePath}";
        return Task.FromResult(url);
    }

    public async Task<string> GetSignedUrlAsync(
        string filePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var urlSigner = UrlSigner.FromCredential(await Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefaultAsync());
            
            var signedUrl = await urlSigner.SignAsync(
                _options.BucketName,
                filePath,
                expiration,
                HttpMethod.Get);

            return signedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for: {Path}", filePath);
            throw new StorageException("SIGNED_URL_FAILED", "Failed to generate signed URL", ex);
        }
    }

    public async Task<string> GenerateUploadSignedUrlAsync(
        string objectName,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var urlSigner = UrlSigner.FromCredential(await Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefaultAsync());
            
            var requestTemplate = UrlSigner.RequestTemplate
                .FromBucket(_options.BucketName)
                .WithObjectName(objectName)
                .WithHttpMethod(HttpMethod.Put)
                .WithContentHeaders(new Dictionary<string, IEnumerable<string>>
                {
                    ["Content-Type"] = new[] { contentType }
                });

            var signedUrl = await urlSigner.SignAsync(requestTemplate, UrlSigner.Options.FromExpiration(DateTimeOffset.UtcNow.Add(expiration)));

            return signedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate upload signed URL for: {Path}", objectName);
            throw new StorageException("SIGNED_URL_FAILED", "Failed to generate upload signed URL", ex);
        }
    }

    private string BuildObjectName(string fileName, string? customDirectory)
    {
        if (!string.IsNullOrEmpty(customDirectory))
        {
            return $"{customDirectory.Trim('/')}/{fileName}";
        }

        if (_options.UseDirectoryStructure)
        {
            var datePath = DateTime.UtcNow.ToString(_options.DirectoryFormat);
            return $"{datePath}/{fileName}";
        }

        return fileName;
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}
