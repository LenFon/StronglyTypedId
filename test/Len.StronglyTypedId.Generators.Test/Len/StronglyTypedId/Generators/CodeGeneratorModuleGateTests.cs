// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 生成器「模块版本闸门」的单元测试：三类生成器各按一个被引用程序集的主版本号决定是否启用。
/// </summary>
public class CodeGeneratorModuleGateTests
{
    #region EF Core：Microsoft.EntityFrameworkCore 主版本 >= 7

    [Fact]
    public void EfCore_Should_Generate_WhenEntityFrameworkCoreMajorVersionIsSeven()
    {
        var generated = RunGenerator(
            EfCoreSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "7.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.EntityFrameworkCore.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    [Fact]
    public void EfCore_Should_NotGenerate_WhenEntityFrameworkCoreMajorVersionIsSix()
    {
        var generated = RunGenerator(
            EfCoreSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "6.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".EntityFrameworkCore.g.cs", StringComparison.Ordinal));
    }

    /// <summary>
    /// 闸门只看主版本：同为 7.x，带补丁号与不带补丁号都必须生成。
    /// </summary>
    [Fact]
    public void EfCore_Should_IgnoreMinorAndPatchVersion()
    {
        var generated = RunGenerator(
            EfCoreSource,
            SyntheticAssembly.GetOrCreate("Microsoft.EntityFrameworkCore", "7.42.13.7"));

        generated.Should().ContainKey("StronglyTypedIds.EntityFrameworkCore.g.cs");
    }

    #endregion

    #region Swagger：Swashbuckle.AspNetCore.SwaggerGen 主版本 >= 6

    [Fact]
    public void Swagger_Should_Generate_WhenSwashbuckleMajorVersionIsSix()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.Swagger.g.cs");
    }

    [Fact]
    public void Swagger_Should_NotGenerate_WhenSwashbuckleMajorVersionIsFive()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "5.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.Swagger.g.cs");
    }

    #endregion

    #region Newtonsoft.Json：主版本 >= 13

