using System.ComponentModel.DataAnnotations;

namespace ACommerce.Files.Storage.GoogleCloud.Configuration;

public class GoogleCloudStorageOptions
{
    public const string SectionName = "Files:Storage:GoogleCloud";

    [Required(ErrorMessage = "Bucket name is required")]
    public string BucketName { get; set; } = string.Empty;

    public string? ProjectId { get; set; }

    public string? CredentialsPath { get; set; }

    public bool UseDirectoryStructure { get; set; } = true;

    public string DirectoryFormat { get; set; } = "yyyy/MM/dd";

    public int SignedUrlExpirationMinutes { get; set; } = 60;

    public long MaxFileSizeInBytes { get; set; } = 10 * 1024 * 1024;

    public List<string> AllowedContentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/gif",
        "image/webp",
        "application/pdf"
    };
}
