using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// N12：自定义基元的生成器文本契约。
/// </summary>
/// <remarks>
/// 这些断言锁的是「产物长什么样」，而不是「跑起来对不对」—— 后者由运行时用例覆盖。
/// 关键点是自定义基元必须走规范文本形：交给序列化器会按公开属性写出、读回 <c>default</c>，
/// 属于「看起来成功了、值已经丢了」这类最难排查的形态。
/// </remarks>
public class CustomPrimitiveCodeGeneratorTests
{
    /// <summary>
    /// 自定义基元的 <c>TryParse</c> 走基元自己的 <c>TryParse</c>，span 重载先转成字符串。
    /// </summary>
    [Fact]
    public void TryParse_Should_DelegateToPrimitiveTryParse()
    {
        var generated = GetGeneratedCode()[CoreHint];

        generated.Should().Contain("global::Probe.Email.TryParse(value, provider ?? global::System.Globalization.CultureInfo.InvariantCulture, out var val)");
        generated.Should().Contain("global::Probe.Email.TryParse(value.ToString(), provider ?? global::System.Globalization.CultureInfo.InvariantCulture, out var val)");
    }

    /// <summary>
    /// STJ 转换器写规范文本、读回经基元 <c>TryParse</c>，不出现 <c>JsonSerializer</c> 委托。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_WriteAndReadCanonicalText()
    {
        var converter = GetGeneratedCode()["Probe.OrderId.SystemTextJson.g.cs"];

        converter.Should().Contain("writer.WriteStringValue(primitiveId.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));");
        converter.Should().Contain("global::Probe.Email.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var value)");
        converter.Should().NotContain("JsonSerializer.Serialize(writer, primitiveId, options)");
    }

    /// <summary>
    /// Newtonsoft 转换器同样写规范文本。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_WriteCanonicalText()
    {
        var converter = GetGeneratedCode()["Probe.OrderId.NewtonsoftJson.g.cs"];

        converter.Should().Contain("writer.WriteValue(primitiveId.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture));");
    }

    /// <summary>
    /// 只实现 <c>IParsable</c> 而不实现 <c>IFormattable</c> 的基元不能调 <c>ToString(null, provider)</c>（CS1501），
    /// 必须退回无参 <c>ToString()</c>。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_UseParameterlessToString_WhenPrimitiveIsNotFormattable()
    {
        var converter = GetGeneratedCode(FormattablePrimitiveSource)["Probe.OrderId.SystemTextJson.g.cs"];

        converter.Should().Contain("writer.WriteStringValue(primitiveId.ToString());");
        converter.Should().NotContain("primitiveId.ToString(null,");
    }

    /// <summary>
    /// 自定义基元的命名实例经 <c>CreateInstance</c> 包装，而不是直接 <c>Create(Parse(...))</c>。
    /// </summary>
    [Fact]
    public void NamedInstance_Should_UseCreateInstanceWrapper()
    {
        var generated = GetGeneratedCode(InstanceSource)[CoreHint];

        generated.Should().Contain("public static readonly OrderId Unknown = CreateInstance(\"Unknown\", \"unknown@example.com\");");
        generated.Should().Contain("private static OrderId CreateInstance(string name, string text)");
        generated.Should().Contain("StronglyTypedId: named instance '");
    }

    /// <summary>
    /// 内置基元的产物必须逐字不变：不生成 <c>CreateInstance</c>，实例仍走 <c>Create(字面量)</c>。
    /// </summary>
    [Fact]
    public void NamedInstance_Should_NotEmitCreateInstance_ForBuiltInPrimitive()
    {
        // 这份源码声明在 Len.StronglyTypedId 命名空间下（内置基元的常规位置），hint 名随之不同。
        var generated = GetGeneratedCode(BuiltInInstanceSource)["Len.StronglyTypedId.OrderId.g.cs"];

        generated.Should().NotContain("CreateInstance");
        generated.Should().Contain("public static readonly OrderId Empty = Create(");
    }

    /// <summary>
    /// EF Core 的 provider 类型对自定义基元必须是 <c>string</c>：数据库不认识用户自定义的 CLR 类型，
    /// 直接用基元本身会让 EF 报「The 'X' property could not be mapped because the database provider
    /// does not support this type」。
    /// </summary>
    /// <remarks>
    /// 这份源码的基元只实现 <c>IParsable</c>（无 <c>IFormattable</c>），故写的是无参 <c>ToString()</c>。
    /// </remarks>
    [Fact]
    public void EfCore_Should_UseStringAsProviderType_ForCustomPrimitive()
    {
        var converter = GetGeneratedCode(DapperSource, efCore: true)["Probe.OrderId.EntityFrameworkCore.g.cs"];

        converter.Should().Contain("ValueConverter<global::Probe.OrderId, string>");
        converter.Should().Contain("base(v => v.Value.ToString(), val => global::Probe.OrderId.Create(global::Probe.Email.Parse(val, global::System.Globalization.CultureInfo.InvariantCulture)))");
    }

    /// <summary>
    /// Dapper 的 <c>Parse</c> 对自定义基元走文本入口，不能落到按短名分派的 <c>Convert.ToXxx</c>。
    /// </summary>
    [Fact]
    public void Dapper_Should_UseTextEntryPoint_ForCustomPrimitive()
    {
        var handler = GetGeneratedCode(DapperSource, dapper: true)["Probe.OrderId.Dapper.g.cs"];

        handler.Should().Contain("global::Probe.Email.Parse(global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture)!, global::System.Globalization.CultureInfo.InvariantCulture)");
        handler.Should().NotContain("Convert.ToGuid");
    }

