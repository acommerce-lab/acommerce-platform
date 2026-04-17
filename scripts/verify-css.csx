#!/usr/bin/env dotnet-script
// Usage: dotnet script scripts/verify-css.csx
// Alternative: dotnet run --project scripts/VerifyCss/VerifyCss.csproj
//
// Purpose: Extract every CSS class used in .razor files and verify it's
// defined in at least one .css file. Reports:
//   - Undefined classes (used in razor but missing in CSS) — BUILD BREAKER
//   - Unused classes (defined in CSS but not used in any razor) — WARNING

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

var root = Environment.CurrentDirectory;
var razorFiles = Directory.GetFiles(root, "*.razor", SearchOption.AllDirectories)
    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
    .ToList();
var cssFiles = Directory.GetFiles(root, "*.css", SearchOption.AllDirectories)
    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
    .ToList();

// Extract class names from class="..." attributes (including interpolated)
var classRegex = new Regex(@"class=[""']([^""']+)[""']", RegexOptions.Compiled);
// Handle Razor expressions: class="@(cond ? "x" : "y")" - extract literals
var literalRegex = new Regex(@"""([a-z][a-z0-9-]*(\s+[a-z][a-z0-9-]*)*)""", RegexOptions.Compiled);
// Extract CSS selectors: .class-name
var selectorRegex = new Regex(@"\.([a-z][a-z0-9_-]*)", RegexOptions.Compiled);

var usedClasses = new HashSet<string>();
var usageMap = new Dictionary<string, List<string>>(); // class -> files using it

foreach (var f in razorFiles)
{
    var content = File.ReadAllText(f);
    foreach (Match m in classRegex.Matches(content))
    {
        var raw = m.Groups[1].Value;
        // Extract literal class names (skip @variable parts)
        foreach (var tok in raw.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (tok.StartsWith("@")) continue; // skip razor expressions
            if (tok.Contains("@")) continue;
            var cls = tok.Trim();
            if (!string.IsNullOrEmpty(cls) && char.IsLetter(cls[0]))
            {
                usedClasses.Add(cls);
                if (!usageMap.ContainsKey(cls)) usageMap[cls] = new();
                usageMap[cls].Add(Path.GetRelativePath(root, f));
            }
        }
        // Extract literals inside razor expressions like class="@(x ? "cls-a" : "cls-b")"
        foreach (Match lit in literalRegex.Matches(raw))
        {
            foreach (var tok in lit.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                usedClasses.Add(tok.Trim());
            }
        }
    }
}

var definedClasses = new HashSet<string>();
var definitionMap = new Dictionary<string, List<string>>(); // class -> CSS files defining it

foreach (var f in cssFiles)
{
    var content = File.ReadAllText(f);
    // Strip comments to avoid false positives
    content = Regex.Replace(content, @"/\*.*?\*/", "", RegexOptions.Singleline);
    foreach (Match m in selectorRegex.Matches(content))
    {
        var cls = m.Groups[1].Value;
        definedClasses.Add(cls);
        if (!definitionMap.ContainsKey(cls)) definitionMap[cls] = new();
        var rel = Path.GetRelativePath(root, f);
        if (!definitionMap[cls].Contains(rel)) definitionMap[cls].Add(rel);
    }
}

// Ignore built-in HTML/Bootstrap/user-agent classes that we know aren't ours
var ignoreClasses = new HashSet<string>
{
    // Razor/Blazor built-ins
    "valid", "invalid", "modified",
    // Bootstrap-compat layer (may or may not be defined)
    "sr-only", "visually-hidden",
    // CSS utility classes from widgets (we check these explicitly)
};

var undefined = usedClasses
    .Where(c => !definedClasses.Contains(c))
    .Where(c => !ignoreClasses.Contains(c))
    .OrderBy(c => c)
    .ToList();

var unused = definedClasses
    .Where(c => !usedClasses.Contains(c))
    .OrderBy(c => c)
    .ToList();

Console.WriteLine($"=== CSS Class Verification Report ===");
Console.WriteLine($"Razor files scanned: {razorFiles.Count}");
Console.WriteLine($"CSS files scanned:   {cssFiles.Count}");
Console.WriteLine($"Classes used:        {usedClasses.Count}");
Console.WriteLine($"Classes defined:     {definedClasses.Count}");
Console.WriteLine();

Console.WriteLine($"=== UNDEFINED CLASSES (used but not defined): {undefined.Count} ===");
foreach (var c in undefined)
{
    Console.WriteLine($"  ✗ .{c}");
    if (usageMap.TryGetValue(c, out var files))
    {
        foreach (var file in files.Take(3))
            Console.WriteLine($"      used in: {file}");
    }
}

Console.WriteLine();
Console.WriteLine($"=== UNUSED CLASSES (defined but not used): {unused.Count} (warnings only) ===");
// Only show first 20
foreach (var c in unused.Take(20))
{
    Console.WriteLine($"  ? .{c}  [{string.Join(",", definitionMap[c])}]");
}
if (unused.Count > 20) Console.WriteLine($"  ... and {unused.Count - 20} more");

Environment.Exit(undefined.Count > 0 ? 1 : 0);
