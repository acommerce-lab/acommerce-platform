using System.ComponentModel.DataAnnotations;

namespace ACommerce.Files.Storage.AliyunOSS.Configuration;

public class AliyunOSSOptions
{
    public const string SectionName = "Files:Storage:AliyunOSS";

    [Required(ErrorMessage = "Access Key ID is required")]
    public string AccessKeyId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Access Key Secret is required")]
    public string AccessKeySecret { get; set; } = string.Empty;

    [Required(ErrorMessage = "Endpoint is required")]
    public string Endpoint { get; set; } = string.Empty;

    [Required(ErrorMessage = "Region is required")]
    public string Region { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bucket name is required")]
    public string BucketName { get; set; } = string.Empty;

    public string? CustomDomain { get; set; }

    public bool UseHttps { get; set; } = true;

    public bool UseV4Signature { get; set; } = true;

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
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };
}
