# Dynamic Attributes — Template + Snapshot Pattern

## The Problem

Ashare has 5 category types (Residential, LookingForHousing, LookingForPartner,
Administrative, Commercial). Each category has category-specific fields (rooms,
area, floor, features, etc.) that differ in number, type, and allowed values.
Hardcoding these as entity columns means every new category requires a schema
migration. Dynamic attributes solve this.

## The Pattern

1. **Template** (`AttributeTemplate`) — lives on `Category.AttributeTemplateJson`.
   Defines the _schema_: which fields a category supports, their types, labels,
   options, icons, units, sort order, and whether they show on the listing card.

2. **Snapshot** (`List<DynamicAttribute>`) — lives on `Listing.DynamicAttributesJson`.
   A frozen copy of the template fields _at the time the listing was created/updated_,
   filled with the actual values. Changing the category template later does NOT
   retroactively alter existing listings.

## Field Types

| Type | C# value | Example |
|---|---|---|
| `text` | string | "أحمد" |
| `number` | long | 3 |
| `decimal` | double | 150.5 |
| `bool` | bool | true |
| `select` | string (option value) | "furnished" |
| `multi` | `List<object>` (option values) | ["ac", "wifi", "parking"] |

## Building a Snapshot

```csharp
var template = DynamicAttributeHelper.ParseTemplate(category.AttributeTemplateJson);
var values = new Dictionary<string, object?> { ["rooms"] = 3, ["area"] = 150 };
var snapshot = DynamicAttributeHelper.BuildSnapshot(template, values);
listing.DynamicAttributesJson = DynamicAttributeHelper.SerializeAttributes(snapshot);
```

`BuildSnapshot` matches each template field to the provided values, resolves
option labels (for select/multi), and returns a complete `List<DynamicAttribute>`.

## Unknown Key Preservation

When integrating with production data, the source may contain attribute keys
that don't exist in the current template. The rule: **never drop data**.

Keys present in the template → full `DynamicAttribute` with label, icon, type.
Keys NOT in the template → preserved as raw `DynamicAttribute` entries with
type inferred from the runtime value (`bool`, `number`, `decimal`, `multi`,
or `text`). This ensures stakeholder-defined custom fields survive even when
the new system doesn't have a template slot for them.

## Adding or Modifying a Template

Templates are defined in `AshareCategoryTemplates.cs`:

```csharp
F("features", "Features", "المرافق", "multi",
    opts: ResidentialAmenities(), sort: 10, icon: "stars"),
F("requires_license", "Requires license", "يتطلب ترخيص", "bool",
    sort: 12, icon: "shield-check"),
```

When adapting to production data: study the actual `attributes` object from
the production API, add/rename template fields to match, then update the
seed data accordingly. The canonical rule: the new system adapts to the
stakeholder's preferred data shape, not the other way around.

## Template Adaptation Log

| Date | Change | Reason |
|---|---|---|
| 2026-04-17 | `amenities` → `features` | Production uses `features` |
| 2026-04-17 | Added `requires_license`, `has_owner_license` | Fields used by stakeholder |

## Widgets

- `AcDynamicAttributeField` — renders a single attribute field (input for
  create/edit, display for read-only).
- `AcDynamicAttributesView` — renders the full snapshot as a list/grid of
  fields with icons, labels, and values.

## Storage Format

Both template and snapshot use **camelCase** JSON (via `JsonNamingPolicy.CamelCase`).
Null values are omitted (`JsonIgnoreCondition.WhenWritingNull`). This matches
the default `System.Text.Json` options used throughout the platform.
