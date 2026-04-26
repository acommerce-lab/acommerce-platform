namespace ACommerce.SharedKernel.Abstractions.Queries;

/// <summary>
/// يمثل عنصر تصفية واحد
/// </summary>
public class FilterItem
{
	/// <summary>
	/// اسم الخاصية المراد التصفية عليها
	/// </summary>
	public required string PropertyName { get; set; }

	/// <summary>
	/// القيمة الأولى للتصفية
	/// </summary>
	public object? Value { get; set; }

	/// <summary>
	/// القيمة الثانية (تستخدم في Between)
	/// </summary>
	public object? SecondValue { get; set; }

	/// <summary>
	/// معامل التصفية
	/// </summary>
	public FilterOperator Operator { get; set; } = FilterOperator.Equals;
}