    [Fact]
    public void NewtonsoftJson_Should_Generate_WhenMajorVersionIsThirteen()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.NewtonsoftJson.g.cs");
    }

    /// <summary>
    /// Newtonsoft.Json 12.x 仍被广泛使用，但其 <c>JsonConverter&lt;T&gt;</c> 与 13.x 存在差异，生成器据此只在 13+ 产出转换器。阈值若被误写成
    /// 12，本用例即失败。
    /// </summary>
    [Fact]
    public void NewtonsoftJson_Should_NotGenerate_WhenMajorVersionIsTwelve()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".NewtonsoftJson.g.cs", StringComparison.Ordinal));
    }

    #endregion

    #region 闸门互不影响：低于阈值的模块不应连带关闭别的生成器

    [Fact]
    public void CoreGenerators_Should_StayEnabled_WhenOnlyLowVersionModulesReferenced()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"),
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "5.0.0.0"));

        // 核心与 System.Text.Json 产物与被排除的模块无关，必须照常产出。
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.g.cs");
        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.SystemTextJson.g.cs");
    }

    #endregion

    #region Dapper：Dapper 主版本 >= 2

    [Fact]
    public void Dapper_Should_Generate_WhenDapperMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.0.0.0"));

        generated.Should().ContainKey("Len.StronglyTypedId.OrderId.Dapper.g.cs");
        generated.Should().ContainKey("StronglyTypedIds.Dapper.g.cs");
    }

    [Fact]
    public void Dapper_Should_NotGenerate_WhenDapperMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "1.0.0.0"));

        generated.Keys.Should().NotContain(key => key.EndsWith(".Dapper.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Dapper_Should_GenerateTypeHandlerAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"]
            .Should().Contain("class OrderIdTypeHandler : global::Dapper.SqlMapper.TypeHandler<global::Len.StronglyTypedId.OrderId>")
            .And.Contain("public override void SetValue(global::System.Data.IDbDataParameter parameter, global::Len.StronglyTypedId.OrderId value)")
            .And.Contain("public override global::Len.StronglyTypedId.OrderId Parse(object? value)");

        generated["StronglyTypedIds.Dapper.g.cs"]
            .Should().Contain("public static void ApplyTo()")
            .And.Contain("public static void ApplyTo(global::System.Data.IDbConnection connection) => ApplyTo();")
            .And.Contain("global::Dapper.SqlMapper.AddTypeHandler");
    }

    /// <summary>
    /// 引用类型 Id（<c>partial record</c>）的 <c>SetValue</c> 也必须写出基元值。<c>DbParameter.Value</c> 只接受驱动认识的类型，
    /// 直接把 Id 包装对象塞进去会让 SqlClient / Sqlite 在绑定时抛「No mapping exists from object type ...」。
    /// 该分支此前正是这么写的，而测试矩阵里没有任何引用类型 Id 走过 Dapper 路径，缺陷因此长期未被发现。
    /// </summary>
    [Fact]
    public void Dapper_Should_UnwrapPrimitiveValue_ForReferenceTypeId()
    {
        var generated = RunGenerator(
            ClassIdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderClassId.Dapper.g.cs"]
            .Should().Contain("public override void SetValue(global::System.Data.IDbDataParameter parameter, global::Len.StronglyTypedId.OrderClassId? value)")
            .And.Contain("parameter.Value = value is null ? (object)global::System.DBNull.Value : value.Value;");
    }

    /// <summary>
    /// <c>Parse</c> 不能把 <c>object</c> 直接拆箱成基元：驱动返回的 CLR 类型常常不是基元本身
    /// （SQLite 下枚举列是 <c>long</c>、<c>char</c> / <c>Guid</c> 列是 <c>string</c>、<c>decimal</c> 列是 <c>double</c>），
    /// 裸强转会在读取时抛 <see cref="InvalidCastException"/>。本用例逐个基元锁定转换分支。
    /// </summary>
    [Fact]
    public void Dapper_Should_ConvertProviderShapedValues_InsteadOfUnboxing()
    {
        var generated = RunGenerator(
            MixedPrimitiveIdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        // 枚举：底层整数走 Enum.ToObject，成员名文本走 Enum.Parse。
        generated["Len.StronglyTypedId.StatusId.Dapper.g.cs"]
            .Should().Contain("if (value is string enumText)")
            .And.Contain("return global::Len.StronglyTypedId.StatusId.Create((global::Len.StronglyTypedId.OrderStatus)global::System.Enum.ToObject(typeof(global::Len.StronglyTypedId.OrderStatus), value!));");

        // char：TEXT 列返回 string，IConvertible.ToChar 正好按「长度恰为 1」判定。
        generated["Len.StronglyTypedId.LetterId.Dapper.g.cs"]
            .Should().Contain("global::System.Convert.ToChar(value, global::System.Globalization.CultureInfo.InvariantCulture)");

        // decimal：REAL 列返回 double。
        generated["Len.StronglyTypedId.AmountId.Dapper.g.cs"]
            .Should().Contain("global::System.Convert.ToDecimal(value, global::System.Globalization.CultureInfo.InvariantCulture)");

        // Guid：TEXT 列返回 string，走 IParsable 的文本入口。
        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"]
            .Should().Contain("global::System.Guid.Parse(global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture)!, global::System.Globalization.CultureInfo.InvariantCulture)");

        // 任何基元都不许再出现「拆箱强转」形态。
        generated.Values.Should().NotContain(code => code.Contains(")value!)", StringComparison.Ordinal));
    }

    /// <summary>
    /// <c>Parse</c> 方法体必须与声明同列（8 空格），语句 12 空格、嵌套块 16 空格。
    /// 多行插值洞不会随模板重新缩进，此前产物里 <c>{</c> 在 12 列而语句在 4 列。
    /// </summary>
    [Fact]
    public void Dapper_ParseBody_Should_KeepConsistentIndentation()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["Len.StronglyTypedId.OrderId.Dapper.g.cs"].Replace("\r\n", "\n")
            .Should().Contain("\n        public override global::Len.StronglyTypedId.OrderId Parse(object? value)\n        {\n            if (value is null or global::System.DBNull)\n            {\n                return default;\n            }\n");
    }

    /// <summary>
    /// 注册入口的方法体此前用制表符缩进，与其余生成物的 4 空格风格不一致。
    /// </summary>
    [Fact]
    public void Dapper_ApplyTo_Should_UseSpacesForIndentation()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Dapper", "2.1.0.0"));

        generated["StronglyTypedIds.Dapper.g.cs"].Replace("\r\n", "\n")
            .Should().Contain("\n    {\n        global::Dapper.SqlMapper.AddTypeHandler<global::Len.StronglyTypedId.OrderId>(new OrderIdTypeHandler());\n    }")
            .And.NotContain("\t");
    }

    #endregion

    #region Microsoft.AspNetCore.OpenApi：主版本 >= 9

    [Fact]
    public void AspNetCoreOpenApi_Should_Generate_WhenMajorVersionIsNine()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_NotGenerate_WhenMajorVersionIsEight()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "8.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    /// <summary>
    /// 8.0 线上没有 schema 转换器 API（<c>IOpenApiSchemaTransformer</c> / <c>OpenApiOptions</c> 都是 9.0.0
    /// 才引入的），OpenAPI 文档由 Swashbuckle 产出。本用例锁定这条替代路径：同一个编译里
    /// OpenApi 8.0 + Swashbuckle 6 时，只产出 Swagger 入口，不产出 OpenAPI 入口 —— 否则消费者会拿到
    /// 一份引用 8.0 里不存在类型的产物，编译直接 CS0246。
    /// </summary>
    [Fact]
    public void AspNetCoreOpenApi_Should_FallBackToSwagger_WhenMajorVersionIsEight()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "8.0.0.0"),
            SyntheticAssembly.GetOrCreate("Swashbuckle.AspNetCore.SwaggerGen", "6.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.Swagger.g.cs");
        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    /// <summary>
    /// 8.0 线的每一个补丁版本都必须落在闸门外：闸门看主版本，8.0.0 与 8.0.32 行为必须一致。
    /// </summary>
    [Theory]
    [InlineData("8.0.0.0")]
    [InlineData("8.0.32.0")]
    [InlineData("8.1.0.0")]
    public void AspNetCoreOpenApi_Should_NotGenerate_ForAnyEightVersion(string assemblyVersion)
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", assemblyVersion));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreOpenApi.g.cs");
    }

    [Fact]
    public void AspNetCoreOpenApi_Should_GenerateSchemaTransformer()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.OpenApi", "9.0.0.0"));

        generated["StronglyTypedIds.AspNetCoreOpenApi.g.cs"]
            .Should().Contain("class StronglyTypedIdOpenApiSchemaTransformer : global::Microsoft.AspNetCore.OpenApi.IOpenApiSchemaTransformer")
            .And.Contain("public static void ApplyTo(global::Microsoft.AspNetCore.OpenApi.OpenApiOptions options)")
            .And.Contain("[typeof(global::Len.StronglyTypedId.OrderId)] = (\"string\", \"uuid\"),");
    }

    #endregion

    #region ASP.NET Core MVC：Microsoft.AspNetCore.Mvc 主版本 >= 2

    [Fact]
    public void AspNetCoreMvc_Should_Generate_WhenMajorVersionIsTwo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "2.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.AspNetCoreMvc.g.cs");
    }

    [Fact]
    public void AspNetCoreMvc_Should_NotGenerate_WhenMajorVersionIsOne()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "1.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.AspNetCoreMvc.g.cs");
    }

    [Fact]
    public void AspNetCoreMvc_Should_GenerateProviderAndApplyTo()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Microsoft.AspNetCore.Mvc", "2.1.0.0"));

        var code = generated["StronglyTypedIds.AspNetCoreMvc.g.cs"];

        code.Should().Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Mvc.MvcOptions options)")
            .And.Contain("options.ModelBinderProviders.Insert(0, new StronglyTypedIdModelBinderProvider());")
            .And.Contain("public static void ApplyTo(global::Microsoft.AspNetCore.Routing.RouteOptions options)")
            .And.Contain("options.ConstraintMap[\"OrderId\"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<global::Len.StronglyTypedId.OrderId>);")
            .And.Contain("private sealed class StronglyTypedIdModelBinderProvider")
            .And.Contain("private sealed class StronglyTypedIdModelBinder<TId>")
            .And.Contain("private sealed class StronglyTypedIdRouteConstraint<TId>");
    }

    #endregion

    #region System.Text.Json / Newtonsoft.Json 全局注册入口

    /// <summary>
    /// STJ 侧此前只有逐 Id 的 <c>[JsonConverter]</c>，没有与 EF / Swagger / Dapper / MVC / 内置 OpenAPI 对称的
    /// <c>ApplyTo</c>。本用例锁住聚合入口的存在与内容：注册的必须是各 Id 内嵌的转换器实例。
    /// </summary>
    [Fact]
    public void SystemTextJsonRegistration_Should_GenerateApplyTo()
    {
        var generated = RunGenerator(IdSource);

        generated.Should().ContainKey("StronglyTypedIds.SystemTextJson.g.cs");

        generated["StronglyTypedIds.SystemTextJson.g.cs"]
            .Should().Contain("internal static partial class StronglyTypedIds")
            .And.Contain("public static void ApplyTo(global::System.Text.Json.JsonSerializerOptions options)")
            .And.Contain("options.Converters.Add(new global::Len.StronglyTypedId.OrderId.SystemTextJsonConverter());");
    }

    /// <summary>
    /// Newtonsoft 侧的入口沿用 <c>sample1</c> 里曾被注释掉的 <c>AddStronglyTypedId()</c> 形态： <c>JsonSerializerSettings</c>
    /// 上的扩展方法。
    /// </summary>
    [Fact]
    public void NewtonsoftJsonRegistration_Should_GenerateExtensionMethod()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"));

        generated.Should().ContainKey("StronglyTypedIds.NewtonsoftJson.g.cs");

        generated["StronglyTypedIds.NewtonsoftJson.g.cs"]
            .Should().Contain("internal static partial class StronglyTypedIds")
            .And.Contain("public static void AddStronglyTypedId(this global::Newtonsoft.Json.JsonSerializerSettings settings)")
            .And.Contain("settings.Converters.Add(new global::Len.StronglyTypedId.OrderId.NewtonsoftJsonConverter());");
    }

    /// <summary>
    /// 引用程序集里的 Id 若没有内嵌 <c>SystemTextJsonConverter</c>（声明它的程序集没引用 System.Text.Json），注册入口必须跳过它。否则生成
    /// <c>new X.SystemTextJsonConverter()</c> 会 CS0426 ——而这条编译错误出现在<b>消费者</b>的编译里，库作者完全看不到。
    /// </summary>
    [Fact]
    public void SystemTextJsonRegistration_Should_Skip_WhenReferencedIdHasNoConverter()
    {
        var generated = RunGenerator(
            IdSource,
            CreateReferencedAssembly("AlreadyGenerated", ReferencedIdWithoutConvertersSource));

        var code = generated["StronglyTypedIds.SystemTextJson.g.cs"];

        code.Should().Contain("new global::Len.StronglyTypedId.OrderId.SystemTextJsonConverter()")
            .And.NotContain("LegacyOrderId");
    }

    /// <summary>
    /// Newtonsoft 侧的同一判据：领域库不引 Newtonsoft、宿主应用引，是完全正常的分层。
    /// </summary>
    [Fact]
    public void NewtonsoftJsonRegistration_Should_Skip_WhenReferencedIdHasNoConverter()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "13.0.0.0"),
            CreateReferencedAssembly("AlreadyGenerated", ReferencedIdWithoutConvertersSource));

        var code = generated["StronglyTypedIds.NewtonsoftJson.g.cs"];

        code.Should().Contain("new global::Len.StronglyTypedId.OrderId.NewtonsoftJsonConverter()")
            .And.NotContain("LegacyOrderId");
    }

    /// <summary>
    /// 注册入口与核心生成器共用同一程序集门控：Newtonsoft 12.x 既不产出转换器，也不产出注册入口。
    /// </summary>
    [Fact]
    public void NewtonsoftJsonRegistration_Should_NotGenerate_WhenMajorVersionIsTwelve()
    {
        var generated = RunGenerator(
            IdSource,
            SyntheticAssembly.GetOrCreate("Newtonsoft.Json", "12.0.0.0"));

        generated.Keys.Should().NotContain("StronglyTypedIds.NewtonsoftJson.g.cs");
    }

    /// <summary>
    /// 一个 Id 都没有时不产出空壳注册类：否则会平白多出一个没有任何成员的 <c>StronglyTypedIds</c>。
    /// </summary>
    [Fact]
    public void Registrations_Should_NotGenerate_WhenNoIds()
    {
        var generated = RunGenerator(NoIdSource);

        generated.Keys.Should().NotContain("StronglyTypedIds.SystemTextJson.g.cs");
        generated.Keys.Should().NotContain("StronglyTypedIds.NewtonsoftJson.g.cs");
    }

    #endregion

    #region 辅助

    private const string IdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 引用类型 Id（<c>partial record</c>）：用于验证 Dapper 处理器对引用类型分支的产物形态。
    /// </summary>
    private const string ClassIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record OrderClassId(Guid Value);
        """;

    /// <summary>
    /// 混合基元源码：覆盖 Dapper <c>Parse</c> 的每一条转换分支 —— 枚举（底层整数 / 成员名文本）、
    /// <c>char</c>（string）、<c>decimal</c>（double）、<c>Guid</c>（string）、<c>string</c>。
    /// </summary>
    private const string MixedPrimitiveIdSource = """
        using System;

        namespace Len.StronglyTypedId;

        public enum OrderStatus
        {
            Unknown,
            Active,
        }

        [StronglyTypedId]
        public partial record struct StatusId(OrderStatus Value);

        [StronglyTypedId]
        public partial record struct LetterId(char Value);

        [StronglyTypedId]
        public partial record struct AmountId(decimal Value);

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);

        [StronglyTypedId]
        public partial record struct Sku(string Value);
        """;

    /// <summary>
    /// 一个强类型 Id 都没有的源码：用来验证「无 Id 时不产出空壳注册类」。
    /// </summary>
    private const string NoIdSource = """
        namespace Len.StronglyTypedId;

        public class NotAnId
        {
        }
        """;

    /// <summary>
    /// 一个「已由生成器补齐接口实现、但没有内嵌任何 JSON 转换器」的强类型 Id，等价于「声明它的程序集既没引用 System.Text.Json 也没引用 Newtonsoft.Json」的产物。
    /// </summary>
    private const string ReferencedIdWithoutConvertersSource = """
        namespace Already.Generated;

        public readonly record struct LegacyOrderId(global::System.Guid Value)
            : global::Len.StronglyTypedId.IStronglyTypedId<LegacyOrderId, global::System.Guid>
        {
            public static LegacyOrderId Create(global::System.Guid value) => new(value);

            public static LegacyOrderId Parse(string value, global::System.IFormatProvider? provider)
                => new(global::System.Guid.Parse(value, provider));

            public static bool TryParse(
                string? value,
                global::System.IFormatProvider? provider,
                out LegacyOrderId result)
            {
                if (global::System.Guid.TryParse(value, provider, out var primitiveId))
                {
                    result = new LegacyOrderId(primitiveId);
                    return true;
                }

                result = default;
                return false;
            }
        }
        """;

    /// <summary>
    /// EF Core 闸门只看引用的 <c>Microsoft.EntityFrameworkCore</c> 主版本，与源码里是否重写
    /// <c>ConfigureConventions</c> 无关，因此示例只需一个 Id。
    /// </summary>
    private const string EfCoreSource = """
        using System;

        namespace Len.StronglyTypedId;

        [StronglyTypedId]
        public partial record struct OrderId(Guid Value);
        """;

    /// <summary>
    /// 测试宿主 AppDomain 里必然加载了这些包的真实版本（且都是满足闸门的版本）。它们必须排除在默认引用之外，只由用例通过 <see cref="SyntheticAssembly"/>
    /// 按需注入指定版本，否则同名程序集出现两份引用，闸门判定的将是真实版本而非用例指定的版本。
    /// </summary>
    private static readonly string[] _versionGatedAssemblies =
    [
        "Len.StronglyTypedId.TestBase",
        "Microsoft.EntityFrameworkCore",
        "Swashbuckle.AspNetCore.SwaggerGen",
        "Microsoft.OpenApi",
        "Newtonsoft.Json",
        "Microsoft.AspNetCore.Mvc",
        "Microsoft.AspNetCore.Mvc.Core",
        "Microsoft.AspNetCore.Mvc.Abstractions",
        "Microsoft.AspNetCore.OpenApi",
        "Microsoft.AspNetCore.Routing",
    ];

    private static Dictionary<string, string> RunGenerator(string sourceCode, params MetadataReference[] extraReferences)
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

    private static CSharpCompilation CreateCompilation(string sourceCode, params MetadataReference[] extraReferences)
        => CreateCompilation("Len.StronglyTypedId.ModuleGate.Test", sourceCode, extraReferences);

    /// <summary>
    /// 把一个源码片段编译成「被引用程序集」的引用（<see cref="CompilationReference"/>）。
    /// </summary>
    private static CompilationReference CreateReferencedAssembly(string assemblyName, string sourceCode)
        => (CompilationReference)CreateCompilation(assemblyName, sourceCode, []).ToMetadataReference();

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        string sourceCode,
        MetadataReference[] extraReferences)
    {
        // AppDomain.GetAssemblies() 只返回「已加载」的程序集，而 System.Text.Json 是延迟加载的：
        // 必须像下面这样显式引用一次，它才会出现在编译引用里，SystemTextJsonCodeGenerator 也才会被选入。
        var seed = new[]
        {
            typeof(StronglyTypedIdAttribute).Assembly,
            typeof(System.Text.Json.JsonSerializer).Assembly,
        };

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Union(seed)
            .Distinct()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
            .Where(assembly => !_versionGatedAssemblies.Contains(assembly.GetName().Name, StringComparer.Ordinal))
            .Select(AssemblyReferenceCache.FromAssembly)
            .Concat(extraReferences)
            .ToArray();

        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(sourceCode)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    #endregion
}
