using ACommerce.Files.Abstractions.Enums;
using ACommerce.Files.Abstractions.Exceptions;
using ACommerce.Files.Abstractions.Helpers;
using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Storage.AliyunOSS.Configuration;
using Aliyun.OSS;
using Aliyun.OSS.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACommerce.Files.Storage.AliyunOSS.Providers;

public class AliyunOSSStorageProvider : IStorageProvider
{
    private readonly AliyunOSSOptions _options;
    private readonly ILogger<AliyunOSSStorageProvider> _logger;
    private readonly OssClient _client;

    public string ProviderName => "AliyunOSS";
    public StorageType StorageType => StorageType.AliyunOSS;

    public AliyunOSSStorageProvider(
        IOptions<AliyunOSSOptions> options,
        ILogger<AliyunOSSStorageProvider> logger)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var conf = new ClientConfiguration();
        if (_options.UseV4Signature)
        {
            conf.SignatureVersion = SignatureVersion.V4;
        }

        _client = new OssClient(_options.Endpoint, _options.AccessKeyId, _options.AccessKeySecret, conf);

        if (_options.UseV4Signature && !string.IsNullOrEmpty(_options.Region))
        {
            _client.SetRegion(_options.Region);
        }

        _logger.LogInformation("Aliyun OSS Storage Provider initialized for bucket: {Bucket}", _options.BucketName);
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
            var objectKey = BuildObjectKey(uniqueFileName, directory);

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
                await Task.Run(() =>
                {
                    var result = _client.PutObject(_options.BucketName, objectKey, uploadStream);
                    _logger.LogInformation("File uploaded to OSS: {Key}, ETag: {ETag}", objectKey, result.ETag);
                }, cancellationToken);
            }
            finally
            {
                if (disposeStream)
                {
                    await uploadStream.DisposeAsync();
                }
            }

            return objectKey;
        }
        catch (OssException ex)
        {
            _logger.LogError(ex, "OSS error uploading file: {FileName}, Code: {Code}", fileName, ex.ErrorCode);
            throw new StorageException("OSS_UPLOAD_FAILED", $"Failed to upload file: {ex.ErrorCode}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file to OSS: {FileName}", fileName);
            throw new StorageException("SAVE_FAILED", "Failed to save file to OSS", ex);
        }
    }

    public async Task<Stream?> GetAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await ExistsAsync(filePath, cancellationToken);
            if (!exists)
            {
                _logger.LogWarning("File not found in OSS: {Path}", filePath);
                return null;
            }

            var memoryStream = new MemoryStream();
            await Task.Run(() =>
            {
                var result = _client.GetObject(_options.BucketName, filePath);
                using var responseStream = result.Content;
                responseStream.CopyTo(memoryStream);
                memoryStream.Position = 0;
            }, cancellationToken);

            return memoryStream;
        }
        catch (OssException ex) when (ex.ErrorCode == "NoSuchKey")
        {
            _logger.LogWarning("File not found in OSS: {Path}", filePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file from OSS: {Path}", filePath);
            throw new StorageException("GET_FAILED", "Failed to retrieve file from OSS", ex);
        }
    }

    public async Task<bool> DeleteAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await ExistsAsync(filePath, cancellationToken);
            if (!exists)
            {
                _logger.LogWarning("File not found for deletion in OSS: {Path}", filePath);
                return false;
            }

            await Task.Run(() =>
            {
                _client.DeleteObject(_options.BucketName, filePath);
            }, cancellationToken);

            _logger.LogInformation("File deleted from OSS: {Path}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file from OSS: {Path}", filePath);
            throw new StorageException("DELETE_FAILED", "Failed to delete file from OSS", ex);
        }
    }

    public Task<bool> ExistsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = _client.DoesObjectExist(_options.BucketName, filePath);
            return Task.FromResult(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check file existence in OSS: {Path}", filePath);
            return Task.FromResult(false);
        }
    }

    public Task<string> GetPublicUrlAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string baseUrl;
        if (!string.IsNullOrEmpty(_options.CustomDomain))
        {
            var protocol = _options.UseHttps ? "https" : "http";
            baseUrl = $"{protocol}://{_options.CustomDomain}";
        }
        else
        {
            var protocol = _options.UseHttps ? "https" : "http";
            baseUrl = $"{protocol}://{_options.BucketName}.{_options.Endpoint.Replace("https://", "").Replace("http://", "")}";
        }

        var url = $"{baseUrl.TrimEnd('/')}/{filePath}";
        return Task.FromResult(url);
    }

    public Task<string> GetSignedUrlAsync(
        string filePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var expirationTime = DateTime.UtcNow.Add(expiration);

            var request = new GeneratePresignedUriRequest(_options.BucketName, filePath, SignHttpMethod.Get)
            {
                Expiration = expirationTime
            };

            var signedUri = _client.GeneratePresignedUri(request);
            return Task.FromResult(signedUri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate signed URL for: {Path}", filePath);
            throw new StorageException("SIGNED_URL_FAILED", "Failed to generate signed URL", ex);
        }
    }

    private string BuildObjectKey(string fileName, string? customDirectory)
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
}
