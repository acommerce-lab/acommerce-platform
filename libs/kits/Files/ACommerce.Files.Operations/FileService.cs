using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Operations.Abstractions;
using ACommerce.Files.Operations.Operations;
using ACommerce.OperationEngine.Core;

namespace ACommerce.Files.Operations;

/// <summary>
/// واجهة المطور البسيطة لعمليات الملفات.
/// تنفّذ كل عملية كقيد محاسبي عبر OperationEngine ثم تستدعي IStorageProvider.
/// </summary>
public class FileService
{
    private readonly IStorageProvider _storage;
    private readonly OpEngine _engine;

    public FileService(IStorageProvider storage, OpEngine engine)
    {
        _storage = storage;
        _engine = engine;
    }

    public string ProviderName => _storage.ProviderName;

    public async Task<FileUploadOutcome> UploadAsync(
        Guid uploaderId,
        FileUploadRequest request,
        CancellationToken ct = default)
    {
        var op = FileOps.Upload(FilePartyId.User(uploaderId.ToString()), request, _storage);
        var result = await _engine.ExecuteAsync(op, ct);

        if (!result.Success)
        {
            result.Context!.TryGet<string>("error", out var err);
            return new FileUploadOutcome(false, null, null, err ?? "upload_failed");
        }

        result.Context!.TryGet<string>("filePath", out var path);
        result.Context!.TryGet<string>("publicUrl", out var url);
        return new FileUploadOutcome(true, path, url, null, request.SizeBytes);
    }

    public async Task<bool> DeleteAsync(Guid actorId, string filePath, CancellationToken ct = default)
    {
        var op = FileOps.Delete(FilePartyId.User(actorId.ToString()), filePath, _storage);
        var result = await _engine.ExecuteAsync(op, ct);
        return result.Success;
    }

    public async Task<Stream?> DownloadAsync(Guid readerId, string filePath, CancellationToken ct = default)
    {
        var op = FileOps.Download(FilePartyId.User(readerId.ToString()), filePath, _storage);
        var result = await _engine.ExecuteAsync(op, ct);
        if (!result.Success) return null;
        return result.Context!.TryGet<Stream>("stream", out var s) ? s : null;
    }

    public Task<bool> ExistsAsync(string filePath, CancellationToken ct = default)
        => _storage.ExistsAsync(filePath, ct);

    public Task<string> GetPublicUrlAsync(string filePath, CancellationToken ct = default)
        => _storage.GetPublicUrlAsync(filePath, ct);
}
