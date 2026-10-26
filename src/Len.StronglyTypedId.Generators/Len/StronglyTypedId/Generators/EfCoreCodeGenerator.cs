using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Entity Framework Core 的 <c>ValueConverter</c> 与统一注册入口。
/// </summary>
internal class EfCoreCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// <c>ApplyTo</c> 方法体内语句的缩进（产物中为 8 空格）。
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
        // 分组键用 Name 而非 EscapedName：这里比的是「是否重名」，名字不落进生成代码；
        // 且两者是 1:1 映射（仅保留关键字差一个 @ 前缀），分组结果本来完全相同。
        var ambiguousNames = stronglyTypedIds
            .GroupBy(idInfo => idInfo.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var registrations = new StringBuilder();
        var modelBuilderRegistrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var converterName = GetConverterName(idInfo, ambiguousNames.Contains(idInfo.Name));

            var converterCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                    class {{converterName}} : global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<{{idInfo.FullyQualifiedName}}, {{idInfo.PrimitiveIdTypeName}}>
                    {
                        /// <summary>
                        /// 默认构造函数。
                        /// </summary>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public {{converterName}}() : base(v => v.Value, val => {{idInfo.FullyQualifiedName}}.Create(val))
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

            context.AddSource($"{idInfo.HintName}.EntityFrameworkCore.g.cs", GeneratedCode.NormalizeLineEndings(converterCode));

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
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder)
                {{{registrations}}
                }

                /// <summary>
                /// 通过扫描模型在 Entity Framework Core 中启用强类型 Id。
                /// 上下文未重写 <c>ConfigureConventions</c> 时在 <c>OnModelCreating</c> 末尾调用，只扫描已加入模型的实体类型。
                /// </summary>
                /// <param name="modelBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelBuilder"/></param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
                {{{modelBuilderRegistrations}}
                }
            }
            """;

        context.AddSource("StronglyTypedIds.EntityFrameworkCore.g.cs", GeneratedCode.NormalizeLineEndings(registrationCode));
    }

    /// <summary>
    /// 取转换器的类名。
    /// </summary>
    private static string GetConverterName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}Converter"
            : $"{idInfo.EscapedName}Converter";
}
