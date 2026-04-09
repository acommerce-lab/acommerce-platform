# ACommerce.Files.ImageProcessing

## نظرة عامة
تنفيذ معالجة الصور باستخدام ImageSharp. يوفر عمليات تغيير الحجم، القص، العلامات المائية، وتحويل الصيغ.

## الموقع
`/Files/ACommerce.Files.ImageProcessing`

## التبعيات
- `ACommerce.Files.Abstractions`
- `SixLabors.ImageSharp`
- `SixLabors.ImageSharp.Drawing`
- `SixLabors.Fonts`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Logging`

---

## الخدمات (Services)

### ImageSharpProcessor

```csharp
public class ImageSharpProcessor : IImageProcessor
{
    public string ProviderName => "ImageSharp";
}
```

---

## الإعدادات (Settings)

### ImageProcessorSettings

```csharp
public class ImageProcessorSettings
{
    public int DefaultQuality { get; set; } = 85;
    public int ThumbnailQuality { get; set; } = 75;
    public int WatermarkFontSize { get; set; } = 24;
    public float WatermarkOpacity { get; set; } = 0.5f;
    public WatermarkPosition WatermarkPosition { get; set; } = WatermarkPosition.BottomRight;
}
```

### WatermarkPosition

| القيمة | الوصف |
|--------|-------|
| `TopLeft` | أعلى يسار |
| `TopCenter` | أعلى وسط |
| `TopRight` | أعلى يمين |
| `MiddleLeft` | وسط يسار |
| `MiddleCenter` | وسط |
| `MiddleRight` | وسط يمين |
| `BottomLeft` | أسفل يسار |
| `BottomCenter` | أسفل وسط |
| `BottomRight` | أسفل يمين |

---

## التنفيذات

### ResizeAsync
تغيير حجم الصورة:

```csharp
public async Task<Stream> ResizeAsync(
    Stream inputStream,
    int width, int height,
    bool maintainAspectRatio = true,
    CancellationToken cancellationToken = default)
{
    using var image = await Image.LoadAsync(inputStream, cancellationToken);

    var resizeOptions = new ResizeOptions
    {
        Size = new Size(width, height),
        Mode = maintainAspectRatio ? ResizeMode.Max : ResizeMode.Stretch
    };

    image.Mutate(x => x.Resize(resizeOptions));
    // ...
}
```

**أوضاع التغيير:**
- `ResizeMode.Max` - يحافظ على التناسب
- `ResizeMode.Stretch` - يمط الصورة

### CreateThumbnailAsync
إنشاء صورة مصغرة:

```csharp
public async Task<Stream> CreateThumbnailAsync(
    Stream inputStream,
    int size = 150,
    CancellationToken cancellationToken = default)
{
    image.Mutate(x => x.Resize(new ResizeOptions
    {
        Size = new Size(size, size),
        Mode = ResizeMode.Crop  // قص مربع
    }));
}
```

### CropAsync
قص الصورة:

```csharp
public async Task<Stream> CropAsync(
    Stream inputStream,
    int x, int y, int width, int height,
    CancellationToken cancellationToken = default)
{
    var cropRectangle = new Rectangle(x, y, width, height);
    image.Mutate(i => i.Crop(cropRectangle));
}
```

### AddWatermarkAsync
إضافة علامة مائية:

```csharp
public async Task<Stream> AddWatermarkAsync(
    Stream inputStream,
    string watermarkText,
    CancellationToken cancellationToken = default)
{
    var font = fontFamily.CreateFont(_settings.WatermarkFontSize, FontStyle.Bold);
    var color = Color.White.WithAlpha(_settings.WatermarkOpacity);

    image.Mutate(x => x.DrawText(textOptions, watermarkText, color));
}
```

### ConvertFormatAsync
تحويل صيغة الصورة:

```csharp
public async Task<Stream> ConvertFormatAsync(
    Stream inputStream,
    ImageFormat format,
    int quality = 85,
    CancellationToken cancellationToken = default)
{
    IImageEncoder encoder = format switch
    {
        ImageFormat.Jpeg => new JpegEncoder { Quality = quality },
        ImageFormat.Png => new PngEncoder(),
        ImageFormat.WebP => new WebpEncoder { Quality = quality },
        _ => new JpegEncoder { Quality = quality }
    };

    await image.SaveAsync(outputStream, encoder, cancellationToken);
}
```

### GetImageInfoAsync
الحصول على معلومات الصورة:

```csharp
public async Task<ImageInfo> GetImageInfoAsync(
    Stream inputStream,
    CancellationToken cancellationToken = default)
{
    var imageInfo = await Image.IdentifyAsync(inputStream, cancellationToken);

    return new ImageInfo
    {
        Width = imageInfo.Width,
        Height = imageInfo.Height,
        Format = imageInfo.Metadata.DecodedImageFormat?.Name ?? "Unknown",
        SizeInBytes = inputStream.Length
    };
}
```

---

## بنية الملفات
```
ACommerce.Files.ImageProcessing/
├── Configuration/
│   └── ImageProcessingOptions.cs
├── Providers/
│   └── ImageSharpProcessor.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

---

## الإعدادات

### appsettings.json
```json
{
  "ImageProcessing": {
    "DefaultQuality": 85,
    "ThumbnailQuality": 75,
    "WatermarkFontSize": 24,
    "WatermarkOpacity": 0.5,
    "WatermarkPosition": "BottomRight"
  }
}
```

### تسجيل الخدمة
```csharp
services.Configure<ImageProcessorSettings>(configuration.GetSection("ImageProcessing"));
services.AddScoped<IImageProcessor, ImageSharpProcessor>();
```

---

## مثال استخدام

### معالجة صورة منتج
```csharp
var processor = serviceProvider.GetRequiredService<IImageProcessor>();

// تغيير الحجم
await using var resized = await processor.ResizeAsync(imageStream, 800, 600);

// إنشاء صورة مصغرة
await using var thumbnail = await processor.CreateThumbnailAsync(imageStream, 200);

// تحويل إلى WebP
await using var webp = await processor.ConvertFormatAsync(imageStream, ImageFormat.WebP, 90);

// إضافة علامة مائية
await using var watermarked = await processor.AddWatermarkAsync(imageStream, "© MyStore");
```

### معالجة متعددة الخطوات
```csharp
var options = new ImageProcessingOptions
{
    Width = 1200,
    Height = 800,
    MaintainAspectRatio = true,
    WatermarkText = "© 2024 MyStore",
    Format = ImageFormat.WebP,
    Quality = 85
};

await using var processed = await processor.ProcessAsync(imageStream, options);
```

---

## ملاحظات تقنية

1. **ImageSharp**: مكتبة .NET أصلية للصور
2. **Format Support**: JPEG, PNG, WebP, GIF
3. **Watermark Positioning**: 9 مواقع للعلامة المائية
4. **Quality Control**: تحكم بجودة الضغط
5. **Aspect Ratio**: دعم الحفاظ على التناسب
6. **Memory Efficient**: استخدام Streams
