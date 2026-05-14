using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Patterns;

namespace ACommerce.Kits.Reports.Frontend.Customer.Stores;

/// <summary>
/// مَصنَع عَمَليّات Reports kit عَلى جانِب العَميل. تَستَهلِكها صَفحات
/// تُريد تَقديم بَلاغ عَن مَحتوى (إعلان، مُستَخدِم، …). الباك يَتَحَقَّق
/// مِن صَلاحيّات + يُخَزِّن عَبر <c>ReportsController</c>.
/// </summary>
public static class ReportsOps
{
    public static Operation Create() => Entry
        .Create("reports.create")
        .From("User:current",      1, ("role", "reporter"))
        .To("Server:reports",      1, ("role", "received"))
        .Build();
}
