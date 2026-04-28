using ACommerce.Files.Abstractions.Providers;
using ACommerce.Files.Operations.Abstractions;
using ACommerce.OperationEngine.Analyzers;
using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Files.Operations.Operations;

/// <summary>
/// قيود الملفات - كل عملية = قيد بين المستخدم والمخزن.
///
/// Upload:   User (مدين) ← Storage (دائن) بحجم الملف.
/// Download: Storage (مدين) ← User (دائن).
/// Delete:   Storage (مدين) ← System (دائن).
/// </summary>
public static class FileOps
{
    /// <summary>
    /// قيد رفع ملف.
    /// </summary>
    public static Operation Upload(
        FilePartyId uploader,
        FileUploadRequest request,
        IStorageProvider storage)
    {
        return Entry.Create("file.upload")
            .Describe($"{uploader} uploads {request.FileName} ({request.SizeBytes} bytes)")
            .From(uploader, request.SizeBytes,
                (FileTags.Role, "uploader"),
                (FileTags.Status, FileStatus.Pending))
            .To(FilePartyId.Storage(storage.ProviderName), request.SizeBytes,
                (FileTags.Role, "storage"),
                (FileTags.Status, FileStatus.Pending))
            .Tag(FileTags.Provider, storage.ProviderName)
            .Tag(FileTags.Operation, FileOperationType.Upload)
            .Tag(FileTags.FileName, request.FileName)
            .Tag(FileTags.ContentType, request.ContentType)
            .Tag(FileTags.SizeBytes, request.SizeBytes.ToString())
            .Tag(FileTags.Directory, request.Directory ?? "default")
            // محللات ما قبل النواة بدل .Validate
            .Analyze(new RangeAnalyzer("size_bytes", () => request.SizeBytes, min: 1))
            .Analyze(new RequiredFieldAnalyzer("file_name", () => request.FileName))
            .Analyze(new RequiredFieldAnalyzer("content_type", () => request.ContentType))
            .Execute(async ctx =>
            {
                try
                {
                    var path = await storage.SaveAsync(
                        request.Content,
                        request.FileName,
                        request.Directory,
                        ctx.CancellationToken);

                    var url = await storage.GetPublicUrlAsync(path, ctx.CancellationToken);

                    ctx.Set("filePath", path);
                    ctx.Set("publicUrl", url);

                    var storageParty = ctx.Operation.GetPartiesByTag(FileTags.Role, "storage").FirstOrDefault();
                    if (storageParty != null)
                    {
                        storageParty.RemoveTag(FileTags.Status);
                        storageParty.AddTag(FileTags.Status, FileStatus.Stored);
                        storageParty.AddTag(FileTags.Url, url);
                    }
                }
                catch (Exception ex)
                {
                    ctx.Set("error", ex.Message);
                    throw;
                }
            })
            .Build();
    }

    /// <summary>
    /// قيد حذف ملف.
    /// </summary>
    public static Operation Delete(
        FilePartyId actor,
        string filePath,
        IStorageProvider storage)
    {
        return Entry.Create("file.delete")
            .Describe($"{actor} deletes {filePath}")
            .From(FilePartyId.Storage(storage.ProviderName), 1,
                (FileTags.Role, "storage"),
                (FileTags.Status, FileStatus.Stored))
            .To(actor, 1, (FileTags.Role, "actor"))
            .Tag(FileTags.Provider, storage.ProviderName)
            .Tag(FileTags.Operation, FileOperationType.Delete)
            .Tag(FileTags.FileName, filePath)
            .Execute(async ctx =>
            {
                var deleted = await storage.DeleteAsync(filePath, ctx.CancellationToken);
                ctx.Set("deleted", deleted);

                var storageParty = ctx.Operation.GetPartiesByTag(FileTags.Role, "storage").FirstOrDefault();
                if (storageParty != null)
                {
                    storageParty.RemoveTag(FileTags.Status);
                    storageParty.AddTag(FileTags.Status, deleted ? FileStatus.Deleted : FileStatus.Failed);
                }

                if (!deleted)
                    throw new InvalidOperationException("storage_delete_failed");
            })
            .Build();
    }

    /// <summary>
    /// قيد قراءة ملف (للتدقيق).
    /// </summary>
    public static Operation Download(
        FilePartyId reader,
        string filePath,
        IStorageProvider storage)
    {
        return Entry.Create("file.download")
            .Describe($"{reader} downloads {filePath}")
            .From(FilePartyId.Storage(storage.ProviderName), 1, (FileTags.Role, "storage"))
            .To(reader, 1, (FileTags.Role, "reader"))
            .Tag(FileTags.Provider, storage.ProviderName)
            .Tag(FileTags.Operation, FileOperationType.Download)
            .Tag(FileTags.FileName, filePath)
            .Execute(async ctx =>
            {
                var stream = await storage.GetAsync(filePath, ctx.CancellationToken);
                if (stream == null)
                    throw new FileNotFoundException(filePath);
                ctx.Set("stream", stream);
            })
            .Build();
    }
}
