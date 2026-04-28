using MediatR;
using ACommerce.OperationEngine.Core;

namespace ACommerce.OperationEngine.DataInterceptors;

/// <summary>
/// يمثل أمر وساطة (MediatR Command) يُطلق بواسطة المعترض البياني 
/// عندما يكتشف أن العملية تحتوي على نية التخزين في قاعدة البيانات.
/// </summary>
public record DataOperationCommand(Operation Operation, string DbAction, string TargetEntity) : IRequest<bool>;
