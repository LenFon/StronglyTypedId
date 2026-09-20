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
        context.RegisterSourceOutput(idsAndModules, GenerateDapperCode);
        context.RegisterSourceOutput(idsAndModules, GenerateAspNetCoreOpenApiCode);
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
                TypeParameterList: null, // 非泛型
                Modifiers: var modifiers and not [],
            } record)
        {
            return false;
        }

        // 带主构造函数时，参数名与可空性在语法层即可判定，提前排除。
        // 不带主构造函数的那一段必须放行：attribute 可能标在不承载主构造函数的段上（例如主构造函数
        // 写在另一个 partial 段里），而本谓词只会对「带 attribute 的那一段」被调用。若在此一律要求
        // (Value) 形状，这种分段写法既不会产出任何代码、也不会报任何诊断——分析器是按「类型」判定的。
        if (record.ParameterList is { } parameterList
            && parameterList.Parameters is not [{ Type: not NullableTypeSyntax, Identifier.ValueText: "Value" }])
        {
            return false;
        }

        if (!modifiers.Any(SyntaxKind.PartialKeyword) || modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return false;
        }

        // 逐层向上检查容器，最后要求整条链落在命名空间里。
        // 嵌套类型是被支持的，但生成代码要新增的是嵌套类型自身的成员，而 partial 的各段必须处于同一
        // 容器内 —— 生成代码只能把声明逐层嵌回原来的包含类型，因此每一层都必须能被原样重开。
        // 这里挡掉的写法（顶层无命名空间 / 容器非 partial / 容器泛型 / 容器是 file 本地类型）分析器都会
        // 另行报错（STIAO004 / STIAO009 / STIAO003），先挡掉是为了不再为已知非法的写法生成一份编译不过的代码。
        var container = record.Parent;

        while (container is TypeDeclarationSyntax declaration)
        {
            if (declaration.TypeParameterList is not null
                || declaration.Modifiers.Any(SyntaxKind.FileKeyword)
                || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                return false;
            }

            container = declaration.Parent;
        }

        return container is BaseNamespaceDeclarationSyntax;
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

    private static void GenerateDapperCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetDapperCodeGenerator(args.Modules)?.Generate(args.Infos, args.Modules, context, _version);
    }

    private static void GenerateAspNetCoreOpenApiCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetAspNetCoreOpenApiCodeGenerator(args.Modules)?.Generate(args.Infos, args.Modules, context, _version);
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

    private static ICodeGenerator? GetDapperCodeGenerator(ImmutableArray<ModuleInfo> modules) =>
        modules.HasModule("Dapper.dll", 2) ? DapperCodeGenerator.Instance : null;

    private static ICodeGenerator? GetAspNetCoreOpenApiCodeGenerator(ImmutableArray<ModuleInfo> modules) =>
        modules.HasModule("Microsoft.AspNetCore.OpenApi.dll", 9) ? AspNetCoreOpenApiCodeGenerator.Instance : null;

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

        var defaultValidator = GetAssemblyDefaultValidator(context.SemanticModel.Compilation.Assembly);

        return SupportedPrimitiveTypes.IsSupported(ctorArgType) ? new StronglyTypedIdInfo(symbol, defaultValidator) : null;
    }

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Validator = …)]</c> 指定的默认验证器方法名。
    /// </summary>
    /// <remarks>
    /// 生成器项目不引用运行时程序集，故只能以字符串匹配 <c>Len.StronglyTypedId.StronglyTypedIdDefaultsAttribute</c>。
    /// 未声明时返回 <see langword="null"/>，此时逐类型 <c>Validator</c> 仍是唯一来源。
    /// </remarks>
    private static string? GetAssemblyDefaultValidator(IAssemblySymbol assembly)
        => assembly.GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdDefaultsAttribute")
            ?.NamedArguments
            .FirstOrDefault(argument => argument.Key == "Validator")
            .Value.Value as string;
}
