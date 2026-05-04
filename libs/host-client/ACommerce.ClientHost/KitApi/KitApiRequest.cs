namespace ACommerce.ClientHost.KitApi;

/// <summary>
/// طلب HTTP من kit api client. تُعطى للـ analyzers و interceptors.
/// </summary>
public sealed class KitApiRequest
{
    public required string KitName { get; init; }
    public required string Method  { get; init; }     // "GET", "POST", ...
    public required string Path    { get; init; }     // "/listings"
    public object? Body { get; init; }
    /// <summary>tags إضافيّة (مَصدر الزرّ، تَتَبّع، …) — يَستهلكها interceptors.</summary>
    public Dictionary<string, string> Tags { get; } = new();
}

/// <summary>الردّ الخامّ بعد الـ HTTP — قبل تَقشير الـ envelope.</summary>
public sealed class KitApiResponse
{
    public required int     StatusCode { get; init; }
    public required string  RawBody    { get; init; }
    public required bool    IsSuccess  { get; init; }
    public Exception?       Exception  { get; init; }
}
