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
        context.RegisterSourceOutput(idsAndModules, GenerateAspNetCoreMvcCode);
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
        // 这里挡掉的写法（顶层无命名空间 / 容器非 partial / 容器是 file 本地类型）分析器都会
        // 另行报错（STIAO004 / STIAO009），先挡掉是为了不再为已知非法的写法生成一份编译不过的代码。
        // 泛型容器已放开：重开的 partial 只需复现类型参数表、约束子句可省略，故不再在此拦截。
        var container = record.Parent;

        while (container is TypeDeclarationSyntax declaration)
        {
            if (declaration.Modifiers.Any(SyntaxKind.FileKeyword)
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
        var generators = GetCoreGenerators(args.Modules);

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

        GetIntegrationGenerator(modules, "Microsoft.EntityFrameworkCore.dll")?.Generate(infos, modules, context, _version);
    }

    private static void GenerateSwaggerCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetIntegrationGenerator(args.Modules, "Swashbuckle.AspNetCore.SwaggerGen.dll")?.Generate(args.Infos, args.Modules, context, _version);
    }

    private static void GenerateDapperCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetIntegrationGenerator(args.Modules, "Dapper.dll")?.Generate(args.Infos, args.Modules, context, _version);
    }

    private static void GenerateAspNetCoreOpenApiCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetIntegrationGenerator(args.Modules, "Microsoft.AspNetCore.OpenApi.dll")?.Generate(args.Infos, args.Modules, context, _version);
    }

    private static void GenerateAspNetCoreMvcCode(
        SourceProductionContext context,
        (ImmutableArray<StronglyTypedIdInfo> Infos, ImmutableArray<ModuleInfo> Modules) args)
    {
        GetIntegrationGenerator(args.Modules, "Microsoft.AspNetCore.Mvc.dll")?.Generate(args.Infos, args.Modules, context, _version);
    }

    /// <summary>
    /// 代码生成器的模块闸门登记：把「引用了哪个程序集才启用哪个生成器」集中在一处表达
    /// （Factory + Strategy），取代原先散落在 GetCodeGenerators / GetEfCoreCodeGenerator 等处的重复判定。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每条登记携带一个 <see cref="CodeGeneratorRegistration.IsEnabled"/> 谓词，逐字复刻原有的门控语义
    /// （含核心生成器的大小写敏感差异），以保证重构前后生成产物字节级一致。
    /// </para>
    /// <para>
    /// <see cref="CodeGeneratorRegistration.IsCore"/> 区分两类生成器：核心生成器（运行时实现 /
    /// System.Text.Json / Newtonsoft.Json）随引用自动批量启用；集成生成器（EF Core / Swagger / Dapper /
    /// 内置 OpenAPI / MVC）各自按引用程序集独立门控。
    /// </para>
    /// </remarks>
    private static readonly ImmutableArray<CodeGeneratorRegistration> GeneratorCatalog = ImmutableArray.Create(
        // 核心生成器：原 switch 按模块名精确匹配（区分大小写），此处用同样的 Any(m => m.Name == …) 复刻。
        new CodeGeneratorRegistration(
            "Len.StronglyTypedId.dll",
            static modules => modules.Any(static module => module.Name == "Len.StronglyTypedId.dll"),
            StronglyTypedIdCodeGenerator.Instance,
            isCore: true),
        new CodeGeneratorRegistration(
            "System.Text.Json.dll",
            static modules => modules.Any(static module => module.Name == "System.Text.Json.dll"),
            SystemTextJsonCodeGenerator.Instance,
            isCore: true),
        new CodeGeneratorRegistration(
            "Newtonsoft.Json.dll",
            static modules => modules.Any(static module => module.Name == "Newtonsoft.Json.dll" && module.Version.Major >= 13),
            NewtonsoftJsonCodeGenerator.Instance,
            isCore: true),
        // 集成生成器：HasModule 本身按 OrdinalIgnoreCase 比较（模块名取自元数据读取器，大小写未必规范）。
        new CodeGeneratorRegistration(
            "Microsoft.EntityFrameworkCore.dll",
            static modules => modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7),
            EfCoreCodeGenerator.Instance),
        new CodeGeneratorRegistration(
            "Swashbuckle.AspNetCore.SwaggerGen.dll",
            static modules => modules.HasModule("Swashbuckle.AspNetCore.SwaggerGen.dll", 6),
            SwaggerCodeGenerator.Instance),
        new CodeGeneratorRegistration(
            "Dapper.dll",
            static modules => modules.HasModule("Dapper.dll", 2),
            DapperCodeGenerator.Instance),
        new CodeGeneratorRegistration(
            "Microsoft.AspNetCore.OpenApi.dll",
            static modules => modules.HasModule("Microsoft.AspNetCore.OpenApi.dll", 9),
            AspNetCoreOpenApiCodeGenerator.Instance),
        // MVC 可用性需覆盖「门面 / 实现」拆分与 .NET 版本差异：
        //   · Microsoft.AspNetCore.Mvc.dll —— 框架引用引入的门面程序集（仅做类型转发），
        //     真实消费者的编译引用里必有，是「MVC 是否可用」的规范判据。
        //   · Microsoft.AspNetCore.Mvc.Core.dll / Microsoft.AspNetCore.Mvc.Abstractions.dll ——
        //     实际定义 IModelBinder / MvcOptions 的实现程序集，随 .NET 版本不同而由其一承载
        //     （如 .NET 8 落在 Abstractions、.NET 10 落在 Core）。测试画像里只加载了承载
        //     类型定义的那个，门面常因类型转发而未直接进入编译引用。
        // 三者任一出现即代表 MVC 可用，保证真实消费者与测试画像判定一致。
        new CodeGeneratorRegistration(
            "Microsoft.AspNetCore.Mvc.dll",
            static modules => modules.HasModule("Microsoft.AspNetCore.Mvc.dll", 2)
                || modules.HasModule("Microsoft.AspNetCore.Mvc.Core.dll", 2)
                || modules.HasModule("Microsoft.AspNetCore.Mvc.Abstractions.dll", 2),
            AspNetCoreMvcCodeGenerator.Instance));

    /// <summary>
    /// 单个代码生成器的模块闸门登记项（Factory 注册表的一项）。
    /// </summary>
    private sealed class CodeGeneratorRegistration
    {
        public CodeGeneratorRegistration(
            string assemblyName,
            Func<ImmutableArray<ModuleInfo>, bool> isEnabled,
            ICodeGenerator generator,
            bool isCore = false)
        {
            AssemblyName = assemblyName;
            IsEnabled = isEnabled;
            Generator = generator;
            IsCore = isCore;
        }

        public string AssemblyName { get; }

        public Func<ImmutableArray<ModuleInfo>, bool> IsEnabled { get; }

        public ICodeGenerator Generator { get; }

        public bool IsCore { get; }
    }

    /// <summary>
    /// 取当前编译引用下所有已启用的核心生成器，按各自 <see cref="ICodeGenerator.Order"/> 升序排列后批量执行。
    /// </summary>
    private static ImmutableArray<ICodeGenerator> GetCoreGenerators(ImmutableArray<ModuleInfo> modules)
        => [.. GeneratorCatalog
            .Where(registration => registration.IsCore && registration.IsEnabled(modules))
            .OrderBy(static registration => registration.Generator.Order)
            .Select(static registration => registration.Generator)];

    /// <summary>
    /// 按登记名取当前编译引用下已启用的集成生成器；未启用时返回 <see langword="null"/>。
    /// </summary>
    private static ICodeGenerator? GetIntegrationGenerator(ImmutableArray<ModuleInfo> modules, string assemblyName)
    {
        foreach (var registration in GeneratorCatalog)
        {
            if (registration.AssemblyName == assemblyName && registration.IsEnabled(modules))
            {
                return registration.Generator;
            }
        }

        return null;
    }

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
