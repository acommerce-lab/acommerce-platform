using ACommerce.OperationEngine.Core;
using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.Operations;

/// <summary>
/// يدمج OperationDescriptor (من رد الخادم) مع Operation (المحلية).
/// النتيجة: العملية المحلية تحمل كل الأطراف والعلامات والمحللات
/// التي أنتجها الخادم - بحيث يصبح القيد الكامل متاحاً للعميل.
/// </summary>
public static class OperationMerger
{
    public static void Merge(Operation localOp, OperationDescriptor serverDescriptor)
    {
        // العلامات: ندمج تلك التي لم تكن موجودة محلياً
        foreach (var (key, value) in serverDescriptor.Tags)
        {
            if (!localOp.HasTag(key))
                localOp.AddTag(key, value);
        }

        // الأطراف: نضيف الأطراف التي أضافها الخادم (مثلاً Subscription Party بعد QuotaConsumption)
        // - نتعرف عليها بالـ Identity
        var existingIdentities = localOp.Parties.Select(p => p.Identity).ToHashSet();
        foreach (var serverParty in serverDescriptor.Parties)
        {
            if (existingIdentities.Contains(serverParty.Identity))
            {
                // طرف موجود - حدّث علاماته
                var local = localOp.Parties.First(p => p.Identity == serverParty.Identity);
                foreach (var (k, v) in serverParty.Tags)
                {
                    if (!local.HasTag(k))
                        local.AddTag(k, v);
                }
            }
            else
            {
                // طرف جديد من الخادم - أضفه
                var newParty = new Party(serverParty.Identity, serverParty.Value);
                foreach (var (k, v) in serverParty.Tags)
                    newParty.AddTag(k, v);
                localOp.AddParty(newParty);
            }
        }
    }
}