    /// <summary>
    /// Dapper 的 <c>SetValue</c> 对自定义基元写规范文本：驱动不认识自定义 CLR 类型，
    /// 直接塞基元值会在绑定时抛「No mapping exists from object type …」。
    /// </summary>
    /// <remarks>
    /// 这份源码的基元只实现 <c>IParsable</c>（无 <c>IFormattable</c>），故写的是无参 <c>ToString()</c>。
    /// </remarks>
    [Fact]
    public void Dapper_Should_WriteCanonicalText_ForCustomPrimitive()
    {
        var handler = GetGeneratedCode(DapperSource, dapper: true)["Probe.OrderId.Dapper.g.cs"];

        handler.Should().Contain("parameter.Value = value.Value.ToString();");
    }

    #region 源码与辅助

    /// <summary>
    /// 核心产物的 hint 名。
    /// </summary>
    private const string CoreHint = "Probe.OrderId.g.cs";

    /// <summary>
    /// 满足全部可选能力的自定义基元（<c>IParsable</c> + <c>IComparable</c> + <c>IFormattable</c>）。
    /// </summary>
    private const string FormattablePrimitive = """
        public readonly record struct Email(string Value)
            : IParsable<Email>, IComparable<Email>, IComparable, IFormattable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s ?? string.Empty);
                return s is not null;
            }

            public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();

            public override string ToString() => Value;

            public string ToString(string? format, IFormatProvider? formatProvider) => Value;
        }
        """;

    /// <summary>
    /// 只满足最低契约的自定义基元（无 <c>IFormattable</c>）。
    /// </summary>
    private const string FormattablePrimitiveSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s ?? string.Empty);
                return s is not null;
            }

            public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();

            public override string ToString() => Value;
        }

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    private const string InstanceSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s ?? string.Empty);
                return s is not null;
            }

            public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();

            public override string ToString() => Value;
        }

        [StronglyTypedId]
        [StronglyTypedIdInstance("Unknown", "unknown@example.com")]
        public partial record struct OrderId(Email Value);
        """;

    private const string BuiltInInstanceSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        [StronglyTypedIdInstance("Empty", "00000000-0000-0000-0000-000000000000")]
        public partial record struct OrderId(Guid Value);
        """;

    private const string DapperSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s ?? string.Empty);
                return s is not null;
            }

            public int CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            public int CompareTo(object? obj) => obj is Email other ? CompareTo(other) : throw new ArgumentException();

            public override string ToString() => Value;
        }

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    /// <summary>
    /// 跑生成器并把全部产物按 hint 名索引。
    /// </summary>
    private static Dictionary<string, string> GetGeneratedCode(string? source = null, bool dapper = false, bool efCore = false)
    {
        var compilation = CreateCompilation(source ?? DefaultSource, dapper, efCore);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var sources = result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());

        return sources;
    }

    /// <summary>
    /// 默认源码：自定义基元 + 一个 Id。
    /// </summary>
    private static string DefaultSource => $$"""
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        {{FormattablePrimitive}}

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    private static CSharpCompilation CreateCompilation(string sourceCode, bool dapper, bool efCore)
    {
        // 与 StronglyTypedIdGeneratorTests.CreateCompilation 同一套引用：AppDomain 全集减去会污染
        // 发现结果的程序集。只挑几个程序集不够 —— 自定义基元的接口（IParsable / IComparable）与
        // Attribute 基类分散在 System.Runtime 等程序集里，漏一个就是 CS0012。
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(ShouldReferenceAssembly)
            .Select(AssemblyReferenceCache.TryResolveReference)
            .OfType<MetadataReference>()
            .ToList();

        // 运行时库必须显式补上：AppDomain 里未必已加载它（生成器测试不引用运行时类型），
        // 而源码里的 [StronglyTypedId] 需要它才能解析。
        references.Add(AssemblyReferenceCache.FromAssembly(typeof(StronglyTypedIdAttribute).Assembly));

        // STJ / Newtonsoft 转换器由「引用了对应程序集」门控。AppDomain 里未必已加载它们
        // （本测试工程不直接引用 Newtonsoft 的类型），故显式补上，否则只剩核心产物。
        references.Add(AssemblyReferenceCache.FromAssembly(typeof(System.Text.Json.JsonSerializer).Assembly));
        references.Add(AssemblyReferenceCache.FromAssembly(typeof(Newtonsoft.Json.JsonConvert).Assembly));

        // Dapper 集成由「引用了 Dapper.dll」门控；测试工程不直接引用它，用合成程序集满足门控。
        if (dapper)
        {
            references.Add(SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));
        }

        // EF Core 集成由「引用了 Microsoft.EntityFrameworkCore.dll ≥ 7」门控；同样用合成程序集。
        if (efCore)
        {
            references.Add(SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "10.0.0.0"));
        }

        return CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// 生成器测试不应引用以下程序集，否则会触发 EF Core / Swagger 生成器或被 TestBase 中已实现的强类型 Id 污染发现结果。
    /// </summary>
    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase" or "Microsoft.EntityFrameworkCore" or "Swashbuckle.AspNetCore.SwaggerGen" or "Microsoft.OpenApi" or "Microsoft.AspNetCore.Mvc" or "Microsoft.AspNetCore.Mvc.Core" or "Microsoft.AspNetCore.Mvc.Abstractions" or "Microsoft.AspNetCore.OpenApi" or "Microsoft.AspNetCore.Routing");

    #endregion
}
