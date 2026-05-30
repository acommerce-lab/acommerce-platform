using System.Data;
using System.Text.Json;
using ACommerce.Catalog.Attributes.Entities;
using ACommerce.Catalog.Listings.Entities;
using ACommerce.Catalog.Listings.Enums;
using ACommerce.Catalog.Products.Entities;
using ACommerce.Catalog.Products.Enums;
using ACommerce.Files.Abstractions.Providers;
using ACommerce.SharedKernel.Abstractions.Repositories;
using ACommerce.SharedKernel.Infrastructure.EFCores.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Ashare.Legacy.SchoolSeeder;

/// <summary>
/// مُنَسِّق البَذر: إيجاد المالِك → نَسخ احتياطيّ → تَعطيل القَديم (حَذف
/// ناعِم) → بَذر فئات/خصائص/رَبط المَدارِس → بَذر عُروض عَيِّنَة مَع الصُّوَر.
///
/// <para>آمِن افتراضيّاً: في وَضع dry-run يَطبَع فَقَط. التَّطبيق الفِعليّ
/// (<c>apply=true</c>) يَجري داخِل transaction واحِدَة عَبر execution
/// strategy (لِأَنّ DbContext الإنتاج يُفَعِّل EnableRetryOnFailure).</para>
/// </summary>
public sealed class LegacySeeder
{
    private readonly IRepositoryFactory _repos;
    private readonly ApplicationDbContext _db;
    private readonly IStorageProvider? _storage;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly bool _apply;
    private readonly DateTime _now = DateTime.UtcNow;

