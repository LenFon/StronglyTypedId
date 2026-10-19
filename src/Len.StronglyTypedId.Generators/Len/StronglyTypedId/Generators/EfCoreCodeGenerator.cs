using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Entity Framework Core 的 <c>ValueConverter</c> 与统一注册入口。
/// </summary>
internal class EfCoreCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new EfCoreCodeGenerator();

    public int Order { get; } = 3;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.External.ToList();

        // 同名 Id 会生成进同一个 StronglyTypedIds 类并撞 CS0102；仅在确实重名时才用「命名空间_类型名」消歧，
        // 以免改动无冲突场景下既有的转换器类名。
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
                        /// <summary>Default constructor.</summary>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public {{converterName}}() : base(v => v.Value, val => {{idInfo.FullyQualifiedName}}.Create(val))
                        {
                        }

                        /// <summary>Apply the {{idInfo.Name}} converter to the Entity Framework Core via conventions. Call this from <c>ConfigureConventions(ModelConfigurationBuilder)</c>.</summary>
                        /// <param name="configurationBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"/></param>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder) =>
                            configurationBuilder.Properties<{{idInfo.FullyQualifiedName}}>().HaveConversion(typeof({{converterName}}));

                        /// <summary>Apply the {{idInfo.Name}} converter to the Entity Framework Core by scanning the model. Call this from <c>OnModelCreating(ModelBuilder)</c> when conventions are not available (e.g. when the context does not override <c>ConfigureConventions</c>).</summary>
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

            registrations.Append($"\n\t\t{converterName}.ApplyTo(configurationBuilder);");
            modelBuilderRegistrations.Append($"\n\t\t{converterName}.ApplyTo(modelBuilder);");
        }

        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>Employ strongly typed IDs in Entity Framework Core via conventions.</summary>
                /// <param name="configurationBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"/></param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder)
                {{{registrations}}
                }

                /// <summary>Employ strongly typed IDs in Entity Framework Core by scanning the model. Call this from <c>OnModelCreating(ModelBuilder)</c> when the context does not override <c>ConfigureConventions(ModelConfigurationBuilder)</c>.</summary>
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
    /// <param name="idInfo">强类型 Id 描述。</param>
    /// <param name="requiresNamespaceQualifier">
    /// 是否存在位于其它命名空间的同名强类型 Id，需要以命名空间前缀消歧。
    /// </param>
    private static string GetConverterName(StronglyTypedIdInfo idInfo, bool requiresNamespaceQualifier)
        => requiresNamespaceQualifier
            ? $"{idInfo.HintName.Replace('.', '_')}Converter"
            : $"{idInfo.Name}Converter";
}
