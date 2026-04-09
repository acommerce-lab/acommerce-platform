# ACommerce.Files.Storage.Local

## نظرة عامة
تنفيذ التخزين المحلي للملفات. يحفظ الملفات على نظام الملفات المحلي مع دعم هيكلة المجلدات.

## الموقع
`/Files/ACommerce.Files.Storage.Local`

## التبعيات
- `ACommerce.Files.Abstractions`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Logging`

---

## الخدمات (Services)

### LocalStorageProvider

```csharp
public class LocalStorageProvider : IStorageProvider
{
    public string ProviderName => "Local";
    public StorageType StorageType => StorageType.Local;
}
```

---

## الخيارات (Options)

### LocalStorageOptions

```csharp
public class LocalStorageOptions
{
    public required string RootPath { get; set; }
    public required string BaseUrl { get; set; }
    public bool UseDirectoryStructure { get; set; } = true;
    public string DirectoryFormat { get; set; } = "yyyy/MM/dd";
}
```

| الخاصية | النوع | الوصف |
|---------|------|-------|
| `RootPath` | `string` | المسار الجذري للملفات |
| `BaseUrl` | `string` | الرابط الأساسي للملفات |
| `UseDirectoryStructure` | `bool` | استخدام بنية مجلدات |
| `DirectoryFormat` | `string` | صيغة التاريخ للمجلدات |

---

## التنفيذات

### SaveAsync
حفظ ملف:

```csharp
public async Task<string> SaveAsync(
    Stream stream,
    string fileName,
    string? directory = null,
    CancellationToken cancellationToken = default)
{
    // إنشاء اسم فريد
    var uniqueFileName = FileNameHelper.GenerateUniqueFileName(fileName);

    // بناء المسار
    var relativePath = BuildRelativePath(uniqueFileName, directory);
    var fullPath = Path.Combine(_options.RootPath, relativePath);

    // التأكد من وجود المجلد
    var directoryPath = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrEmpty(directoryPath))
    {
        Directory.CreateDirectory(directoryPath);
    }

    // حفظ الملف
    await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
    await stream.CopyToAsync(fileStream, cancellationToken);

    return relativePath;
}
```

**بنية المجلدات:** `{directory}/yyyy/MM/dd/{unique-filename}`

### GetAsync
جلب ملف:

```csharp
public async Task<Stream?> GetAsync(
    string filePath,
    CancellationToken cancellationToken = default)
{
    var fullPath = Path.Combine(_options.RootPath, filePath);

    if (!File.Exists(fullPath))
    {
        return null;
    }

    var memoryStream = new MemoryStream();
    await using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
    await fileStream.CopyToAsync(memoryStream, cancellationToken);
    memoryStream.Position = 0;

    return memoryStream;
}
```

### GetPublicUrlAsync
الحصول على رابط عام:

```csharp
public Task<string> GetPublicUrlAsync(string filePath, CancellationToken ct = default)
{
    var normalizedPath = filePath.Replace("\\", "/");
    var url = $"{_options.BaseUrl.TrimEnd('/')}/{normalizedPath}";
    return Task.FromResult(url);
}
```

### GetSignedUrlAsync
الحصول على رابط موقع:

```csharp
public Task<string> GetSignedUrlAsync(
    string filePath,
    TimeSpan expiration,
    CancellationToken cancellationToken = default)
{
    // Local storage لا يدعم signed URLs
    // يجب تنفيذ signed URLs عبر middleware في ASP.NET Core
    return GetPublicUrlAsync(filePath, cancellationToken);
}
```

---

## المزودات الإضافية

### InMemoryFileProvider
مزود ذاكرة للاختبار:

```csharp
public class InMemoryFileProvider : IStorageProvider
{
    public string ProviderName => "InMemory";
    public StorageType StorageType => StorageType.Local;

    private readonly ConcurrentDictionary<string, byte[]> _files = new();
}
```

---

## بنية الملفات
```
ACommerce.Files.Storage.Local/
├── Configuration/
│   └── LocalStorageOptions.cs
├── Providers/
│   ├── LocalStorageProvider.cs
│   └── InMemoryFileProvider.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── Middleware/
    └── StaticFilesMiddlewareExtensions.cs
```

---

## الإعدادات

### appsettings.json
```json
{
  "LocalStorage": {
    "RootPath": "wwwroot/uploads",
    "BaseUrl": "https://example.com/uploads",
    "UseDirectoryStructure": true,
    "DirectoryFormat": "yyyy/MM/dd"
  }
}
```

### تسجيل الخدمة
```csharp
services.Configure<LocalStorageOptions>(configuration.GetSection("LocalStorage"));
services.AddScoped<IStorageProvider, LocalStorageProvider>();
```

---

## مثال استخدام

### رفع صورة منتج
```csharp
var provider = serviceProvider.GetRequiredService<IStorageProvider>();

await using var stream = file.OpenReadStream();
var path = await provider.SaveAsync(stream, file.FileName, "products/images");

// path = "products/images/2024/01/15/abc123-product.jpg"
var url = await provider.GetPublicUrlAsync(path);
// url = "https://example.com/uploads/products/images/2024/01/15/abc123-product.jpg"
```

### حذف ملف
```csharp
await provider.DeleteAsync("products/images/2024/01/15/abc123-product.jpg");
```

---

## ملاحظات تقنية

1. **Directory Structure**: تنظيم الملفات بالتاريخ
2. **Unique Names**: إنشاء أسماء فريدة تلقائياً
3. **No Signed URLs**: يحتاج middleware للـ signed URLs
4. **InMemory Provider**: للاختبار والتطوير
5. **Static Files Middleware**: دعم StaticFiles في ASP.NET Core