    // UnsafeRelaxedJsonEscaping لِكَي تُكتَب العَرَبيّة كَما هي (لا \uXXXX).
    private static readonly JsonSerializerOptions _jsonAr = new()
    { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public LegacySeeder(
        IRepositoryFactory repos, ApplicationDbContext db,
        IStorageProvider? storage, IHttpClientFactory httpFactory,
        IConfiguration config, bool apply)
    {
        _repos = repos;
        _db = db;
        _storage = storage;
        _http = httpFactory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(120);
        _config = config;
        _apply = apply;
    }

    private static void Log(string msg) => Console.WriteLine(msg);
    private void Plan(string msg) => Console.WriteLine($"  {(_apply ? "✔" : "•")} {msg}");

    public async Task RunAsync(CancellationToken ct)
    {
        Log($"\n=== بَذر مَدارِس عشير القديم — الوَضع: {(_apply ? "APPLY (كِتابَة فِعليّة)" : "DRY-RUN (مُعايَنَة فَقَط)")} ===\n");

        // تَحَقُّق مُبَكِّر: لَو القاعِدَة الهَدَف بِلا مُخَطَّط (مَثلاً قاعِدَة
        // جَديدَة فارِغَة) نُعطي رِسالَة واضِحَة بَدَل "Invalid object name".
        // في APPLY = خَطَأ قاتِل. في dry-run = تَنبيه فَقَط ثُمَّ تَخطّي
        // مُعايَنَة البَذر، لِأَنّ rebuild dry-run لَم يُنشِئ المُخَطَّط بَعد
        // (الـ clone كانَ مُعايَنَةً) — وَفي التَّنفيذ الفِعليّ يُنشِئه clone أوّلاً.
        if (!await TableExistsAsync("ProductListing", ct))
        {
            const string msg = "القاعِدَة الهَدَف لا تَحوي جَداوِل عشير (ProductListing مَفقود). " +
                "إن كانَت قاعِدَة جَديدَة فارِغَة، شَغِّل SEEDER_MODE=clone (أو rebuild) لِنَسخ المُخَطَّط والبَيانات مِن الإنتاج.";
            if (_apply) throw new InvalidOperationException(msg);
            Log("ℹ️ " + msg);
            Log("   (dry-run) تَخطّي مُعايَنَة البَذر — في التَّنفيذ الفِعليّ يُنشِئ clone المُخَطَّط أوّلاً.");
            return;
        }

        var ownerId = await ResolveOwnerAsync(ct);
        Log($"المالِك المُختار لِلعُروض: {ownerId}\n");

        if (_apply)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                await SeedAllAsync(ownerId, ct);
                await tx.CommitAsync(ct);
            });
            Log("\n✅ اكتَمَل البَذر وَحُفِظَت التَّغييرات.");
        }
        else
        {
            await SeedAllAsync(ownerId, ct);
            Log("\nℹ️ DRY-RUN: لَم تُكتَب أَيّ تَغييرات. شَغِّل بِـ SEED_APPLY=true لِلتَّطبيق.");
        }
    }

    private async Task SeedAllAsync(Guid ownerId, CancellationToken ct)
    {
        await BackupBeforeDisableAsync(ct);
        if (_config.GetValue("Seeder:DisableOldTaxonomy", true))
            await DisableOldTaxonomyAsync(ct);
        await SeedCategoriesAsync(ct);
        await SeedAttributesAsync(ct);
        await SeedMappingsAsync(ct);
        await SeedListingsAsync(ownerId, ct);
    }

    // ═══ حَذف بَيانات المَدارِس (حَذف نِهائيّ — لِلقاعِدَة الجَديدَة) ════════
    // يُستَخدَم في خَطّ rebuild: بَعد نَسخ الإنتاج، نُزيل المَدارِس لِبَدء
    // نَظيف ثُمَّ نُعيد البَذر. حَذف نِهائيّ (لا ناعِم) لِأَنّ الهَدَف قاعِدَة
    // جَديدَة قابِلَة لِإعادَة البِناء. تَرتيب آمِن لِلـ FK.
    public async Task PurgeSchoolsAsync(CancellationToken ct)
    {
        Log("\n=== حَذف بَيانات المَدارِس ===");
        var catIds = SchoolSeedData.Categories(_now).Select(c => $"'{c.Id}'");
        var defIds = SchoolSeedData.NewAttributes().Select(a => $"'{a.Id}'");
        var c = string.Join(",", catIds);
        var d = string.Join(",", defIds);

        await ExecAsync($"DELETE FROM ProductListing WHERE CategoryId IN ({c})", ct);
        await ExecAsync("DELETE FROM Products WHERE Sku LIKE 'SCHOOL-%'", ct);
        await ExecAsync($"DELETE FROM CategoryAttributeMappings WHERE CategoryId IN ({c}) OR AttributeDefinitionId IN ({d})", ct);
        await ExecAsync($"DELETE FROM AttributeValues WHERE AttributeDefinitionId IN ({d})", ct);
        await ExecAsync($"DELETE FROM AttributeDefinitions WHERE Id IN ({d})", ct);
        await ExecAsync($"DELETE FROM ProductCategory WHERE Id IN ({c})", ct);
    }

    // ═══ إظهار/استِعادَة تَصنيف عشير القَديم ══════════════════════════════
    // يُعيد تَفعيل كُلّ فئات/رَوابِط/تَعريفات عشير القَديمَة + **قِيَم الخصائص**
    // (كانَت ناقِصَة سابِقاً — لِذا كانَت الفئات تَعود دونَ ظُهور خصائصها).
    // الآن يَشمَل: الفئات + الرَّبط + التَّعريفات + القِيَم، فَتَظهَر الخصائص
    // كامِلَةً عِندَ الاستِعادَة.
    public async Task RestoreLegacyAsync(CancellationToken ct)
    {
        Log("\n=== إظهار/استِعادَة تَصنيف عشير القَديم ===");
        var catIds = string.Join(",", SchoolSeedData.Categories(_now).Select(c => $"'{c.Id}'"));
        var defIds = string.Join(",", SchoolSeedData.NewAttributes().Select(a => $"'{a.Id}'"));

        // مُحَصَّن: يُفَعِّل بِغَضّ النَّظَر عَن كَيف أُخفِيَ الصَفّ (IsDeleted=1
        // أو IsActive=0). الـ GetAvailableCategories يَشتَرِط IsActive=1 +
        // غير محذوف، فَنَضمَن الاثنَين مَعاً.
        await ExecAsync($"UPDATE ProductCategory SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE Id NOT IN ({catIds}) AND (IsDeleted=1 OR IsActive=0)", ct);
        await ExecAsync($"UPDATE CategoryAttributeMappings SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId NOT IN ({catIds}) AND AttributeDefinitionId NOT IN ({defIds}) AND (IsDeleted=1 OR IsActive=0)", ct);
        await ExecAsync($"UPDATE AttributeDefinitions SET IsDeleted=0, UpdatedAt=SYSUTCDATETIME() WHERE Id NOT IN ({defIds}) AND Code NOT LIKE 'school[_]%' AND IsDeleted=1", ct);
        // قِيَم الخصائص القَديمَة — هذا ما كانَ يَنقُص فَلا تَظهَر الخصائص.
        await ExecAsync($"UPDATE AttributeValues SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId NOT IN ({defIds}) AND (IsDeleted=1 OR IsActive=0)", ct);
        // عُروض قَديمَة (غير مَدرَسيّة) — تَأكيد ظُهورها.
        await ExecAsync($"UPDATE ProductListing SET IsActive=1, IsDeleted=0, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId IS NOT NULL AND CategoryId NOT IN ({catIds}) AND (IsDeleted=1 OR IsActive=0)", ct);

        // تَشخيص: كَم فِئَة قَديمَة ظاهِرَة الآن (IsActive=1 + غير محذوفَة)؟
        var visible = await ScalarLongAsync(
            $"SELECT COUNT(*) FROM ProductCategory WHERE IsActive=1 AND IsDeleted=0 AND Id NOT IN ({catIds})", ct);
        Log($"  ► فئات قَديمَة ظاهِرَة الآن: {visible}");
        if (visible == 0)
            Log("  ⚠ صِفر! إمّا الفئات محذوفَة نِهائيّاً (لا soft-delete) فَتَحتاج إعادَة إنشاء، " +
                "أو أَنتَ على قاعِدَة غير الَّتي يَقرَؤها التَّطبيق، أو التَّطبيق يُخَبِّئ القائِمَة (أعِد التَّحميل/التَّثبيت).");
    }

    // ═══ إظهار/إخفاء المَدارِس ═════════════════════════════════════════════
    // toggle شامِل لِكُلّ كائِنات المَدارِس (فئات + عُروض + مُنتَجات + رَبط +
    // تَعريفات + قِيَم). الأب "مدارس" يَبقى غير فَعّال دائماً (يَظهَر فارِغاً
    // في الإنشاء). يَعمَل على القاعِدَة المُحَدَّدَة في DefaultConnection.
    public async Task SetSchoolsVisibilityAsync(bool visible, CancellationToken ct)
    {
        Log($"\n=== {(visible ? "إظهار" : "إخفاء")} المَدارِس ===");
        var parent = SchoolSeedData.Ids.CategoryParent;
        var allCats = SchoolSeedData.Categories(_now).Select(c => c.Id).ToList();
        var allCsv = string.Join(",", allCats.Select(id => $"'{id}'"));
        var childCsv = string.Join(",", allCats.Where(id => id != parent).Select(id => $"'{id}'"));
        var defCsv = string.Join(",", SchoolSeedData.NewAttributes().Select(a => $"'{a.Id}'"));
        int del = visible ? 0 : 1, act = visible ? 1 : 0;

        await ExecAsync($"UPDATE ProductCategory SET IsDeleted={del}, IsActive={act}, UpdatedAt=SYSUTCDATETIME() WHERE Id IN ({childCsv})", ct);
        await ExecAsync($"UPDATE ProductCategory SET IsDeleted={del}, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE Id='{parent}'", ct);
        await ExecAsync($"UPDATE ProductListing SET IsDeleted={del}, IsActive={act}, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId IN ({allCsv})", ct);
        await ExecAsync($"UPDATE Products SET IsDeleted={del}, UpdatedAt=SYSUTCDATETIME() WHERE Sku LIKE 'SCHOOL-%'", ct);
        await ExecAsync($"UPDATE CategoryAttributeMappings SET IsDeleted={del}, IsActive={act}, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId IN ({allCsv}) OR AttributeDefinitionId IN ({defCsv})", ct);
        await ExecAsync($"UPDATE AttributeDefinitions SET IsDeleted={del}, UpdatedAt=SYSUTCDATETIME() WHERE Id IN ({defCsv})", ct);
        await ExecAsync($"UPDATE AttributeValues SET IsDeleted={del}, IsActive={act}, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN ({defCsv})", ct);
    }

    // إخفاء القَديم = نَفس التَّعطيل الدَّقيق المُستَخدَم في seed (يُبقي خصائص
    // البروفايل سَليمَة، يُعَطِّل فَقَط ما رُبِطَ بِالفئات القَديمَة).
    public Task HideLegacyAsync(CancellationToken ct) => DisableOldTaxonomyAsync(ct);

    // ═══ إظهار مَجموعَتَي "عشير يَدور سكن" + "عشير عنده سكن" فَقَط ═══════════
    // يُطابِق بِالاسم العَرَبيّ — يَلتَقِط كُلّ الصُّفوف بِنَفس الاسم بِغَضّ
    // النَّظَر عَن الـ slug (looking-for-housing / looking-for-partner /
    // roommate_has / roommate_wants). يُفَعِّل: الفئات نَفسها + رَوابِطها +
    // التَّعريفات المُرتَبِطَة + قِيَمها + العُروض الَّتي CategoryId تَحتَها.
    private const string RoommateNamesSql = "N'عشير يدور سكن', N'عشير عنده سكن'";

    public async Task ShowRoommateGroupsAsync(CancellationToken ct)
    {
        Log("\n=== إظهار مَجموعَتَي عشير: يَدور سكن + عنده سكن ===");

        // الفئات
        await ExecAsync(
            $"UPDATE ProductCategory SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() " +
            $"WHERE Name IN ({RoommateNamesSql}) AND (IsDeleted=1 OR IsActive=0)", ct);

        // الرَّوابِط (مَع تَفَعيل أيّ تَعريف مَحذوف مُشار إليه + قِيَمه)
        await ExecAsync(
            $"UPDATE CategoryAttributeMappings SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() " +
            $"WHERE CategoryId IN (SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql})) " +
            $"AND (IsDeleted=1 OR IsActive=0)", ct);

        // تَعريفات الخصائص الَّتي تَستَعمِلها هاتانِ المَجموعَتان
        await ExecAsync(
            $"UPDATE AttributeDefinitions SET IsDeleted=0, UpdatedAt=SYSUTCDATETIME() " +
            $"WHERE IsDeleted=1 AND Id IN (" +
            $"SELECT AttributeDefinitionId FROM CategoryAttributeMappings " +
            $"WHERE CategoryId IN (SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql}))" +
            $")", ct);

        // قِيَم تِلكَ التَّعريفات
        await ExecAsync(
            $"UPDATE AttributeValues SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() " +
            $"WHERE AttributeDefinitionId IN (" +
            $"SELECT AttributeDefinitionId FROM CategoryAttributeMappings " +
            $"WHERE CategoryId IN (SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql}))" +
            $") AND (IsDeleted=1 OR IsActive=0)", ct);

        // العُروض تَحتَ هاتَين المَجموعَتَين
        await ExecAsync(
            $"UPDATE ProductListing SET IsActive=1, IsDeleted=0, UpdatedAt=SYSUTCDATETIME() " +
            $"WHERE CategoryId IN (SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql})) " +
            $"AND (IsDeleted=1 OR IsActive=0)", ct);

        // تَشخيص
        var catCount = await ScalarLongAsync(
            $"SELECT COUNT(*) FROM ProductCategory WHERE Name IN ({RoommateNamesSql}) AND IsActive=1 AND IsDeleted=0", ct);
        var listingCount = await ScalarLongAsync(
            $"SELECT COUNT(*) FROM ProductListing WHERE CategoryId IN " +
            $"(SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql})) AND IsActive=1 AND IsDeleted=0", ct);
        var defCount = await ScalarLongAsync(
            $"SELECT COUNT(DISTINCT m.AttributeDefinitionId) FROM CategoryAttributeMappings m " +
            $"INNER JOIN AttributeDefinitions d ON d.Id=m.AttributeDefinitionId " +
            $"WHERE m.CategoryId IN (SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql})) " +
            $"AND m.IsActive=1 AND m.IsDeleted=0 AND d.IsDeleted=0", ct);
        Log($"  ► فئات ظاهِرَة: {catCount} | عُروض: {listingCount} | خصائص دِيناميكيّة فَعّالَة: {defCount}");
        if (catCount == 0)
            Log("  ⚠ صِفر فئات — لا يوجَد صَفّ ProductCategory بِاسم \"عشير يدور سكن\" أو \"عشير عنده سكن\" في هذه القاعِدَة. " +
                "تَأَكَّد مِن الـ DefaultConnection.");
    }

    // ═══ إبقاء مَجموعَتَي عشير فَقَط (إخفاء كُلّ ما عَداها) ═══════════════════
    // يَجعَل القاعِدَة تَعرِض هاتَين المَجموعَتَين حَصراً مَع عُروضهما +
    // خَصائِصهما الديناميكيّة. كُلّ فئات/رَوابِط/تَعريفات/قِيَم/عُروض أُخرى
    // تُخفى. يَحمي خصائص البروفايل (لا تَظهَر في أيّ ربط — لا تُمَسّ).
    public async Task OnlyRoommateGroupsAsync(CancellationToken ct)
    {
        Log("\n=== إبقاء مَجموعَتَي عشير فَقَط — إخفاء كُلّ ما عَداها ===");
        // نُضَيِّق المُطابَقَة إلى الصُّفوف الَّتي تَحوي أيقونَة (لِلتَخَلُّص
        // مِن التَّكرار حَيث يوجَد صَفّ بِأيقونَة وآخَر بِلا).
        var rcats = $"(SELECT Id FROM ProductCategory WHERE Name IN ({RoommateNamesSql}) AND Icon IS NOT NULL AND LTRIM(RTRIM(Icon)) <> '')";
        var rdefs = $"(SELECT DISTINCT AttributeDefinitionId FROM CategoryAttributeMappings WHERE CategoryId IN {rcats})";

        // ① الفئات: أَخفِ غير-عشير، فَعِّل عشير
        await ExecAsync($"UPDATE ProductCategory SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE Id NOT IN {rcats}", ct);
        await ExecAsync($"UPDATE ProductCategory SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE Id IN {rcats}", ct);

        // ② الرَّوابِط
        await ExecAsync($"UPDATE CategoryAttributeMappings SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId NOT IN {rcats}", ct);
        await ExecAsync($"UPDATE CategoryAttributeMappings SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId IN {rcats}", ct);

        // ③ تَعريفات الخصائص: نُخفي فَقَط الَّتي تُستَخدَم في رَوابِط غير-عشير
        //    وَلَيسَت في رَوابِط عشير. خصائص البروفايل (غير المُستَخدَمَة في
        //    أيّ ربط) تَبقى سَليمَة.
        await ExecAsync($"UPDATE AttributeDefinitions SET IsDeleted=1, UpdatedAt=SYSUTCDATETIME() WHERE Id IN (SELECT DISTINCT AttributeDefinitionId FROM CategoryAttributeMappings WHERE CategoryId NOT IN {rcats}) AND Id NOT IN {rdefs}", ct);
        await ExecAsync($"UPDATE AttributeDefinitions SET IsDeleted=0, UpdatedAt=SYSUTCDATETIME() WHERE Id IN {rdefs}", ct);

        // ④ قِيَم الخصائص
        await ExecAsync($"UPDATE AttributeValues SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN (SELECT Id FROM AttributeDefinitions WHERE IsDeleted=1)", ct);
        await ExecAsync($"UPDATE AttributeValues SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN {rdefs}", ct);

        // ⑤ العُروض: نُخفي كُلّ ما لَيسَ تَحت فئات عشير
        await ExecAsync($"UPDATE ProductListing SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId IS NULL OR CategoryId NOT IN {rcats}", ct);
        await ExecAsync($"UPDATE ProductListing SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE CategoryId IN {rcats}", ct);

        // تَشخيص نِهائيّ
        var totalCats = await ScalarLongAsync("SELECT COUNT(*) FROM ProductCategory WHERE IsActive=1 AND IsDeleted=0", ct);
        var totalListings = await ScalarLongAsync("SELECT COUNT(*) FROM ProductListing WHERE IsActive=1 AND IsDeleted=0", ct);
        var roomCats = await ScalarLongAsync(
            $"SELECT COUNT(*) FROM ProductCategory WHERE Name IN ({RoommateNamesSql}) AND IsActive=1 AND IsDeleted=0", ct);
        var roomListings = await ScalarLongAsync(
            $"SELECT COUNT(*) FROM ProductListing WHERE CategoryId IN {rcats} AND IsActive=1 AND IsDeleted=0", ct);
        Log($"  ► إجماليّ ظاهِر: فئات={totalCats}، عُروض={totalListings}");
        Log($"  ► تَحت عشير:      فئات={roomCats}، عُروض={roomListings}");
        if (totalCats != roomCats || totalListings != roomListings)
            Log("  ⚠ هُناك فئات/عُروض ظاهِرَة لَيسَت تَحت عشير — راجِع.");
    }



    // ═══ إخفاء كُلّ الفئات بِلا أيقونَة (تَنظيف عامّ) ═══════════════════════
    // مُفيد لِإخفاء الصُّفوف المُكَرَّرَة الَّتي بِلا أيقونَة، بِغَضّ النَّظَر
    // عَن الاسم. يَتَجاهَل الصُّفوف المَخفيّة سَلَفاً.
    public async Task HideIconlessCategoriesAsync(CancellationToken ct)
    {
        Log("\n=== إخفاء الفئات بِلا أيقونَة ===");
        await ExecAsync(
            "UPDATE ProductCategory SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() " +
            "WHERE IsDeleted=0 AND (Icon IS NULL OR LTRIM(RTRIM(Icon)) = '')", ct);
        var withIcon = await ScalarLongAsync(
            "SELECT COUNT(*) FROM ProductCategory WHERE IsActive=1 AND IsDeleted=0 AND Icon IS NOT NULL AND LTRIM(RTRIM(Icon)) <> ''", ct);
        var without = await ScalarLongAsync(
            "SELECT COUNT(*) FROM ProductCategory WHERE IsActive=1 AND IsDeleted=0 AND (Icon IS NULL OR LTRIM(RTRIM(Icon)) = '')", ct);
        Log($"  ► فئات ظاهِرَة بِأيقونَة: {withIcon} | بِلا أيقونَة: {without}");
    }

    // ═══ تَعطيل خَصائص السَّماح بِالاتِّصال + واتساب مِن كُلّ الفئات ════════
    // يُخفي تَعريفات `is_phone_allowed` و `is_whatsapp_allowed` + قِيَمهما +
    // كُلّ رَوابِطهما، **وَيُزيل مَفاتيحهما مِن AttributesJson** في كُلّ
    // العُروض الحاليّة — وإلّا تَبقى أزرار الاتِّصال/واتساب ظاهِرَة في
    // التَّفاصيل لِأَنّ العميل يَقرَأها مِن JSON العَرض مُباشَرَةً.
    // `is_messaging_allowed` يَبقى كَما هُوَ. عَكسيّاً: شَغِّل enable-contact-attrs
    // (لكِنّه لا يَستَطيع استِعادَة قِيَم AttributesJson — هذه فَقدان دائم
    // بَعد التَّعطيل).
    public async Task DisableContactAttrsAsync(CancellationToken ct)
    {
        Log("\n=== تَعطيل is_phone_allowed + is_whatsapp_allowed (مَع تَنظيف العُروض) ===");
        const string defs = "(SELECT Id FROM AttributeDefinitions WHERE Code IN ('is_phone_allowed','is_whatsapp_allowed'))";
        await ExecAsync($"UPDATE CategoryAttributeMappings SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN {defs}", ct);
        await ExecAsync($"UPDATE AttributeValues SET IsDeleted=1, IsActive=0, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN {defs}", ct);
        await ExecAsync($"UPDATE AttributeDefinitions SET IsDeleted=1, UpdatedAt=SYSUTCDATETIME() WHERE Id IN {defs}", ct);

        // إزالَة المَفاتيح مِن AttributesJson في كُلّ العُروض الَّتي تَحويها.
        // JSON_MODIFY(..., path, NULL) في الوَضع lax (الافتراضيّ) يَحذِف
        // المِفتاح. نُحَدِّث فَقَط الصُّفوف الَّتي تَحوي أَحَد المِفتاحَين.
        await ExecAsync(
            "UPDATE ProductListing " +
            "SET AttributesJson = JSON_MODIFY(JSON_MODIFY(AttributesJson, '$.is_phone_allowed', NULL), '$.is_whatsapp_allowed', NULL), " +
            "    UpdatedAt = SYSUTCDATETIME() " +
            "WHERE AttributesJson IS NOT NULL " +
            "  AND ISJSON(AttributesJson) = 1 " +
            "  AND (JSON_VALUE(AttributesJson, '$.is_phone_allowed') IS NOT NULL " +
            "    OR JSON_VALUE(AttributesJson, '$.is_whatsapp_allowed') IS NOT NULL)", ct);

        // تَشخيص: كَم عَرضاً ما زالَ يَحوي أَحَد المِفتاحَين (يَجِب أَن يَكون صِفر)؟
        var remaining = await ScalarLongAsync(
            "SELECT COUNT(*) FROM ProductListing WHERE AttributesJson IS NOT NULL AND ISJSON(AttributesJson)=1 AND (" +
            "JSON_VALUE(AttributesJson, '$.is_phone_allowed') IS NOT NULL OR " +
            "JSON_VALUE(AttributesJson, '$.is_whatsapp_allowed') IS NOT NULL)", ct);
        Log($"  ► عُروض ما زالَت تَحوي phone/whatsapp في JSON: {remaining}");
    }

    public async Task EnableContactAttrsAsync(CancellationToken ct)
    {
        Log("\n=== إعادَة تَفعيل is_phone_allowed + is_whatsapp_allowed ===");
        const string defs = "(SELECT Id FROM AttributeDefinitions WHERE Code IN ('is_phone_allowed','is_whatsapp_allowed'))";
        await ExecAsync($"UPDATE AttributeDefinitions SET IsDeleted=0, UpdatedAt=SYSUTCDATETIME() WHERE Id IN {defs}", ct);
        await ExecAsync($"UPDATE AttributeValues SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN {defs}", ct);
        await ExecAsync($"UPDATE CategoryAttributeMappings SET IsDeleted=0, IsActive=1, UpdatedAt=SYSUTCDATETIME() WHERE AttributeDefinitionId IN {defs}", ct);
    }

    // ═══ ضَبط باقَة واحِدَة فَقَط (إنشاء/تَحديث + تَعطيل البَقيَّة) ═══════════
    // باقَة "أَساسيّة": عَرض واحِد، ٣٠ يَوماً، ١٩ ر.س/شَهر. مُعَرِّف ثابِت
    // (idempotent). كُلّ الباقات الأُخرى تُعَطَّل (IsActive=0, IsDeleted=1).
    private static readonly Guid SinglePlanId = Guid.Parse("dd000000-0000-0000-0001-000000000001");

    public async Task SetupSinglePlanAsync(CancellationToken ct)
    {
        Log("\n=== ضَبط باقَة واحِدَة (١ عَرض / شَهر / ١٩ ر.س) + تَعطيل البَقيَّة ===");

        // CommissionType.Percentage=1, SupportLevel.Basic=1, AnalyticsLevel.Basic=1
        // upsert بِمُعَرِّف ثابِت (idempotent). كُلّ الأعمِدَة المَطلوبَة مُضَمَّنَة.
        var upsert = $@"
IF NOT EXISTS (SELECT 1 FROM SubscriptionPlans WHERE Id='{SinglePlanId}')
    INSERT INTO SubscriptionPlans (
        Id, CreatedAt, UpdatedAt, IsDeleted,
        Name, NameEn, Slug, Description, DescriptionEn, Icon, Color,
        SortOrder, IsActive, IsDefault, IsRecommended,
        MonthlyPrice, QuarterlyPrice, SemiAnnualPrice, AnnualPrice, Currency,
        TrialDays, GracePeriodDays,
        MaxListings, MaxImagesPerListing, MaxFeaturedListings, StorageLimitMB,
        MaxTeamMembers, MaxMonthlyMessages, MaxMonthlyApiCalls, ListingDurationDays,
        CommissionType, CommissionPercentage, CommissionFixedAmount, MinCommission, MaxCommission,
        HasVerifiedBadge, SearchPriorityBoost, AnalyticsLevel, SupportLevel,
        AllowDirectMessages, AllowApiAccess, AllowCustomStorePage, AllowPromotionalTools,
        AllowDataExport, RemoveBranding, EmailReports, PushNotifications,
        ExtraFeaturesJson, MetadataJson
    ) VALUES (
        '{SinglePlanId}', SYSUTCDATETIME(), NULL, 0,
        N'باقَة أساسيّة', N'Basic', 'basic-single', N'باقَة واحِدَة شَهريّة لِعَرض واحِد', N'Single monthly listing plan', NULL, NULL,
        0, 1, 1, 0,
        19.00, NULL, NULL, NULL, 'SAR',
        0, 3,
        1, 5, 0, 500,
        1, -1, 0, 30,
        1, 0, 0, NULL, NULL,
        0, 0, 1, 1,
        1, 0, 0, 0,
        0, 0, 0, 1,
        NULL, NULL
    );
ELSE
    UPDATE SubscriptionPlans SET
        Name=N'باقَة أساسيّة', NameEn=N'Basic', Slug='basic-single',
        Description=N'باقَة واحِدَة شَهريّة لِعَرض واحِد', DescriptionEn=N'Single monthly listing plan',
        MonthlyPrice=19.00, Currency='SAR',
        MaxListings=1, ListingDurationDays=30,
        IsActive=1, IsDefault=1, IsDeleted=0,
        UpdatedAt=SYSUTCDATETIME()
    WHERE Id='{SinglePlanId}';
";
        await ExecAsync(upsert, ct);

        // تَعطيل كُلّ الباقات الأُخرى
        await ExecAsync(
            $"UPDATE SubscriptionPlans SET IsActive=0, IsDeleted=1, UpdatedAt=SYSUTCDATETIME() WHERE Id <> '{SinglePlanId}'", ct);

        // تَشخيص
        var active = await ScalarLongAsync("SELECT COUNT(*) FROM SubscriptionPlans WHERE IsActive=1 AND IsDeleted=0", ct);
        var total = await ScalarLongAsync("SELECT COUNT(*) FROM SubscriptionPlans", ct);
        Log($"  ► الباقات الفَعّالَة الآن: {active} (مِن أَصل {total})");
        if (active != 1) Log($"  ⚠ توقَّعتُ ١ — راجِع.");
    }

    private async Task ExecAsync(string sql, CancellationToken ct)
    {
        if (!_apply) { Plan($"(dry-run) {sql}"); return; }
        var n = await _db.Database.ExecuteSqlRawAsync(sql, ct);
        Plan($"{n} صَفّ — {(sql.Length > 70 ? sql[..70] + "…" : sql)}");
    }

    private async Task<bool> TableExistsAsync(string name, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT OBJECT_ID(N'dbo.{name}', N'U')";
        var r = await cmd.ExecuteScalarAsync(ct);
        return r is not null && r != DBNull.Value;
    }

    // قِراءَة عَدَد (تَعمَل دائماً حَتّى في dry-run — لا تُعَدِّل شَيئاً).
    private async Task<long> ScalarLongAsync(string sql, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var r = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt64(r ?? 0);
    }


    // ─── ① المالِك ───────────────────────────────────────────────────────
    private async Task<Guid> ResolveOwnerAsync(CancellationToken ct)
    {
        var configured = _config["Seeder:OwnerProfileId"];
        if (Guid.TryParse(configured, out var forced) && forced != Guid.Empty)
        {
            Log($"استُخدِم OwnerProfileId المُحَدَّد في الإعدادات.");
            return forced;
        }

        var listingRepo = _repos.CreateRepository<ProductListing>();
        var listings = await listingRepo.GetAllWithPredicateAsync(null, includeDeleted: false);
        var top = listings
            .Where(l => l.VendorId != Guid.Empty)
            .GroupBy(l => l.VendorId)
            .Select(g => new { VendorId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefault();

        if (top is null)
            throw new InvalidOperationException(
                "لا توجَد عُروض حاليّة لاشتِقاق المالِك. حَدِّد Seeder:OwnerProfileId يَدويّاً.");

        Log($"المالِك الأَكثَر نَشراً: {top.VendorId} ({top.Count} عَرضاً قائِماً).");
        return top.VendorId;
    }

    // ─── ② نَسخ احتياطيّ ─────────────────────────────────────────────────
    private async Task BackupBeforeDisableAsync(CancellationToken ct)
    {
        var cats = await _repos.CreateRepository<ProductCategory>().GetAllWithPredicateAsync(null, true);
        var defs = await _repos.CreateRepository<AttributeDefinition>().GetAllWithPredicateAsync(null, true);
        var maps = await _repos.CreateRepository<CategoryAttributeMapping>().GetAllWithPredicateAsync(null, true);

        var backup = new
        {
            takenAt = _now,
            categories = cats.Select(c => new { c.Id, c.Name, c.Slug, c.IsActive, c.IsDeleted }),
            attributeDefinitions = defs.Select(d => new { d.Id, d.Code, d.Name, d.IsDeleted }),
            categoryAttributeMappings = maps.Select(m => new { m.Id, m.CategoryId, m.AttributeDefinitionId, m.IsDeleted }),
        };
        var path = Path.Combine(AppContext.BaseDirectory, $"backup-taxonomy-{_now:yyyyMMdd-HHmmss}.json");
        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        if (_apply)
        {
            await File.WriteAllTextAsync(path, json, ct);
            Log($"📦 نَسخَة احتياطيّة: {path}");
        }
        else
        {
            Log($"📦 (dry-run) سَتُكتَب نَسخَة احتياطيّة بِـ {cats.Count} فِئَة + {defs.Count} خاصّيّة + {maps.Count} ربط.");
        }
    }

    // ─── ③ تَعطيل القَديم (حَذف ناعِم) ────────────────────────────────────
    private async Task DisableOldTaxonomyAsync(CancellationToken ct)
    {
        Log("تَعطيل الفئات والخصائص القَديمَة (حَذف ناعِم):");
        var schoolCatIds = SchoolSeedData.Categories(_now).Select(c => c.Id).ToHashSet();

        // الفئات: كُلّ ما لَيسَ فِئَة مَدرَسيّة.
        var catRepo = _repos.CreateRepository<ProductCategory>();
        var cats = await catRepo.GetAllWithPredicateAsync(null, includeDeleted: false);
        var oldCats = cats.Where(c => !schoolCatIds.Contains(c.Id)).ToList();
        var oldCatIds = oldCats.Select(c => c.Id).ToHashSet();
        foreach (var c in oldCats)
        {
            Plan($"فِئَة → تَعطيل: {c.Name} ({c.Slug})");
            if (_apply) { c.IsActive = false; c.IsDeleted = true; await catRepo.UpdateAsync(c, ct); }
        }

        // الرَّبط: كُلّ ربط لِفِئَة قَديمَة.
        var mapRepo = _repos.CreateRepository<CategoryAttributeMapping>();
        var maps = await mapRepo.GetAllWithPredicateAsync(null, includeDeleted: false);
        var oldMaps = maps.Where(m => oldCatIds.Contains(m.CategoryId)).ToList();
        Plan($"ربط فِئَة-خاصّيّة → تَعطيل: {oldMaps.Count} ربط");
        foreach (var m in oldMaps)
            if (_apply) { m.IsActive = false; m.IsDeleted = true; await mapRepo.UpdateAsync(m, ct); }

        // الخصائص: نُعَطِّل فَقَط ما كانَ مَربوطاً بِالفئات القَديمَة (مِن
        // الرَّبط) — لا المُشتَرَكَة الأَساسيّة ولا school_*. هذا يَحمي خصائص
        // البروفايل (Country/IsVerified/BusinessName…) الَّتي لَيسَت تَصنيف
        // إعلانات أَصلاً فَلا تُربَط بِأَيّ فِئَة، فَتَبقى سَليمَة. (سابِقاً
        // كانَ التَّعطيل يَشمَل أَيّ خاصّيّة غير أَساسيّة/مَدرَسيّة فَطال
        // البروفايل بِالخَطَأ.)
        var keep = new HashSet<string>(SchoolSeedData.ReusedBaseCodes, StringComparer.OrdinalIgnoreCase);
        var usedByOldCats = oldMaps.Select(m => m.AttributeDefinitionId).ToHashSet();
        var defRepo = _repos.CreateRepository<AttributeDefinition>();
        var defs = await defRepo.GetAllWithPredicateAsync(null, includeDeleted: false);
        var oldDefs = defs.Where(d =>
            usedByOldCats.Contains(d.Id) &&
            !keep.Contains(d.Code) &&
            !d.Code.StartsWith("school_", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var d in oldDefs)
        {
            Plan($"خاصّيّة → تَعطيل: {d.Code} ({d.Name})");
            if (_apply) { d.IsDeleted = true; await defRepo.UpdateAsync(d, ct); }
        }
        Log($"  المُبقاة فَعّالَة (مُشتَرَكَة): {string.Join(", ", keep)}");
        Log($"  (خصائص البروفايل وغير المَربوطَة بِفئات قَديمَة لَم تُمَسّ)");
    }

    // ─── ④ فئات المَدارِس ────────────────────────────────────────────────
    private async Task SeedCategoriesAsync(CancellationToken ct)
    {
        Log("بَذر فئات المَدارِس:");
        var repo = _repos.CreateRepository<ProductCategory>();
        var existing = (await repo.GetAllWithPredicateAsync(null, includeDeleted: true))
            .ToDictionary(c => c.Id);
        foreach (var c in SchoolSeedData.Categories(_now))
        {
            if (existing.TryGetValue(c.Id, out var cur))
            {
                // مُزامَنَة IsActive/IsDeleted (مُهِمّ لِتَعطيل الأب "مدارس"
                // عَلى إعادَة التَّشغيل لَو كانَ أُنشِئ فَعّالاً سابِقاً).
                if (cur.IsActive != c.IsActive || cur.IsDeleted)
                {
                    Plan($"تَحديث فِئَة: {c.Name} (IsActive={c.IsActive})");
                    if (_apply) { cur.IsActive = c.IsActive; cur.IsDeleted = false; cur.UpdatedAt = _now; await repo.UpdateAsync(cur, ct); }
                }
                else Plan($"فِئَة موجودَة — تَخطّي: {c.Name}");
                continue;
            }
            Plan($"فِئَة جَديدَة: {c.Name} ({c.Slug})" + (c.IsActive ? "" : " [غير فَعّالَة]"));
            if (_apply) await repo.AddAsync(c, ct);
        }
    }

    // ═══ تَحويل خصائص العَرض إلى عَرَبيّة جاهِزَة لِلعَرض ══════════════════
    // صَفحَة التَّفاصيل في العميل القَديم تَعرِض مَفتاح AttributesJson
    // كَتَسمِيَة (بَعد FormatAttributeKey) وَقيمَته كَنَصّ، بِلا استِشارَة
    // القاعِدَة لِلخصائص غير المَعروفَة. لِذا نُخَزِّن مَفاتيح + قِيَم عَرَبيّة
    // جاهِزَة (مُشتَقَّة مِن Name/DisplayName في تَعريفاتنا) فَتَظهَر عَرَبيّة
    // بِلا أَيّ تَحديث لِلتَّطبيق. (الأكواد آلِيّاً غير مُتَرجَمَة في العميل.)
    private static Dictionary<string, object>? _arLabelByCode;
    private static Dictionary<string, string>? _arValueByCodeValue;

    private static void EnsureArMaps()
    {
        if (_arLabelByCode is not null) return;
        var specs = SchoolSeedData.NewAttributes();
        _arLabelByCode = specs.ToDictionary(s => s.Code, s => (object)s.Name);
        _arValueByCodeValue = new(StringComparer.Ordinal);
        foreach (var s in specs)
            if (s.Values is not null)
                foreach (var (v, disp) in s.Values)
                    _arValueByCodeValue[$"{s.Code}|{v}"] = disp;
    }

    private static Dictionary<string, object> ArabicDisplayAttributes(Dictionary<string, object> attrs)
    {
        EnsureArMaps();
        var result = new Dictionary<string, object>();
        foreach (var (code, val) in attrs)
        {
            var label = _arLabelByCode!.TryGetValue(code, out var l) ? (string)l : code.Replace('_', ' ');
            string ValDisp(string v) => _arValueByCodeValue!.TryGetValue($"{code}|{v}", out var d) ? d : v;

            object display = val switch
            {
                bool b              => b ? "نعم" : "لا",
                string[] arr        => string.Join("، ", arr.Select(ValDisp)),
                IEnumerable<string> e => string.Join("، ", e.Select(ValDisp)),
                string s            => ValDisp(s),
                int or long         => code.Contains("year") ? val.ToString()! : Convert.ToInt64(val).ToString("N0"),
                _                   => val.ToString() ?? ""
            };
            result[label] = display;
        }
        return result;
    }

    // ─── ⑤ خصائص المَدارِس + قِيَمها ──────────────────────────────────────
    private async Task SeedAttributesAsync(CancellationToken ct)
    {
        Log("بَذر خصائص المَدارِس:");
        var defRepo = _repos.CreateRepository<AttributeDefinition>();
        var valRepo = _repos.CreateRepository<AttributeValue>();
        var existingDefs = (await defRepo.GetAllWithPredicateAsync(null, includeDeleted: true))
            .Select(d => d.Id).ToHashSet();
        var existingVals = (await valRepo.GetAllWithPredicateAsync(null, includeDeleted: true))
            .Select(v => v.Id).ToHashSet();

        foreach (var a in SchoolSeedData.NewAttributes())
        {
            if (!existingDefs.Contains(a.Id))
            {
                Plan($"خاصّيّة جَديدَة: {a.Code} ({a.Name}) [{a.Type}]");
                if (_apply)
                    await defRepo.AddAsync(new AttributeDefinition
                    {
                        Id = a.Id, Code = a.Code, Name = a.Name, Type = a.Type,
                        IsRequired = a.Required, IsFilterable = a.Filterable,
                        IsVisibleInList = true, IsVisibleInDetail = true,
                        SortOrder = a.Sort, ValidationRules = a.ValidationRules,
                        CreatedAt = _now
                    }, ct);
            }
            else Plan($"خاصّيّة موجودَة — تَخطّي: {a.Code}");

            if (a.Values is null) continue;
            int vi = 0;
            foreach (var (val, disp) in a.Values)
            {
                var vid = DeterministicValueId(a.Id, ++vi);
                if (existingVals.Contains(vid)) continue;
                if (_apply)
                    await valRepo.AddAsync(new AttributeValue
                    {
                        Id = vid, AttributeDefinitionId = a.Id,
                        Value = val, DisplayName = disp, Code = val,
                        SortOrder = vi, IsActive = true, CreatedAt = _now
                    }, ct);
            }
            if (a.Values.Length > 0) Plan($"   + {a.Values.Length} قيمة لِـ {a.Code}");
        }
    }

    // مُعَرِّف قيمَة حَتميّ مُشتَقّ مِن مُعَرِّف التَّعريف + الفِهرِس. نَضَع
    // الفِهرِس في بايت مُستَقِلّ (b[13]) وَعَلامَة (b[12]=0x5C) دونَ المَساس
    // بِالبايتات الَّتي تُمَيِّز التَّعريف (خاصّةً b[15]). هذا يَضمَن عَدَم
    // التَّصادُم بَينَ قِيَم تَعريفات مُختَلِفَة أو قِيَم نَفس التَّعريف، وَلا
    // يَتَصادَم مَع مُعَرِّفات التَّعريفات (الَّتي b[12]=0).
    private static Guid DeterministicValueId(Guid defId, int index)
    {
        var b = defId.ToByteArray();
        b[12] = 0x5C;                       // عَلامَة "قيمَة"
        b[13] = (byte)(index & 0xFF);       // فِهرِس القيمَة داخِل التَّعريف
        return new Guid(b);
    }

    // ─── ⑥ رَبط الفئات بِالخصائص ─────────────────────────────────────────
    private async Task SeedMappingsAsync(CancellationToken ct)
    {
        Log("رَبط فئات المَدارِس بِالخصائص:");
        var mapRepo = _repos.CreateRepository<CategoryAttributeMapping>();
        var defRepo = _repos.CreateRepository<AttributeDefinition>();

        var defsByCode = (await defRepo.GetAllWithPredicateAsync(null, includeDeleted: true))
            .GroupBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var existingPairs = (await mapRepo.GetAllWithPredicateAsync(null, includeDeleted: true))
            .Select(m => (m.CategoryId, m.AttributeDefinitionId)).ToHashSet();

        foreach (var (catId, codes) in SchoolSeedData.CategoryAttributeCodes())
        {
            int sort = 0, added = 0, missing = 0;
            foreach (var code in codes)
            {
                sort++;
                if (!defsByCode.TryGetValue(code, out var defId)) { missing++; continue; }
                if (existingPairs.Contains((catId, defId))) continue;
                if (_apply)
                    await mapRepo.AddAsync(new CategoryAttributeMapping
                    {
                        Id = Guid.NewGuid(), CategoryId = catId, AttributeDefinitionId = defId,
                        SortOrder = sort, IsActive = true, CreatedAt = _now
                    }, ct);
                added++;
            }
            Plan($"فِئَة {catId}: +{added} ربط" + (missing > 0 ? $" (⚠ {missing} كود غير موجود)" : ""));
        }
    }

    // ─── ⑦ عُروض المَدارِس العَيِّنَة + الصُّوَر ──────────────────────────
    private async Task SeedListingsAsync(Guid ownerId, CancellationToken ct)
    {
        Log("بَذر عُروض المَدارِس العَيِّنَة:");
        var listingRepo = _repos.CreateRepository<ProductListing>();
        var productRepo = _repos.CreateRepository<Product>();
        var sampleIds = SchoolSeedData.Samples().Select(s => s.Id).ToHashSet();
        var existing = (await listingRepo.GetAllWithPredicateAsync(null, includeDeleted: true))
            .Where(l => sampleIds.Contains(l.Id))
            .ToDictionary(l => l.Id);

        // المُضيف الصَّحيح لِلروابِط = نَفس ما يُنادي التَّطبيق القَديم
        // (api.ashare.sa). الافتراضيّ القَديم (خادِم GCP) جَعَلَ روابِط
        // /api/media/… تُشير لِمُضيف مَيِّت فَلا تُحَمَّل الصُّوَر.
        var baseUrl = (string.IsNullOrWhiteSpace(_config["HostSettings:BaseUrl"])
            ? "https://api.ashare.sa" : _config["HostSettings:BaseUrl"]!).TrimEnd('/');

        foreach (var s in SchoolSeedData.Samples())
        {
            // عَرض مَوجود مِن تَشغيل سابِق → أَصلِح مُضيف روابِط الصُّوَر فَقَط
            // (الصُّوَر مَرفوعَة فِعلاً في OSS؛ لا نُعيد الرَّفع).
            if (existing.TryGetValue(s.Id, out var ex))
            {
                var imgs = SafeDeserialize(ex.ImagesJson);
                var fixedImgs = imgs.Select(u => ReHost(u, baseUrl)).ToList();
                var arabicAttrs = JsonSerializer.Serialize(ArabicDisplayAttributes(s.Attributes), _jsonAr);
                var needsImgFix = !fixedImgs.SequenceEqual(imgs) && fixedImgs.Count > 0;
                var needsAttrFix = ex.AttributesJson != arabicAttrs;
                if (needsImgFix || needsAttrFix)
                {
                    Plan($"إصلاح عَرض مَوجود: {s.Title}" +
                         (needsImgFix ? " [روابِط صُوَر]" : "") + (needsAttrFix ? " [خصائص عَرَبيّة]" : ""));
                    if (_apply)
                    {
                        if (needsImgFix) { ex.ImagesJson = JsonSerializer.Serialize(fixedImgs); ex.FeaturedImage = fixedImgs.FirstOrDefault(); }
                        if (needsAttrFix) ex.AttributesJson = arabicAttrs;
                        ex.UpdatedAt = _now;
                        await listingRepo.UpdateAsync(ex, ct);

                        if (needsImgFix)
                        {
                            var prod = (await productRepo.GetAllWithPredicateAsync(p => p.Id == s.Id, true)).FirstOrDefault();
                            if (prod is not null) { prod.FeaturedImage = fixedImgs.FirstOrDefault(); await productRepo.UpdateAsync(prod, ct); }
                        }
                    }
                }
                else Plan($"عَرض مَوجود، سَليم — تَخطّي: {s.Title}");
                continue;
            }

            Plan($"عَرض جَديد: {s.Title} — {s.Price:N0} ر.س — صُوَر: {s.ImageUrls.Length}");

            var imageUrls = await UploadImagesAsync(s, baseUrl, ct);

            if (!_apply) continue;

            var productId = s.Id; // نُطابِق Product.Id مَع Listing.Id (نَفس نَمَط البَذر القَديم).
            await productRepo.AddAsync(new Product
            {
                Id = productId, Name = s.Title, Sku = $"SCHOOL-{s.Id:N}",
                Type = ProductType.Service, Status = ProductStatus.Active,
                ShortDescription = s.Description.Length > 200 ? s.Description[..200] : s.Description,
                FeaturedImage = imageUrls.FirstOrDefault(), SortOrder = 0, CreatedAt = _now
            }, ct);

            await listingRepo.AddAsync(new ProductListing
            {
                Id = s.Id, VendorId = ownerId, ProductId = productId, CategoryId = s.CategoryId,
                Title = s.Title, Description = s.Description,
                Status = ListingStatus.Active, IsActive = true, IsNew = true,
                Price = s.Price, Currency = "SAR", QuantityAvailable = 1,
                Latitude = s.Lat, Longitude = s.Lng, Address = s.Address, City = s.City,
                ImagesJson = JsonSerializer.Serialize(imageUrls),
                FeaturedImage = imageUrls.FirstOrDefault(),
                AttributesJson = JsonSerializer.Serialize(ArabicDisplayAttributes(s.Attributes), _jsonAr),
                CreatedAt = _now
            }, ct);
        }
    }

    private static List<string> SafeDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new(); }
    }

    // يُعيد كِتابَة مُضيف رابِط proxy (كُلّ ما قَبل /api/media/) إلى baseUrl
    // الحاليّ، مَع إبقاء objectName كَما هُوَ. روابِط غير proxy تُترَك.
    private static string ReHost(string url, string baseUrl)
    {
        if (string.IsNullOrEmpty(url)) return url;
        var i = url.IndexOf("/api/media/", StringComparison.OrdinalIgnoreCase);
        return i >= 0 ? baseUrl + url[i..] : url;
    }

    private async Task<List<string>> UploadImagesAsync(SchoolSeedData.SampleSchool s, string baseUrl, CancellationToken ct)
    {
        var result = new List<string>();
        foreach (var src in s.ImageUrls)
        {
            if (!_apply || _storage is null)
            {
                // dry-run أو بِلا تَخزين: أَبقِ الرابِط الأَصليّ لِلمُعايَنَة.
                result.Add(src);
                continue;
            }
            try
            {
                var resp = await _http.GetAsync(src, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    Log($"     ⚠ تَعَذَّر تَنزيل صورَة ({(int)resp.StatusCode}): {src}");
                    continue;
                }
                await using var inStream = await resp.Content.ReadAsStreamAsync(ct);
                using var mem = new MemoryStream();
                await inStream.CopyToAsync(mem, ct);
                mem.Position = 0;

                var ext = (resp.Content.Headers.ContentType?.MediaType) switch
                {
                    "image/png" => ".png", "image/webp" => ".webp", _ => ".jpg"
                };
                var fileName = $"{Guid.NewGuid()}{ext}";
                var objectName = await _storage.SaveAsync(mem, fileName, "listings/schools", ct);
                result.Add(string.IsNullOrEmpty(baseUrl) ? objectName : $"{baseUrl}/api/media/{objectName}");
            }
            catch (Exception ex)
            {
                Log($"     ⚠ فَشَل رَفع صورَة: {ex.Message} — {src}");
            }
        }
        return result;
    }
}
