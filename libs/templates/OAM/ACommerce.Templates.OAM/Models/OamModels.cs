namespace ACommerce.Templates.OAM.Models;

/// <summary>الحالة المرئيّة لعمليّة. تَتحوّل لـ ختم بصريّ.</summary>
public enum OamState
{
    /// <summary>قيد الإعداد — لم تُرسَل.</summary>
    Pending,
    /// <summary>قيد التَنفيذ — أُرسلت.</summary>
    Executing,
    /// <summary>نُفِّذت بنجاح.</summary>
    Executed,
    /// <summary>رُفضت — analyzer أخفق.</summary>
    Rejected,
}

/// <summary>سَطر داخل OamReceipt — tag = key/value pair.</summary>
public sealed record OamTag(string Key, string Value);

/// <summary>سَطر داخل OamLedger.</summary>
public sealed record OamLedgerEntry(
    string Time,
    string Operation,
    string From,
    string To,
    string Amount,
    OamState State = OamState.Executed);

/// <summary>سَطر داخل أحد عمودَي OamTAccount.</summary>
public sealed record OamTAccountLine(string Label, string Amount);

/// <summary>analyzer واحد في OamAnalyzerStrip.</summary>
public sealed record OamAnalyzer(
    string Label,
    OamAnalyzerState State,
    string? Hint = null);

public enum OamAnalyzerState
{
    /// <summary>لم يُختَبَر بعد.</summary>
    Idle,
    /// <summary>قيد التَحقّق.</summary>
    Checking,
    /// <summary>نَجح.</summary>
    Pass,
    /// <summary>أخفق — العمليّة محظورة.</summary>
    Fail,
}
