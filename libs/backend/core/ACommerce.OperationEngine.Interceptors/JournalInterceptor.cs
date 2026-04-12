using ACommerce.OperationEngine.Core;
using ACommerce.SharedKernel.Abstractions.Entities;
using ACommerce.SharedKernel.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace ACommerce.OperationEngine.Interceptors;

/// <summary>
/// معترض الدفتر: يُسجّل كل عملية (نوعها، أطرافها، علاماتها، نتيجتها) في جدول journal_entries.
///
/// مُفعَّل بشكل اختياري عبر services.AddOperationJournal().
/// يعمل بعد التنفيذ (Post) على كل العمليات غير المختومة.
///
/// يتيح:
///   - مسار تدقيق كامل بدون معترضات مخصصة
///   - استعلامات الحسابات (IAccountQuery)
///   - إعادة التشغيل والتصحيح
/// </summary>
public class JournalInterceptor : IOperationInterceptor
{
    public string Name => "Journal";
    public InterceptorPhase Phase => InterceptorPhase.Post;

    /// <summary>يعمل على كل العمليات غير المختومة. العمليات المختومة (sealed=true) تتجاوز كل المعترضات.</summary>
    public bool AppliesTo(Operation op) => !op.HasTag("sealed", "true");

    public async Task<AnalyzerResult> InterceptAsync(OperationContext context, OperationResult? result = null)
    {
        var factory = context.Services.GetService<IRepositoryFactory>();
        if (factory == null) return AnalyzerResult.Pass();

        var repo = factory.CreateRepository<JournalEntry>();
        var op = context.Operation;

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            OperationId = op.Id,
            OperationType = op.Type,
            Status = op.Status.ToString(),
            Success = result?.Success ?? (op.Status == OperationStatus.Completed),
            ErrorMessage = result?.ErrorMessage,
            Timestamp = op.CompletedAt ?? DateTime.UtcNow,
            ParentOperationId = op.ParentOperationId,
            PartiesJson = JsonSerializer.Serialize(
                op.Parties.Select(p => new
                {
                    p.Identity,
                    p.Value,
                    Tags = p.Tags.Select(t => new { t.Key, t.Value })
                })),
            TagsJson = JsonSerializer.Serialize(
                op.Tags.Select(t => new { t.Key, t.Value }))
        };

        await repo.AddAsync(entry, context.CancellationToken);
        return AnalyzerResult.Pass();
    }
}
