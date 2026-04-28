namespace ACommerce.SharedKernel.Abstractions.Queries;

/// <summary>
/// معاملات التصفية المتاحة
/// </summary>
public enum FilterOperator
{
	/// <summary>
	/// المساواة التامة
	/// </summary>
	Equals,

	/// <summary>
	/// عدم المساواة
	/// </summary>
	NotEquals,

	/// <summary>
	/// يحتوي على (للنصوص)
	/// </summary>
	Contains,

	/// <summary>
	/// يبدأ بـ (للنصوص)
	/// </summary>
	StartsWith,

	/// <summary>
	/// ينتهي بـ (للنصوص)
	/// </summary>
	EndsWith,

	/// <summary>
	/// أكبر من
	/// </summary>
	GreaterThan,

	/// <summary>
	/// أقل من
	/// </summary>
	LessThan,

	/// <summary>
	/// أكبر من أو يساوي
	/// </summary>
	GreaterThanOrEqual,

	/// <summary>
	/// أقل من أو يساوي
	/// </summary>
	LessThanOrEqual,

	/// <summary>
	/// بين قيمتين
	/// </summary>
	Between,

	/// <summary>
	/// موجود في قائمة
	/// </summary>
	In,

	/// <summary>
	/// غير موجود في قائمة
	/// </summary>
	NotIn,

	/// <summary>
	/// قيمة فارغة (NULL)
	/// </summary>
	IsNull,

	/// <summary>
	/// قيمة غير فارغة (NOT NULL)
	/// </summary>
	IsNotNull
}
