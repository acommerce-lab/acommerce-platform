using ACommerce.OperationEngine.Core;

namespace ACommerce.Kits.DynamicAttributes.Operations;

/// <summary>
/// أَنواع العَمَليّات (OperationType) الَّتي يُنتِجها/يَستَهلِكها كيت
/// DynamicAttributes. التَطبيق يَستَخدِم هذه الثَوابِت في
/// <c>Entry.Create(DynamicAttributeOps.ValueSet.Name)</c> ولا يَبني
/// نُصوصاً يَدَوِيّاً.
/// </summary>
public static class DynamicAttributeOps
{
    // ─── إدارَة التَعريفات (admin) ─────────────────────────────────
    public static readonly OperationType DefinitionCreate = new("dynamic_attrs.definition.create");
    public static readonly OperationType DefinitionUpdate = new("dynamic_attrs.definition.update");
    public static readonly OperationType DefinitionDelete = new("dynamic_attrs.definition.delete");

    // ─── ربط النِطاق بِالتَعريفات ─────────────────────────────────
    public static readonly OperationType ScopeAttach     = new("dynamic_attrs.scope.attach");
    public static readonly OperationType ScopeDetach     = new("dynamic_attrs.scope.detach");

    // ─── عَمَليّات قِيَم الكِيان ────────────────────────────────────
    public static readonly OperationType ValueSet        = new("dynamic_attrs.value.set");
    public static readonly OperationType ValueClear      = new("dynamic_attrs.value.clear");

    // ─── قِراءات (تَوسيم لِلتَدخُّل بِالـ enricher) ─────────────────
    public static readonly OperationType TemplateGet     = new("dynamic_attrs.template.get");
    public static readonly OperationType SnapshotGet     = new("dynamic_attrs.snapshot.get");
}

public static class DynamicAttributeTagKeys
{
    /// <summary>اسم النِطاق (Profile, Listing, Complaint, …) — لِلسُجوع.</summary>
    public static readonly TagKey ScopeName = new("dyn_scope_name");
    /// <summary>Guid النِطاق ⇒ مَدخَل <c>IAttributeTemplateSource</c>.</summary>
    public static readonly TagKey ScopeId   = new("dyn_scope_id");
    /// <summary>مُعَرِّف الكِيان المُتَأَثِّر.</summary>
    public static readonly TagKey EntityId  = new("dyn_entity_id");
    /// <summary>عَلامَة "اِدمِج dynamic snapshot في الـ envelope" — يَلتَقِطها
    /// المُعتَرِض في الخَلفِيَّة.</summary>
    public static readonly TagKey HydrateSnapshot = new("dyn_hydrate");
}
