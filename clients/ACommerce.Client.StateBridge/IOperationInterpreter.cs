using ACommerce.OperationEngine.Wire;

namespace ACommerce.Client.StateBridge;

/// <summary>
/// مُفسّر عملية → تغييرات على الـ store.
/// كل domain يُطبق واحداً أو أكثر يعتني بنوع عملية معين.
/// </summary>
public interface IOperationInterpreter<TStore>
{
    /// <summary>يقرر: هل هذا المُفسّر يهتم بهذه العملية؟</summary>
    bool CanInterpret(OperationDescriptor op);

    /// <summary>يطبّق التغييرات على الـ store ويُعيد أحداث جانبية اختيارية</summary>
    Task InterpretAsync(OperationDescriptor op, object? data, TStore store, CancellationToken ct = default);
}
