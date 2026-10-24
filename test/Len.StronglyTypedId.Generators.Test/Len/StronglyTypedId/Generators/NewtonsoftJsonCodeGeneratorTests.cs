// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText / Emit 等均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="NewtonsoftJsonCodeGenerator"/> 的单元测试：覆盖顺序属性、真实 Newtonsoft.Json 引用下的产物形态（[JsonConverter] 特性 +
/// 内嵌 <c>NewtonsoftJsonConverter</c> 的 ReadJson / WriteJson），多 Id 生成，以及消费者可编译性。
/// </summary>
public class NewtonsoftJsonCodeGeneratorTests
{
    #region 辅助：驱动生成器并捕获 Newtonsoft 产物

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

        // 显式 seed 若干程序集，确保它们出现在合成编译引用里（即使尚未被 AppDomain 延迟加载）：
        //   · Newtonsoft.Json  —— 门控触发来源 + 产物里 JsonConverter / JsonReader / JsonWriter 的来源；
        //   · System.Text.Json —— 核心 SystemTextJson 生成器随引用自动启用，需纳入。
        var seedAssemblies = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
            typeof(Newtonsoft.Json.JsonSerializer).Assembly,
        };

        // 排除会触发其它集成生成器的门控程序集（EF Core / Swagger / Dapper / OpenApi / Mvc），避免本用例被它们带偏；
        // 真实 Newtonsoft.Json 程序集刻意保留，因为本测试要验证 Newtonsoft 产物可编译。
        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seedAssemblies)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic)
            .Where(ShouldReferenceAssembly)
            .Select(AssemblyReferenceCache.TryResolveReference)
            .OfType<MetadataReference>()
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            "Len.StronglyTypedId.Newtonsoft.Test",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Dictionary<string, string> GetGeneratedCode(string sourceCode, params MetadataReference[] extraReferences)
    {
        var compilation = CreateCompilation(sourceCode, extraReferences);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .ToDictionary(source => source.HintName, source => source.SourceText.ToString());
    }

    private static bool ShouldReferenceAssembly(Assembly assembly)
        => assembly.GetName().Name is not
            ("Len.StronglyTypedId.TestBase"
            or "Microsoft.EntityFrameworkCore"
            or "Swashbuckle.AspNetCore.SwaggerGen"
            or "Microsoft.OpenApi"
            or "Microsoft.AspNetCore.Mvc"
            or "Microsoft.AspNetCore.Mvc.Core"
            or "Microsoft.AspNetCore.Mvc.Abstractions"
            or "Microsoft.AspNetCore.OpenApi"
            or "Microsoft.AspNetCore.Routing"
            or "Dapper");

    private const string IdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 「对象 JSON 形态」（<c>JsonShape = JsonShape.Object</c>）的消费者源码。
    /// </summary>
    private const string ObjectShapeIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(JsonShape = JsonShape.Object)]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 对象形态 + 前缀：<c>value</c> 属性里存规范文本形，读回走 <c>TryParse</c>。
    /// </summary>
    private const string ObjectShapePrefixedIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(Prefix = "ord_", JsonShape = JsonShape.Object)]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 对象形态 + Newtonsoft 不认识的基元（<c>Int128</c>）：同样退回规范文本形。
    /// </summary>
    private const string ObjectShapeBinaryTextIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId(JsonShape = JsonShape.Object)]
        public partial record struct BigId(Int128 Value);
        """;

    #endregion

    #region 顺序属性

    [Fact]
    public void NewtonsoftJson_Should_HaveOrderTwo() => NewtonsoftJsonCodeGenerator.Instance.Order.Should().Be(2);

    #endregion

    #region 真实引用下的产物形态

    [Fact]
    public void NewtonsoftJson_Should_Generate_WhenNewtonsoftReferenced()
    {
        var generated = GetGeneratedCode(IdSource);

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs");
    }

    [Fact]
    public void NewtonsoftJson_Should_DeclareConverterAttributeAndNestedClass()
    {
        var generated = GetGeneratedCode(IdSource);

        var code = generated["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        code.Should().Contain("[global::Newtonsoft.Json.JsonConverter(typeof(NewtonsoftJsonConverter))]")
            .And.Contain("public class NewtonsoftJsonConverter : global::Newtonsoft.Json.JsonConverter<OrderId?>")
            .And.Contain("public override OrderId? ReadJson(")
            .And.Contain("public override void WriteJson(");
    }

    [Fact]
    public void NewtonsoftJson_Should_GenerateReadJsonBody()
    {
        var generated = GetGeneratedCode(IdSource);

        var code = generated["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        code.Should().Contain("serializer.Deserialize<global::System.Guid?>(reader) switch")
            .And.Contain("{ } value when OrderId.TryCreate(value, out var id) => id,")
            .And.Contain("null when (objectType.IsClass || global::System.Nullable.GetUnderlyingType(objectType) is not null) => null,");
    }

    [Fact]
    public void NewtonsoftJson_Should_GenerateWriteJsonBody()
    {
        var generated = GetGeneratedCode(IdSource);

        var code = generated["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        code.Should().Contain("if (value is { Value: var primitiveId })")
            .And.Contain("writer.WriteValue(primitiveId);")
            .And.Contain("writer.WriteNull();");
    }

    [Fact]
    public void NewtonsoftJson_Should_GenerateForEveryId()
    {
        var source = """
            using System;

            namespace Len.StronglyTypedId;

            [StronglyTypedId]
            public partial record struct OrderId(Guid Value);

            [StronglyTypedId]
            public partial record struct ProductId(int Value);
            """;

        var generated = GetGeneratedCode(source);

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs");
        generated.Should().ContainKey("Len.StronglyTypedId.ProductId.NewtonsoftJson.g.cs");

        generated["Len.StronglyTypedId.ProductId.NewtonsoftJson.g.cs"]
            .Should().Contain("public class NewtonsoftJsonConverter : global::Newtonsoft.Json.JsonConverter<ProductId?>")
            .And.Contain("public override ProductId? ReadJson(");
    }

    #endregion

    #region JSON 形态（JsonShape）

    /// <summary>
    /// <c>JsonShape = JsonShape.Object</c> 时，Newtonsoft 产物改为读写 <c>{ "value": … }</c>。
    /// </summary>
    [Fact]
    public void JsonShape_Object_Should_GenerateObjectShape_ForNewtonsoftJson()
    {
        var generated = GetGeneratedCode(ObjectShapeIdSource);

        var code = generated["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        code.Should()
            .Contain("if (reader.TokenType != global::Newtonsoft.Json.JsonToken.StartObject)")
            .And.Contain("var json = global::Newtonsoft.Json.Linq.JObject.Load(reader);")
            // 属性名大小写不敏感：Newtonsoft 对普通 POCO 就是这么匹配的，转换器不能比它更严。
            .And.Contain("var valueToken = json.GetValue(\"value\", global::System.StringComparison.OrdinalIgnoreCase);")
            .And.Contain("if (valueToken is null)")
            .And.Contain("writer.WriteStartObject();")
            .And.Contain("writer.WritePropertyName(\"value\");")
            .And.Contain("writer.WriteEndObject();");
    }

    /// <summary>
    /// 默认（基元形态）下产物不含任何对象形态痕迹——引入本开关不改变既有产物。
    /// </summary>
    [Fact]
    public void JsonShape_Primitive_Should_NotGenerateObjectShape_ForNewtonsoftJson()
    {
        var generated = GetGeneratedCode(IdSource);

        var code = generated["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        code.Should()
            .NotContain("WriteStartObject")
            .And.NotContain("JObject.Load");
    }

    /// <summary>
    /// 对象形态 + 前缀：<c>value</c> 属性里存的是<b>规范文本形</b>（含前缀），读回必须经 <c>TryParse</c>，
    /// 不能走 <c>Create(基元)</c> —— 后者拿到的会是带前缀的字符串，与基元类型对不上。
    /// </summary>
    [Fact]
    public void JsonShape_Object_WithPrefix_Should_ReadValueAsCanonicalText()
    {
        var generated = GetGeneratedCode(ObjectShapePrefixedIdSource);

        var code = generated["Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs"];

        code.Should()
            .Contain("return valueToken.ToObject<string>() switch")
            .And.Contain("{ } text when OrderId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id) => id,")
            .And.NotContain("valueToken.ToObject<global::System.Guid?>()");
    }

    /// <summary>
    /// 对象形态 + Newtonsoft 不认识的基元（<c>Int128</c>）：同样退回规范文本形。
    /// </summary>
    [Fact]
    public void JsonShape_Object_WithNewtonsoftTextPrimitive_Should_ReadValueAsCanonicalText()
    {
        var generated = GetGeneratedCode(ObjectShapeBinaryTextIdSource);

        var code = generated["Len.StronglyTypedId.BigId.NewtonsoftJson.g.cs"];

        code.Should()
            .Contain("return valueToken.ToObject<string>() switch")
            .And.Contain("{ } text when BigId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out var id) => id,");
    }

    #endregion

    #region 消费者可编译性：生成文件在真实 Newtonsoft 引用下必须能编译

    /// <summary>
    /// 回归用例：Newtonsoft 产物在「真实引用 Newtonsoft.Json」的消费者里必须能编译。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_Compile_WhenConsumerReferencesNewtonsoft()
    {
        var compilation = CreateCompilation(IdSource);

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        // 编译「全部」生成产物：Newtonsoft 产物内嵌进 OrderId 的 partial 声明，
        // 必须与核心文件一起编译，Order 的 Value / Create / IParsable 实现才齐全。
        var generatedTrees = result.Results[0].GeneratedSources
            .Select(source => CSharpSyntaxTree.ParseText(source.SourceText.ToString(), path: source.HintName))
            .ToArray();

        using var stream = new MemoryStream();
        var emit = compilation
            .AddSyntaxTrees(generatedTrees)
            .Emit(stream);

        var errors = emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

        errors.Should().BeEmpty(string.Join("\n", errors.Select(error => error.ToString())));
    }

    #endregion
}
