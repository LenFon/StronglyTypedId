namespace Len.StronglyTypedId.Generators;

// 同 StronglyTypedIdAnalyzer：程序集合并后引用 Microsoft.CodeAnalysis.Workspaces 触发 RS1038，局部豁免。
#pragma warning disable RS1038
[Generator]
internal class StronglyTypedIdGenerator : IIncrementalGenerator
{
    private static readonly string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var stronglyTypedIdInfos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Len.StronglyTypedId.StronglyTypedIdAttribute",
                CouldBeStronglyTypedId,
                GetStronglyTypedIdInfoOrNull)
           .Where(static info => info is not null)
           .Select(static (info, _) => info!.Value)
           .Collect();

        var modules = context.MetadataReferencesProvider
            .Combine(context.CompilationProvider)
            .SelectMany(static (source, _) => source.Left.GetModules(source.Right))
            .WithComparer(new ModuleInfo.Comparer())
            .Collect();

        // 只需知道「是否存在」DbContext 约定配置，故把匹配结果收敛为单个布尔值，
        // 避免把无意义的 bool 数组一路带到输出阶段。
        var hasDbContextConventions = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeEfCoreDbContext, static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => !matches.IsDefaultOrEmpty);

        var idsAndModules = stronglyTypedIdInfos.Combine(modules);

        // Swagger 代码是否生成，仅由是否引用了 Swashbuckle.AspNetCore.SwaggerGen.dll 决定，
        // 不再依赖源码中是否出现 AddSwaggerGen（参见 SwaggerCodeGenerator）。
        context.RegisterSourceOutput(idsAndModules, GenerateCoreCode);
        context.RegisterSourceOutput(idsAndModules.Combine(hasDbContextConventions), GenerateEfCoreCode);
        context.RegisterSourceOutput(idsAndModules, GenerateSwaggerCode);
    }

    private static bool CouldBeEfCoreDbContext(SyntaxNode syntaxNode, CancellationToken _)
    {
        if (syntaxNode is not MethodDeclarationSyntax
            {
                Identifier.ValueText: "ConfigureConventions",
                Modifiers: var modifiers and not [],
                Parent: ClassDeclarationSyntax,
                ParameterList.Parameters.Count: 1,
            })
        {
            return false;
        }

        return modifiers.Any(SyntaxKind.OverrideKeyword) && modifiers.Any(SyntaxKind.ProtectedKeyword);
    }

    private static bool CouldBeStronglyTypedId(SyntaxNode syntaxNode, CancellationToken _)
    {
        if (syntaxNode is not RecordDeclarationSyntax
            {
                ParameterList.Parameters: [{ Type: not NullableTypeSyntax, Identifier.ValueText: "Value" }],
                TypeParameterList: null, // 非泛型
                Parent: BaseNamespaceDeclarationSyntax, // 非嵌套类型，并且有命名空间
                Modifiers: var modifiers and not [],
            })
        {
            return false;
        }

        return modifiers.Any(SyntaxKind.PartialKeyword) && !modifiers.Any(SyntaxKind.AbstractKeyword);
    }

    private static void GenerateCoreCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        var generators = GetCodeGenerators(args.Modules);

        if (generators.IsDefaultOrEmpty)
        {
            return;
        }

        // EfCore 与 Swagger 由各自的 RegisterSourceOutput 单独注册，这里只跑核心生成器。
        foreach (var generator in generators.OrderBy(generator => generator.Order))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            generator.Generate(args.Infos, args.Modules, context, _version);
        }
    }

    private static void GenerateEfCoreCode(
        SourceProductionContext context,
        ((ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) IdsAndModules, bool HasDbContextConventions) args)
    {
        if (!args.HasDbContextConventions)
        {
            return;
        }

        var (infos, modules) = args.IdsAndModules;

        GetEfCoreCodeGenerator(modules)?.Generate(infos, modules, context, _version);
    }

    private static void GenerateSwaggerCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetSwaggerCodeGenerator(args.Modules)?.Generate(args.Infos, args.Modules, context, _version);
    }

    private static ImmutableArray<ICodeGenerator> GetCodeGenerators(ImmutableArray<ModuleInfo> modules)
    {
        var codeGenerators = modules
            .Select(module => module switch
            {
                { Name: "System.Text.Json.dll" } => SystemTextJsonCodeGenerator.Instance,
                { Name: "Len.StronglyTypedId.dll" } => StronglyTypedIdCodeGenerator.Instance,
                { Name: "Newtonsoft.Json.dll", Version.Major: >= 13 } => NewtonsoftJsonCodeGenerator.Instance,
                _ => null,
            })
            .OfType<ICodeGenerator>();

        return [.. codeGenerators];
    }

    private static ICodeGenerator? GetEfCoreCodeGenerator(ImmutableArray<ModuleInfo> modules) =>
        modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7) ? EfCoreCodeGenerator.Instance : null;

    private static ICodeGenerator? GetSwaggerCodeGenerator(ImmutableArray<ModuleInfo> modules) =>
        modules.HasModule("Swashbuckle.AspNetCore.SwaggerGen.dll", 6) ? SwaggerCodeGenerator.Instance : null;

    private static StronglyTypedIdInfo? GetStronglyTypedIdInfoOrNull(GeneratorAttributeSyntaxContext context, CancellationToken _)
    {
        if (context is not
            {
                TargetSymbol: INamedTypeSymbol
                {
                    Constructors: [{ Parameters: [{ Type: var ctorArgType, NullableAnnotation: not NullableAnnotation.Annotated }] }, ..]
                } symbol
            })
        {
            return null;
        }

        return SupportedPrimitiveTypes.IsSupported(ctorArgType.Name) ? new StronglyTypedIdInfo(symbol) : null;
    }
}
