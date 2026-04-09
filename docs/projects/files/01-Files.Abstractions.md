# ACommerce.Files.Abstractions

## نظرة عامة
تجريدات نظام الملفات والتخزين. توفر واجهات موحدة لتخزين الملفات ومعالجة الصور مع دعم مزودين متعددين.

## الموقع
`/Files/ACommerce.Files.Abstractions`

## التبعيات
- لا توجد تبعيات خارجية (مكتبة تجريدات)

---

## الواجهات (Providers)

### IStorageProvider
واجهة مزود التخزين:

```csharp
public interface IStorageProvider
{
    // اسم المزود
    string ProviderName { get; }

    // نوع التخزين
    StorageType StorageType { get; }

    // حفظ ملف
    Task<string> SaveAsync(
        Stream stream,
        string fileName,
        string? directory = null,
        CancellationToken cancellationToken = default);

    // جلب ملف
    Task<Stream?> GetAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    // حذف ملف
    Task<bool> DeleteAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    // التحقق من وجود ملف
    Task<bool> ExistsAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    // الحصول على رابط عام
    Task<string> GetPublicUrlAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    // الحصول على رابط موقع (مؤقت)
    Task<string> GetSignedUrlAsync(
        string filePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);
}
```

### IImageProcessor
واجهة معالجة الصور:

```csharp
public interface IImageProcessor
{
    string ProviderName { get; }

    // تغيير حجم الصورة
    Task<Stream> ResizeAsync(
        Stream inputStream,
        int width, int height,
        bool maintainAspectRatio = true,
        CancellationToken cancellationToken = default);

    // إنشاء صورة مصغرة
    Task<Stream> CreateThumbnailAsync(
        Stream inputStream,
        int size = 150,
        CancellationToken cancellationToken = default);

    // قص الصورة
    Task<Stream> CropAsync(
        Stream inputStream,
        int x, int y, int width, int height,
        CancellationToken cancellationToken = default);

    // إضافة علامة مائية
    Task<Stream> AddWatermarkAsync(
        Stream inputStream,
        string watermarkText,
        CancellationToken cancellationToken = default);

    // تحويل الصيغة
    Task<Stream> ConvertFormatAsync(
        Stream inputStream,
        ImageFormat format,
        int quality = 85,
        CancellationToken cancellationToken = default);

    // معالجة بخيارات متعددة
    Task<Stream> ProcessAsync(
        Stream inputStream,
        ImageProcessingOptions options,
        CancellationToken cancellationToken = default);

    // الحصول على معلومات الصورة
    Task<ImageInfo> GetImageInfoAsync(
        Stream inputStream,
        CancellationToken cancellationToken = default);
}
```

---

## النماذج (Models)

### UploadRequest
طلب رفع ملف:

```csharp
public record UploadRequest
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public string? OwnerId { get; init; }
    public string? Directory { get; init; }
    public bool GenerateThumbnail { get; init; } = true;
    public Dictionary<string, string>? Metadata { get; init; }
}
```

### UploadResult
نتيجة رفع ملف:

```csharp
public record UploadResult
{
    public required bool Success { get; init; }
    public FileInfo? File { get; init; }
    public FileError? Error { get; init; }
}
```

### FileInfo
معلومات الملف:

```csharp
public record FileInfo
{
    public required string FileId { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
    public required FileType FileType { get; init; }
    public required string StoragePath { get; init; }
    public required string PublicUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public int? Width { get; init; }        // للصور
    public int? Height { get; init; }       // للصور
    public string? OwnerId { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
    public DateTimeOffset UploadedAt { get; init; }
}
```

---

## التعدادات (Enums)

### StorageType

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Local` | 1 | تخزين محلي |
| `Azure` | 2 | Azure Blob Storage |
| `S3` | 3 | Amazon S3 |
| `GoogleCloud` | 4 | Google Cloud Storage |

### FileType

| القيمة | الرقم | الوصف |
|--------|------|-------|
| `Unknown` | 0 | غير معروف |
| `Image` | 1 | صورة |
| `Document` | 2 | مستند |
| `Video` | 3 | فيديو |
| `Audio` | 4 | صوت |
| `Archive` | 5 | أرشيف |
| `Other` | 6 | أخرى |

### ImageFormat

| القيمة | الوصف |
|--------|-------|
| `Jpeg` | JPEG |
| `Png` | PNG |
| `WebP` | WebP |
| `Gif` | GIF |

---

## بنية الملفات
```
ACommerce.Files.Abstractions/
├── Providers/
│   ├── IStorageProvider.cs
│   ├── IImageProcessor.cs
│   └── ImageInfo.cs
├── Models/
│   ├── UploadRequest.cs
│   ├── UploadResult.cs
│   ├── FileInfo.cs
│   ├── FileError.cs
│   └── ImageProcessingOptions.cs
├── Enums/
│   ├── StorageType.cs
│   ├── FileType.cs
│   └── ImageFormat.cs
├── Helpers/
│   ├── FileTypeHelper.cs
│   └── FileNameHelper.cs
└── Exceptions/
    ├── FileException.cs
    ├── StorageException.cs
    └── ImageProcessingException.cs
```

---

## مثال استخدام

### رفع ملف
```csharp
var result = await storageProvider.SaveAsync(
    fileStream,
    "product-image.jpg",
    "products/images"
);

var publicUrl = await storageProvider.GetPublicUrlAsync(result);
```

### معالجة صورة
```csharp
var thumbnail = await imageProcessor.CreateThumbnailAsync(imageStream, 200);
var resized = await imageProcessor.ResizeAsync(imageStream, 800, 600);
var webp = await imageProcessor.ConvertFormatAsync(imageStream, ImageFormat.WebP, 90);
```

---

## التنفيذات المتاحة
- `ACommerce.Files.Storage.Local` - تخزين محلي
- `ACommerce.Files.ImageProcessing` - معالجة الصور بـ ImageSharp

---

## ملاحظات تقنية

1. **Provider Pattern**: يدعم تبديل مزودي التخزين
2. **Record Types**: استخدام records للـ immutability
3. **Signed URLs**: دعم روابط مؤقتة للأمان
4. **Image Processing**: معالجة صور متكاملة
5. **Thumbnail Support**: إنشاء صور مصغرة تلقائياً
