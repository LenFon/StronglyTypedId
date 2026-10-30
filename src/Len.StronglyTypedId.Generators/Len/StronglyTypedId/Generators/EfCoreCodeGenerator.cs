using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Entity Framework Core 的 <c>ValueConverter</c> 与统一注册入口。
/// </summary>
internal class EfCoreCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// <c>AddStronglyTypedIds</c> 方法体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 8);

    internal static readonly IIntegrationCodeGenerator Instance = new EfCoreCodeGenerator();

    public int Order { get; } = 3;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.EntityFrameworkCore).ToList();

        // 空注册入口没有任何作用：Integrations = None 时必须连这个文件也不产出。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        // 同名 Id 会生成进同一个 StronglyTypedIds 类并撞 CS0102；仅在确实重名时才用「命名空间_类型名」消歧，
        // 以免改动无冲突场景下既有的转换器类名。
        var ambiguousNames = IntegrationNaming.GetAmbiguousNames(stronglyTypedIds);

        var registrations = new StringBuilder();
        var modelBuilderRegistrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var converterName = IntegrationNaming.GetTypeName(idInfo, ambiguousNames, "Converter");

            // 自定义基元的 provider 类型必须是数据库认得的类型。直接用基元本身会让 EF 报
            // 「The '<Primitive>' property could not be mapped because the database provider does not support
            // this type」—— 数据库不认识用户自定义的 CLR 类型。故与 JSON / 二进制 / Dapper 同一口径，
            // 落库的是规范文本（string）。内置基元的产物逐字节不变。
            var providerType = idInfo.IsCustomPrimitive ? "string" : idInfo.PrimitiveIdTypeName;
            var toProvider = idInfo.IsCustomPrimitive
                ? idInfo.GetPrimitiveTextExpression("v.Value")
                : "v.Value";
            var fromProvider = idInfo.IsCustomPrimitive
                ? $"{idInfo.FullyQualifiedName}.Create({idInfo.PrimitiveIdTypeName}.Parse(val, global::System.Globalization.CultureInfo.InvariantCulture))"
                : $"{idInfo.FullyQualifiedName}.Create(val)";

            var converterCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                    class {{converterName}} : global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<{{idInfo.FullyQualifiedName}}, {{providerType}}>
                    {
                        /// <summary>
                        /// 默认构造函数。
                        /// </summary>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public {{converterName}}() : base(v => {{toProvider}}, val => {{fromProvider}})
                        {
                        }

                        /// <summary>
                        /// 通过约定把 {{idInfo.EscapedName}} 转换器应用到 Entity Framework Core。请在 <c>ConfigureConventions(ModelConfigurationBuilder)</c> 中调用。
                        /// </summary>
                        /// <param name="configurationBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"/></param>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder) =>
                            configurationBuilder.Properties<{{idInfo.FullyQualifiedName}}>().HaveConversion(typeof({{converterName}}));

                        /// <summary>
                        /// 通过扫描模型把 {{idInfo.EscapedName}} 转换器应用到 Entity Framework Core。
                        /// 约定不可用时在 <c>OnModelCreating(ModelBuilder)</c> 末尾调用，只扫描已加入模型的实体类型。
                        /// </summary>
                        /// <param name="modelBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelBuilder"/></param>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
                        {
                            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                            {
                                foreach (var property in entityType.GetProperties())
                                {
                                    if (property.ClrType == typeof({{idInfo.FullyQualifiedName}}))
                                    {
                                        property.SetValueConverter(new {{converterName}}());
                                    }
                                }
                            }
                        }
                    }
                }
                """;

            GeneratedCode.AddGeneratedSource(context, $"{idInfo.HintName}.EntityFrameworkCore.g.cs", converterCode);

            registrations.Append($"\n{_registrationIndent}{converterName}.ApplyTo(configurationBuilder);");
            modelBuilderRegistrations.Append($"\n{_registrationIndent}{converterName}.ApplyTo(modelBuilder);");
        }

        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// 通过约定在 Entity Framework Core 中启用强类型 Id。
                /// </summary>
                /// <param name="configurationBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"/></param>
                /// <returns>同一个 <paramref name="configurationBuilder"/>，便于链式调用。</returns>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder AddStronglyTypedIds(this global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder)
                {{{registrations}}
                    return configurationBuilder;
                }

                /// <summary>
                /// 通过扫描模型在 Entity Framework Core 中启用强类型 Id。
                /// 上下文未重写 <c>ConfigureConventions</c> 时在 <c>OnModelCreating</c> 末尾调用，只扫描已加入模型的实体类型。
                /// </summary>
                /// <param name="modelBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelBuilder"/></param>
                /// <returns>同一个 <paramref name="modelBuilder"/>，便于链式调用。</returns>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static global::Microsoft.EntityFrameworkCore.ModelBuilder AddStronglyTypedIds(this global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
                {{{modelBuilderRegistrations}}
                    return modelBuilder;
                }
            }
            """;

        GeneratedCode.AddGeneratedSource(context, "StronglyTypedIds.EntityFrameworkCore.g.cs", registrationCode);
    }
}
