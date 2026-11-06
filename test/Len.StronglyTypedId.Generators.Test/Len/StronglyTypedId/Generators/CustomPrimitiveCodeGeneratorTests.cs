// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
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
    /// 自定义基元把 <c>TryParse</c> 首参声明成非可空 <c>string</c> 时，生成的 Id <c>TryParse</c> 必须先挡 null，
    /// 否则消费方构建里会留下 CS8604（开了 <c>TreatWarningsAsErrors</c> 就直接失败）。
    /// </summary>
    /// <remarks>
    /// 首参接受 null 的基元不加这一档：那会改变它们对空文本的既定处理（见
    /// <see cref="TryParse_Should_NotGuardNull_WhenPrimitiveAcceptsNullText"/>）。
    /// </remarks>
    [Fact]
    public void TryParse_Should_GuardNull_WhenPrimitiveTryParseRejectsNullText()
    {
        var generated = GetGeneratedCode(NonNullableTryParsePrimitiveSource)[CoreHint];

        generated.Should()
            .Contain("value is not null && global::Probe.Email.TryParse(value, provider ?? global::System.Globalization.CultureInfo.InvariantCulture, out var val)");
    }

    /// <summary>
    /// 首参声明为 <c>string?</c> 的自定义基元不加 null 守卫：它自己就能处理 null 文本。
    /// </summary>
    [Fact]
    public void TryParse_Should_NotGuardNull_WhenPrimitiveAcceptsNullText()
    {
        var generated = GetGeneratedCode()[CoreHint];

        generated.Should().NotContain("value is not null && global::Probe.Email.TryParse");
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
    /// 基元把 <c>IFormattable.ToString(string, IFormatProvider)</c> 写成显式接口实现时，生成代码必须改调泛型辅助方法。
    /// </summary>
    /// <remarks>
    /// 显式实现不能按类型名直呼（CS1501）。封装类型常这么写，而 <c>IsFormattable</c> 只看「是否实现接口」，
    /// 因此必须另判「能否直呼」，否则核心产物与四个集成产物一起编译失败。
    /// <para>
    /// 也不能写成 <c>((IFormattable)value).ToString(…)</c>：值类型会装箱，每次调用一次分配。
    /// 泛型辅助方法靠 <c>constrained.</c> 调用消除装箱，故断言里不许出现接口强制转换。
    /// </para>
    /// </remarks>
    [Fact]
    public void ToString_Should_UseGenericHelper_WhenPrimitiveImplementsIFormattableExplicitly()
    {
        var generated = GetGeneratedCode(ExplicitFormattablePrimitiveSource, dapper: true, efCore: true, protobuf: true);

        var core = generated[CoreHint];

        core.Should().Contain("public override string ToString() => global::Len.StronglyTypedId.StronglyTypedIdFormatting.ToInvariantString(Value);");
        core.Should().Contain("global::Len.StronglyTypedId.StronglyTypedIdFormatting.Format(Value, format, formatProvider);");
        core.Should().NotContain("Value.ToString(null,");
        core.Should().NotContain("Value.ToString(format, formatProvider)");
        core.Should().NotContain("((global::System.IFormattable)Value)");

        generated["Probe.OrderId.SystemTextJson.g.cs"]
            .Should().Contain("writer.WriteStringValue(global::Len.StronglyTypedId.StronglyTypedIdFormatting.ToInvariantString(primitiveId));");
        generated["Probe.OrderId.NewtonsoftJson.g.cs"]
            .Should().Contain("writer.WriteValue(global::Len.StronglyTypedId.StronglyTypedIdFormatting.ToInvariantString(primitiveId));");
        generated["Probe.OrderId.Dapper.g.cs"]
            .Should().Contain("parameter.Value = global::Len.StronglyTypedId.StronglyTypedIdFormatting.ToInvariantString(value.Value);");
        generated["Probe.OrderId.EntityFrameworkCore.g.cs"]
            .Should().Contain("base(v => global::Len.StronglyTypedId.StronglyTypedIdFormatting.ToInvariantString(v.Value), val =>");
        generated["Probe.OrderId.Protobuf.g.cs"]
            .Should().Contain("global::Len.StronglyTypedId.StronglyTypedIdFormatting.ToInvariantString(id.Value)");
    }

    /// <summary>
    /// 公开实现两参重载的基元必须继续按类型名直呼：这条路径的产物逐字节不变。
    /// </summary>
    [Fact]
    public void ToString_Should_CallDirectly_WhenPrimitiveExposesPublicOverload()
    {
        var generated = GetGeneratedCode()[CoreHint];

        generated.Should().Contain("public override string ToString() => Value.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;");
        generated.Should().Contain("Value.ToString(format, formatProvider);");
        generated.Should().NotContain("((global::System.IFormattable)Value)");
    }

    /// <summary>
    /// 显式实现 <c>IFormattable</c> 的基元在真实引用下必须零错误 Emit。
    /// </summary>
    /// <remarks>
    /// 只带真实程序集（STJ / Newtonsoft / protobuf-net）：Dapper 与 EF Core 在本工程里是合成占位程序集，
    /// 没有可供 Emit 的 API 面，它们的产物形态由上面那条文本断言锁住。
    /// </remarks>
    [Fact]
    public void ExplicitFormattablePrimitive_Should_Compile_WithJsonAndProtobufIntegrations()
    {
        var compilation = CreateCompilation(ExplicitFormattablePrimitiveSource, dapper: false, efCore: false, protobuf: true);

        CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        using var stream = new MemoryStream();
        var emit = outputCompilation.Emit(stream);

        var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        errors.Should().BeEmpty(string.Join("\n", errors.Select(error => error.ToString())));
    }

    /// <summary>
    /// 基元把 <c>IComparable&lt;TSelf&gt;.CompareTo</c> 写成显式接口实现时，生成代码必须改走
    /// <c>Comparer&lt;T&gt;.Default</c>。
    /// </summary>
    /// <remarks>
    /// 显式实现不能按类型名直呼：<c>Value.CompareTo(other.Value)</c> 会绑定到
    /// <c>MemoryExtensions.CompareTo(ReadOnlySpan&lt;char&gt;, ReadOnlySpan&lt;char&gt;, StringComparison)</c>
    /// 这个扩展方法上，报 CS7036。封装类型常把比较写成显式实现，而 <c>IsCustomPrimitive</c> 只看
    /// 「是否实现接口」，因此必须另判「能否直呼」。
    /// </remarks>
    [Fact]
    public void CompareTo_Should_UseComparerDefault_WhenPrimitiveImplementsIComparableExplicitly()
    {
        var generated = GetGeneratedCode(ExplicitComparablePrimitiveSource)[CoreHint];

        generated.Should().Contain("global::System.Collections.Generic.Comparer<global::Probe.Email>.Default.Compare(Value, other.Value)");
        generated.Should().NotContain("Value.CompareTo(other.Value)");
    }

    /// <summary>
    /// 公开实现 <c>CompareTo</c> 的基元必须继续按类型名直呼：这条路径的产物逐字节不变。
    /// </summary>
    [Fact]
    public void CompareTo_Should_CallDirectly_WhenPrimitiveExposesPublicOverload()
    {
        var generated = GetGeneratedCode()[CoreHint];

        generated.Should().Contain("Value.CompareTo(other.Value)");
        generated.Should().NotContain("Comparer<global::Probe.Email>.Default");
    }

    /// <summary>
    /// 显式实现 <c>IComparable&lt;TSelf&gt;</c> 的基元在真实引用下必须零错误 Emit。
    /// </summary>
    [Fact]
    public void ExplicitComparablePrimitive_Should_Compile()
    {
        var compilation = CreateCompilation(ExplicitComparablePrimitiveSource, dapper: false, efCore: false);

        CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        using var stream = new MemoryStream();
        var emit = outputCompilation.Emit(stream);

        var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();

        errors.Should().BeEmpty(string.Join("\n", errors.Select(error => error.ToString())));
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
        converter.Should().Contain("base(v => v.Value.ToString(), val => CreateFromProvider(val))");
        converter.Should().Contain("private static global::Probe.OrderId CreateFromProvider(string val)");
        converter.Should().Contain("global::Probe.Email.TryParse(val, global::System.Globalization.CultureInfo.InvariantCulture, out var value)");
        converter.Should().Contain("global::Probe.OrderId.TryCreate(value, out var id)");
    }

    /// <summary>
    /// 引用类型基元的 provider 类型是 <c>string?</c>，而自定义基元的 <c>TryParse</c> 常把首参声明成非可空
    /// <c>string</c>。EF Core 的还原条件必须先挡 null，否则消费方构建里会留下 CS8604。
    /// </summary>
    [Fact]
    public void EfCore_Should_GuardNull_WhenPrimitiveTryParseRejectsNullText()
    {
        var converter = GetGeneratedCode(NonNullableReferenceTypePrimitiveSource, efCore: true)["Probe.OrderId.EntityFrameworkCore.g.cs"];

        converter.Should().Contain("val is not null && global::Probe.Email.TryParse(val, global::System.Globalization.CultureInfo.InvariantCulture, out var value)");
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

    /// <summary>
    /// 引用类型基元（自定义 <c>class</c>）的实例可以是 <see langword="null"/>：<c>default(TId)</c> 与
    /// <c>TryCreate</c> / <c>TryParse</c> 失败的结果都是它。四条文本写出路径都必须判空，
    /// 否则 <c>ToString()</c> 会抛 <see cref="NullReferenceException"/>。
    /// </summary>
    /// <remarks>
    /// 值类型基元（含自定义 <c>struct</c>）的产物逐字节不变，故判空只出现在引用类型基元上。
    /// EF Core 的判空必须写成 <c>== null</c>：转换器 lambda 会被编译成表达式树，而表达式树不允许
    /// <c>is null</c> 模式匹配（CS8122）。
    /// </remarks>
    [Fact]
    public void ReferenceTypePrimitive_Should_GuardNull_OnEveryTextWritePath()
    {
        var generated = GetGeneratedCode(ReferenceTypePrimitiveSource, dapper: true, efCore: true, protobuf: true);

        generated[CoreHint]
            .Should().Contain("public override string ToString() => Value?.ToString() ?? string.Empty;");

        generated["Probe.OrderId.SystemTextJson.g.cs"]
            .Should().Contain("writer.WriteStringValue((primitiveId == null ? null : primitiveId.ToString()));");

        generated["Probe.OrderId.NewtonsoftJson.g.cs"]
            .Should().Contain("writer.WriteValue((primitiveId == null ? null : primitiveId.ToString()));");

        generated["Probe.OrderId.Dapper.g.cs"]
            .Should().Contain("parameter.Value = value.Value is null ? (object)global::System.DBNull.Value : value.Value.ToString();");

        generated["Probe.OrderId.EntityFrameworkCore.g.cs"]
            .Should().Contain("base(v => (v.Value == null ? null : v.Value.ToString()), val =>");

        generated["Probe.OrderId.Protobuf.g.cs"]
            .Should().Contain("(id.Value == null ? null : id.Value.ToString())");
    }

    /// <summary>
    /// 引用类型基元的实例可以是 <see langword="null"/>，比较必须走 <c>Comparer&lt;T&gt;.Default</c>：
    /// 直呼 <c>Value.CompareTo(other.Value)</c> 会在 <c>default(TId)</c> 上抛 <see cref="NullReferenceException"/>。
    /// </summary>
    /// <remarks>
    /// 值类型基元（含自定义 <c>struct</c>）的产物逐字节不变，故这条只对引用类型基元成立。
    /// </remarks>
    [Fact]
    public void ReferenceTypePrimitive_Should_UseComparer_ForComparison()
    {
        var generated = GetGeneratedCode(ReferenceTypePrimitiveSource);

        generated[CoreHint]
            .Should().Contain("public int CompareTo(OrderId other) => global::System.Collections.Generic.Comparer<global::Probe.Email>.Default.Compare(Value, other.Value);")
            .And.Contain("public static bool operator <(OrderId left, OrderId right) => global::System.Collections.Generic.Comparer<global::Probe.Email>.Default.Compare(left.Value, right.Value) < 0;")
            .And.NotContain("Value.CompareTo(other.Value)");
    }

    /// <summary>
    /// 引用类型基元实现 <c>IFormattable</c> 时，带格式的 <c>ToString</c> 与 <c>TryFormat</c> 同样要容忍
    /// <see langword="null"/> 基元：它们与无参 <c>ToString()</c> 同属规范文本形。
    /// </summary>
    /// <remarks>
    /// 空基元按空文本处理：<c>TryFormat</c> 成功写入 0 个字符（有前缀时只写前缀），与 <c>ToString()</c>
    /// 写出空串一致。值类型基元的产物逐字节不变。
    /// </remarks>
    [Fact]
    public void ReferenceTypePrimitive_Should_GuardNull_OnFormattedWritePaths()
    {
        var generated = GetGeneratedCode(FormattableReferenceTypePrimitiveSource);

        generated[CoreHint]
            .Should().Contain("public string ToString(string? format, global::System.IFormatProvider? formatProvider) =>")
            .And.Contain("Value is null ? string.Empty : Value.ToString(")
            .And.Contain("if (Value is null)")
            .And.Contain("return true;");
    }

    /// <summary>
    /// 引用类型基元为 <see langword="null"/> 的 Id 不能作 JSON 字典键：JSON 属性名不允许 null。
    /// 生成代码必须先拦下来给出明确的 <see cref="NotSupportedException"/>，而不是在
    /// <c>WritePropertyName</c> 里抛 NRE。
    /// </summary>
    /// <remarks>
    /// 带前缀时键文本是「前缀 + 空串」，永远非 null，故不生成这一档。
    /// </remarks>
    [Fact]
    public void ReferenceTypePrimitive_Should_RejectNull_When_UsedAsJsonPropertyName()
    {
        var generated = GetGeneratedCode(ReferenceTypePrimitiveSource);

        generated["Probe.OrderId.SystemTextJson.g.cs"]
            .Should().Contain("if (primitiveId is null)")
            .And.Contain("throw new global::System.NotSupportedException($\"A null OrderId cannot be written as a JSON property name.\");");
    }

    /// <summary>
    /// 值类型 Id 包装引用类型基元时，基元可以是 <see langword="null"/>，写侧已把它写成 nil / BSON null /
    /// 空载荷。读侧必须还原成 <c>default</c>，否则「写出去就读不回来」。
    /// </summary>
    /// <remarks>
    /// 判定依据是「基元是否为引用类型」，与 Id 是 <c>record struct</c> 还是 <c>record class</c> 无关 ——
    /// 与 STJ / Newtonsoft 的既有口径一致。值类型基元的产物逐字节不变，仍按契约拒绝 nil。
    /// </remarks>
    [Fact]
    public void ReferenceTypePrimitive_Should_ReadBackDefault_FromNil_ForValueTypeId()
    {
        var generated = GetGeneratedCode(ReferenceTypePrimitiveSource, protobuf: true, messagePack: true, mongoDb: true);

        generated["Probe.OrderId.MessagePack.g.cs"]
            .Should().Contain("if (reader.TryReadNil())")
            .And.Contain("return default;")
            .And.NotContain("Cannot convert a nil MessagePack value to a OrderId.");

        generated["Probe.OrderId.MongoDb.g.cs"]
            .Should().Contain("if (context.Reader.CurrentBsonType == global::MongoDB.Bson.BsonType.Null)")
            .And.Contain("return default;")
            .And.NotContain("Cannot convert a BSON null to a OrderId.");

        generated["Probe.OrderId.Protobuf.g.cs"]
            .Should().Contain("if (surrogate.Value is null)")
            .And.Contain("return default;");
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

    /// <summary>
    /// 把 <c>IFormattable.ToString(string, IFormatProvider)</c> 写成显式接口实现的自定义基元。
    /// </summary>
    /// <remarks>
    /// 封装类型常这么写：公开面只留无参 <c>ToString()</c>，带格式的重载藏在接口后面。
    /// 生成代码若按类型名直呼两参重载会 CS1501。
    /// </remarks>
    private const string ExplicitFormattablePrimitiveSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

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

            string IFormattable.ToString(string? format, IFormatProvider? formatProvider) => Value;
        }

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    /// <summary>
    /// 把 <c>IComparable&lt;TSelf&gt;.CompareTo</c> 写成显式接口实现的自定义基元。
    /// </summary>
    /// <remarks>
    /// 封装类型常这么写：公开面只留非泛型 <c>CompareTo(object)</c>，泛型重载藏在接口后面。
    /// 生成代码若按类型名直呼泛型重载会 CS7036（绑定到 <c>MemoryExtensions.CompareTo</c>）。
    /// </remarks>
    private const string ExplicitComparablePrimitiveSource = """
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

            int IComparable<Email>.CompareTo(Email other) => string.CompareOrdinal(Value, other.Value);

            int IComparable.CompareTo(object? obj) => obj is Email other ? ((IComparable<Email>)this).CompareTo(other) : throw new ArgumentException();

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
    /// 引用类型基元（<c>class</c>）：实例可以是 <see langword="null"/>，四条文本写出路径都必须判空。
    /// </summary>
    private const string ReferenceTypePrimitiveSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public sealed class Email : IParsable<Email>, IComparable<Email>, IComparable
        {
            private Email(string value) => Value = value;

            public string Value { get; }

            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out Email result)
            {
                if (string.IsNullOrEmpty(s))
                {
                    result = null!;
                    return false;
                }

                result = new Email(s);
                return true;
            }

            public int CompareTo(Email? other) => string.CompareOrdinal(Value, other?.Value);

            public int CompareTo(object? obj) => CompareTo(obj as Email);

            public override string ToString() => Value;
        }

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    /// <summary>
    /// 引用类型基元，且 <c>TryParse</c> 首参声明成非可空 <c>string</c>：EF Core 的还原条件必须先挡 null。
    /// </summary>
    private const string NonNullableReferenceTypePrimitiveSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public sealed class Email : IParsable<Email>, IComparable<Email>, IComparable
        {
            private Email(string value) => Value = value;

            public string Value { get; }

            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out Email result)
            {
                result = new Email(s);
                return true;
            }

            public int CompareTo(Email? other) => string.CompareOrdinal(Value, other?.Value);

            public int CompareTo(object? obj) => CompareTo(obj as Email);

            public override string ToString() => Value;
        }

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    /// <summary>
    /// 引用类型基元（<c>class</c>）且实现 <c>ISpanFormattable</c>：带格式的写出路径同样要容忍 null 基元。
    /// </summary>
    private const string FormattableReferenceTypePrimitiveSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public sealed class Email : IParsable<Email>, IComparable<Email>, IComparable, ISpanFormattable
        {
            private Email(string value) => Value = value;

            public string Value { get; }

            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string? s, IFormatProvider? provider, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out Email result)
            {
                if (string.IsNullOrEmpty(s))
                {
                    result = null!;
                    return false;
                }

                result = new Email(s);
                return true;
            }

            public int CompareTo(Email? other) => string.CompareOrdinal(Value, other?.Value);

            public int CompareTo(object? obj) => CompareTo(obj as Email);

            public override string ToString() => Value;

            public string ToString(string? format, IFormatProvider? formatProvider) => Value;

            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            {
                if (!Value.AsSpan().TryCopyTo(destination))
                {
                    charsWritten = 0;
                    return false;
                }

                charsWritten = Value.Length;
                return true;
            }
        }

        [StronglyTypedId]
        public partial record struct OrderId(Email Value);
        """;

    /// <summary>
    /// 自定义基元把 <c>TryParse</c> 首参声明成非可空 <c>string</c>（编译器只给 CS8767，仍满足契约）。
    /// </summary>
    private const string NonNullableTryParsePrimitiveSource = """
        using System;
        using Len.StronglyTypedId;

        namespace Probe;

        public readonly record struct Email(string Value) : IParsable<Email>, IComparable<Email>, IComparable
        {
            public static Email Parse(string s, IFormatProvider? provider) => new(s);

            public static bool TryParse(string s, IFormatProvider? provider, out Email result)
            {
                result = new Email(s);
                return true;
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
    private static Dictionary<string, string> GetGeneratedCode(
        string? source = null,
        bool dapper = false,
        bool efCore = false,
        bool protobuf = false,
        bool messagePack = false,
        bool mongoDb = false)
    {
        var compilation = CreateCompilation(source ?? DefaultSource, dapper, efCore, protobuf, messagePack, mongoDb);

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

    private static CSharpCompilation CreateCompilation(
        string sourceCode,
        bool dapper,
        bool efCore,
        bool protobuf = false,
        bool messagePack = false,
        bool mongoDb = false)
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

        // protobuf-net 的 surrogate 是核心产物（特性只能加在本次编译声明的类型上），同样按引用门控。
        // 按路径建引用而不是 typeof(...).Assembly：后者会把程序集加载进 AppDomain，于是本工程其余用例
        // 的 AppDomain 扫描也会带上它。门控查的是 protobuf-net.dll，特性在 protobuf-net.Core.dll。
        if (protobuf)
        {
            references.Add(AssemblyReferenceCache.FromFile(Path.Combine(AppContext.BaseDirectory, "protobuf-net.Core.dll")));
            references.Add(AssemblyReferenceCache.FromFile(Path.Combine(AppContext.BaseDirectory, "protobuf-net.dll")));
        }

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

        // MessagePack / MongoDB 集成同样按「引用了对应程序集」门控；用合成程序集满足门控。
        if (messagePack)
        {
            references.Add(SyntheticAssembly.GetOrCreate("MessagePack", "3.0.0.0"));
        }

        if (mongoDb)
        {
            references.Add(SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.0.0.0"));
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
