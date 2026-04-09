using ACommerce.Files.Operations;
using ACommerce.Files.Operations.Abstractions;
using ACommerce.OperationEngine.Wire;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Ashare.Api2.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Ashare.Api2.Controllers;

[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly FileService _files;
    private readonly IBaseAsyncRepository<MediaFile> _repo;
    private readonly ILogger<MediaController> _logger;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
    };

    private static readonly HashSet<string> AllowedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "listings", "profiles", "vendors", "messages"
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public MediaController(FileService files, IRepositoryFactory factory, ILogger<MediaController> logger)
    {
        _files = files;
        _repo  = factory.CreateRepository<MediaFile>();
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string directory = "listings",
        [FromQuery] Guid? uploaderId = null,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return this.BadRequestEnvelope("no_file");

        if (file.Length > MaxFileSizeBytes)
            return this.BadRequestEnvelope("file_too_large", $"max bytes: {MaxFileSizeBytes}");

        if (!AllowedContentTypes.Contains(file.ContentType))
            return this.BadRequestEnvelope("invalid_content_type", string.Join(",", AllowedContentTypes));

        if (!AllowedDirectories.Contains(directory))
            return this.BadRequestEnvelope("invalid_directory", string.Join(",", AllowedDirectories));

        var uploader = uploaderId ?? Guid.Empty;

        await using var stream = file.OpenReadStream();
        var request = new FileUploadRequest(
            Content: stream,
            FileName: file.FileName,
            ContentType: file.ContentType,
            SizeBytes: file.Length,
            Directory: directory,
            UploaderId: uploader);

        var outcome = await _files.UploadAsync(uploader, request, ct);

        if (!outcome.Succeeded)
            return this.BadRequestEnvelope("upload_failed", outcome.Error);

        var media = new MediaFile
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UploaderId = uploader,
            FileName = file.FileName,
            FilePath = outcome.FilePath!,
            PublicUrl = outcome.PublicUrl!,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            Directory = directory,
            Provider = _files.ProviderName
        };
        await _repo.AddAsync(media, ct);

        return this.OkEnvelope("media.upload", media);
    }

    [HttpPost("upload/multiple")]
    [RequestSizeLimit(MaxFileSizeBytes * 5)]
    public async Task<IActionResult> UploadMultiple(
        IFormFileCollection files,
        [FromQuery] string directory = "listings",
        [FromQuery] Guid? uploaderId = null,
        CancellationToken ct = default)
    {
        if (files == null || files.Count == 0)
            return this.BadRequestEnvelope("no_files");

        var results = new List<object>();
        var errors = new List<object>();

        foreach (var file in files)
        {
            if (file.Length == 0 ||
                file.Length > MaxFileSizeBytes ||
                !AllowedContentTypes.Contains(file.ContentType))
            {
                errors.Add(new { fileName = file.FileName, error = "invalid_file" });
                continue;
            }

            await using var stream = file.OpenReadStream();
            var req = new FileUploadRequest(stream, file.FileName, file.ContentType, file.Length, directory, uploaderId);
            var outcome = await _files.UploadAsync(uploaderId ?? Guid.Empty, req, ct);

            if (outcome.Succeeded)
            {
                var media = new MediaFile
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    UploaderId = uploaderId ?? Guid.Empty,
                    FileName = file.FileName,
                    FilePath = outcome.FilePath!,
                    PublicUrl = outcome.PublicUrl!,
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    Directory = directory,
                    Provider = _files.ProviderName
                };
                await _repo.AddAsync(media, ct);
                results.Add(new { id = media.Id, url = media.PublicUrl, fileName = file.FileName });
            }
            else
            {
                errors.Add(new { fileName = file.FileName, error = outcome.Error });
            }
        }

        return this.OkEnvelope("media.upload_multiple", new { uploaded = results, failed = errors });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var media = await _repo.GetByIdAsync(id, ct);
        return media == null ? this.NotFoundEnvelope("media_not_found") : this.OkEnvelope("media.get", media);
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> ByUser(Guid userId, CancellationToken ct)
    {
        var list = await _repo.GetAllWithPredicateAsync(m => m.UploaderId == userId);
        return this.OkEnvelope("media.list", list.ToList());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? actorId = null, CancellationToken ct = default)
    {
        var media = await _repo.GetByIdAsync(id, ct);
        if (media == null) return this.NotFoundEnvelope("media_not_found");

        var deleted = await _files.DeleteAsync(actorId ?? media.UploaderId, media.FilePath, ct);
        if (!deleted)
            return StatusCode(500, OperationEnvelopeFactory.Error<object>("storage_delete_failed"));

        await _repo.SoftDeleteAsync(id, ct);
        return this.NoContentEnvelope("media.delete");
    }
}
