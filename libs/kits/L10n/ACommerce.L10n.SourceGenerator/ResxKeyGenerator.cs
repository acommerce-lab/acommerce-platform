using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.CodeAnalysis;

namespace ACommerce.L10n.SourceGenerator
{

/// <summary>
/// IIncrementalGenerator يَقرأ مَلَفّات <c>.resx</c> NEUTRAL (بِلا culture
/// suffix) المُمَرَّرَة عَبر <c>AdditionalFiles</c> ويُوَلِّد class مُرافِقَة
/// تَحوي ثوابِت <see cref="ACommerce.L10n.Blazor.TranslationKey"/>.
///
/// <para><b>التَفعيل في csproj</b>:
/// <code>
/// &lt;ItemGroup&gt;
///   &lt;ProjectReference Include="...\ACommerce.L10n.SourceGenerator.csproj"
///                       OutputItemType="Analyzer"
///                       ReferenceOutputAssembly="false" /&gt;
///   &lt;AdditionalFiles Include="Resources\Strings.resx" /&gt;
/// &lt;/ItemGroup&gt;
/// &lt;PropertyGroup&gt;
///   &lt;L10nClassName&gt;Strings&lt;/L10nClassName&gt;
///   &lt;L10nNamespace&gt;MyApp.Resources&lt;/L10nNamespace&gt;
/// &lt;/PropertyGroup&gt;
/// </code>
/// </para>
///
/// <para><b>النَواتِج</b>: مَلَفّ مُوَلَّد <c>{ClassName}.g.cs</c> لِكلّ
/// <c>.resx</c> NEUTRAL في AdditionalFiles. مَلَفّات culture variants
/// (<c>Strings.ar.resx</c>) تُتَجاهَل — تُقرَأ runtime عَبر ResourceManager.</para>
///
/// <para><b>تَسمية الـ properties</b>: <c>home.title</c> ⇒ <c>HomeTitle</c>
/// (PascalCase). أيّ مِفتاح يَبدأ بِرَقم يُسبَق بِـ <c>_</c>. أيّ حَرف غير صالِح
/// (#, @, …) يُستَبدَل بِـ <c>_</c>.</para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ResxKeyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. اقرأ الـ msbuild props التي يُمكِن لِلتَطبيق ضَبطها.
        var classNameProvider = context.AnalyzerConfigOptionsProvider.Select((opts, _) =>
            opts.GlobalOptions.TryGetValue("build_property.L10nClassName", out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : null);
        var namespaceProvider = context.AnalyzerConfigOptionsProvider.Select((opts, _) =>
            opts.GlobalOptions.TryGetValue("build_property.L10nNamespace", out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : null);
        var rootNamespaceProvider = context.AnalyzerConfigOptionsProvider.Select((opts, _) =>
            opts.GlobalOptions.TryGetValue("build_property.RootNamespace", out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : "Generated");

        // 2. اجمَع مَلَفّات الـ AdditionalFiles ذات الامتِداد .resx ⇒ NEUTRAL only.
        var resxFiles = context.AdditionalTextsProvider
            .Where(static at => IsNeutralResx(at.Path))
            .Select(static (at, ct) =>
            {
                var content = at.GetText(ct)?.ToString();
                if (string.IsNullOrWhiteSpace(content)) return default;
                var keys = ExtractResxKeys(content!);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(at.Path);
                return new ResxFile(fileNameWithoutExt!, keys);
            })
            .Where(static rf => rf.FileName is not null);

        var combined = resxFiles.Combine(classNameProvider.Combine(namespaceProvider.Combine(rootNamespaceProvider)));

        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var resx = tuple.Left;
            var (overrideClass, (overrideNs, rootNs)) = tuple.Right;
            if (resx.FileName is null || resx.Keys.IsDefaultOrEmpty) return;

            var className = overrideClass ?? resx.FileName;
            var ns = overrideNs ?? $"{rootNs}.Resources";
            var source = Render(ns, className, resx.Keys, resx.FileName);
            spc.AddSource($"{className}.g.cs", source);
        });
    }

    private static bool IsNeutralResx(string path)
    {
        if (!path.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)) return false;
        // Strings.ar.resx ⇒ aux culture، نَتَجاوَزه. NEUTRAL = مَلَفّ بِامتِداد
        // واحِد فَقَط (Strings.resx) — اسم المَلَفّ بدون .resx لا يَحوي نُقطَة.
        var name = Path.GetFileNameWithoutExtension(path);
        return !name.Contains('.');
    }

    private static ImmutableArray<string> ExtractResxKeys(string xmlContent)
    {
        var keys = new List<string>();
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true,
                IgnoreWhitespace = true,
            };
            using var reader = XmlReader.Create(new StringReader(xmlContent), settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "data")
                {
                    var name = reader.GetAttribute("name");
                    if (!string.IsNullOrWhiteSpace(name)) keys.Add(name!);
                }
            }
        }
        catch
        {
            // resx مَكسور ⇒ نَتَجاوَز بِـ keys فارِغَة.
        }
        return keys.ToImmutableArray();
    }

    private static string Render(string ns, string className, ImmutableArray<string> keys, string sourceFileName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine($"//   Source: {sourceFileName}.resx");
        sb.AppendLine("//   Generated by ACommerce.L10n.SourceGenerator");
        sb.AppendLine("//   Do not edit manually — regenerated on build.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Typed TranslationKey constants generated from <c>{sourceFileName}.resx</c>.");
        sb.AppendLine("/// Each <c>data name=</c> entry yields one constant. Use as");
        sb.AppendLine($"/// <c>L[{className}.HomeTitle]</c> for compile-time safety.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var propName = ToPropertyName(key);
            // تَجَنُّب تَصادُم: لَو propName مُكَرَّر (مَفاتيح مُختَلِفَة تُعطي
            // نَفس PascalCase) نُلحِق hash تَفريد.
            if (!seen.Add(propName))
            {
                propName += "_" + Math.Abs(key.GetHashCode()).ToString("X");
                seen.Add(propName);
            }
            var safeKey = key.Replace("\"", "\\\"");
            sb.AppendLine($"    /// <summary>resx key: <c>{System.Security.SecurityElement.Escape(key)}</c></summary>");
            sb.AppendLine($"    public static readonly global::ACommerce.L10n.Blazor.TranslationKey {propName}");
            sb.AppendLine($"        = new global::ACommerce.L10n.Blazor.TranslationKey(\"{safeKey}\");");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// <c>home.title</c> ⇒ <c>HomeTitle</c>. <c>auth.otp.hint</c> ⇒ <c>AuthOtpHint</c>.
    /// أيّ حَرف غير alphanumeric يُعامَل كَ separator.
    /// </summary>
    private static string ToPropertyName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "_Empty";
        var parts = Regex.Split(key, "[^A-Za-z0-9]+")
            .Where(p => p.Length > 0)
            .Select(Capitalize)
            .ToArray();
        if (parts.Length == 0) return "_Empty";
        var name = string.Concat(parts);
        // C# identifier قَواعِد: لا يَبدأ بِرَقم
        if (char.IsDigit(name[0])) name = "_" + name;
        return name;
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private readonly struct ResxFile
    {
        public ResxFile(string? fileName, ImmutableArray<string> keys)
        {
            FileName = fileName;
            Keys = keys;
        }
        public string? FileName { get; }
        public ImmutableArray<string> Keys { get; }
    }
}

} // namespace ACommerce.L10n.SourceGenerator
