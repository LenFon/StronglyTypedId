namespace Len.StronglyTypedId.Generators;

// 同 StronglyTypedIdAnalyzer：合并后引用 Workspaces 触发 RS1038，局部豁免。
#pragma warning disable RS1038
[Generator]
internal class StronglyTypedIdGenerator : IIncrementalGenerator
{
    private static readonly string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 按「单个 Id」注册输出：任一 Id 改动只重跑它自己，.Collect() 会让整批一起失效。
        var stronglyTypedIdInfos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Len.StronglyTypedId.StronglyTypedIdAttribute",
                CouldBeStronglyTypedId,
                GetStronglyTypedIdInfoOrNull)
           .Where(static info => info is not null)
           .Select(static (info, _) => info!.Value);

        // 拆成两级，把 Compilation 依赖下沉：第一级只依赖引用列表（编辑期间不变），
        // 第二级才 Combine(CompilationProvider) 做「引用 → 程序集符号」的查表。
        var moduleIdentities = context.MetadataReferencesProvider
            .SelectMany(static (references, _) => CollectModuleIdentities(references))
            .WithComparer(new ModuleIdentity.Comparer())
            .Collect();

        var modules = moduleIdentities
            .Combine(context.CompilationProvider)
            .SelectMany(static (source, _) => ResolveModuleAssemblies(source.Left, source.Right))
            .WithComparer(new ModuleInfo.Comparer())
            .Collect();

        // 只需知道「是否存在」，故把匹配结果收敛为单个布尔值。
        var hasDbContextConventions = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeEfCoreDbContext, static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => !matches.IsDefaultOrEmpty);

        // 枚举全部引用程序集的类型是发现阶段的主要开销，这里一次算好，交给各集成生成器取用。
        var discoveredIds = stronglyTypedIdInfos
            .Collect()
            .Combine(modules)
            .Select(static (source, _) => DiscoveredIds.Create(source.Left, source.Right));

        var idsAndModules = stronglyTypedIdInfos.Combine(modules);
        var discoveredIdsAndModules = discoveredIds.Combine(modules);

        // 各集成生成器是否启用，只由是否引用了对应程序集决定（见 _generatorCatalog）。
        context.RegisterSourceOutput(idsAndModules, GenerateCoreCode);
        context.RegisterSourceOutput(discoveredIdsAndModules.Combine(hasDbContextConventions), GenerateEfCoreCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateSwaggerCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateDapperCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateAspNetCoreOpenApiCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateAspNetCoreMvcCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateSystemTextJsonRegistrationCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateNewtonsoftJsonRegistrationCode);
    }

    private static bool CouldBeEfCoreDbContext(SyntaxNode syntaxNode, CancellationToken _)
        => syntaxNode is MethodDeclarationSyntax
            {
                Identifier.ValueText: "ConfigureConventions",
                Modifiers: var modifiers and not [],
                Parent: ClassDeclarationSyntax,
                ParameterList.Parameters.Count: 1,
            }
            && modifiers.Any(SyntaxKind.OverrideKeyword)
            && modifiers.Any(SyntaxKind.ProtectedKeyword);

    private static bool CouldBeStronglyTypedId(SyntaxNode syntaxNode, CancellationToken _)
    {
        if (syntaxNode is not RecordDeclarationSyntax
            {
                TypeParameterList: null,
                Modifiers: var modifiers and not [],
            } record)
        {
            return false;
        }

        // 带主构造函数时参数名与可空性可提前排除；不带主构造函数的段必须放行 —— attribute 可能标在
        // 不承载主构造函数的 partial 段上，一律要求 (Value) 形状会让这种写法既不产出代码也不报诊断。
        if (record.ParameterList is { } parameterList
            && parameterList.Parameters is not [{ Type: not NullableTypeSyntax, Identifier.ValueText: "Value" }])
        {
            return false;
        }

        if (!modifiers.Any(SyntaxKind.PartialKeyword) || modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return false;
        }

        // 逐层向上检查容器，最后要求整条链落在命名空间里。这些写法分析器会另行报错（STIA0004 / STIA0009），
        // 先挡掉是为了不为已知非法的写法生成编译不过的代码。
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

    /// <summary>
    /// 取单个引用所代表的全部模块身份（不解析程序集符号，故不依赖 <see cref="Compilation"/>）。
    /// </summary>
    private static ImmutableArray<ModuleIdentity> CollectModuleIdentities(MetadataReference reference)
        => [.. reference.GetModuleIdentities()];

    /// <summary>
    /// 把模块身份集合解析成完整的 <see cref="ModuleInfo"/> 集合（这一步才需要 <see cref="Compilation"/>）。
    /// </summary>
    private static ImmutableArray<ModuleInfo> ResolveModuleAssemblies(ImmutableArray<ModuleIdentity> identities, Compilation compilation)
        => [.. identities.Select(identity => identity.ResolveAssembly(compilation))];

    private static void GenerateCoreCode(
        SourceProductionContext context,
        (StronglyTypedIdInfo Info, ImmutableArray<ModuleInfo> Modules) args)
    {
        var generators = GetCoreGenerators(args.Modules);

        if (generators.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (var generator in generators)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            RunGuarded(generator, args.Info, args.Modules, context);
        }
    }

    private static void GenerateEfCoreCode(
        SourceProductionContext context,
        ((DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) Integration, bool HasDbContextConventions) args)
    {
        if (!args.HasDbContextConventions)
        {
            return;
        }

        GenerateIntegrationCode(context, args.Integration, "Microsoft.EntityFrameworkCore.dll");
    }

    private static void GenerateSwaggerCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "Swashbuckle.AspNetCore.SwaggerGen.dll");

    private static void GenerateDapperCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "Dapper.dll");

    private static void GenerateAspNetCoreOpenApiCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "Microsoft.AspNetCore.OpenApi.dll");

    private static void GenerateAspNetCoreMvcCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "Microsoft.AspNetCore.Mvc.dll");

    private static void GenerateSystemTextJsonRegistrationCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "System.Text.Json.dll");

    private static void GenerateNewtonsoftJsonRegistrationCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "Newtonsoft.Json.dll");

    /// <summary>
    /// 按登记名取当前编译引用下已启用的集成生成器并执行；未启用时什么也不做。
    /// </summary>
    private static void GenerateIntegrationCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args,
        string assemblyName)
    {
        var generator = GetIntegrationGenerator(args.Modules, assemblyName);

        if (generator is not null)
        {
            RunGuarded(generator, args.DiscoveredIds, args.Modules, context);
        }
    }

    /// <summary>
    /// 执行单个生成器，并把生成阶段的任何异常转成 <see cref="Descriptors.GeneratorFailure"/> 诊断。
    /// </summary>
    internal static void RunGuarded(
        string generatorName,
        Action generate,
        Action<Diagnostic> reportDiagnostic,
        CancellationToken cancellationToken)
    {
        try
        {
            generate();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            reportDiagnostic(Diagnostic.Create(
                Descriptors.GeneratorFailure,
                Location.None,
                generatorName,
                exception.GetType().FullName,
                exception.Message));
        }
    }

    private static void RunGuarded(
        ICoreCodeGenerator generator,
        StronglyTypedIdInfo info,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context)
        => RunGuarded(
            generator.GetType().Name,
            () => generator.Generate(info, modules, context, _version),
            context.ReportDiagnostic,
            context.CancellationToken);

    private static void RunGuarded(
        IIntegrationCodeGenerator generator,
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context)
        => RunGuarded(
            generator.GetType().Name,
            () => generator.Generate(discoveredIds, modules, context, _version),
            context.ReportDiagnostic,
            context.CancellationToken);

    /// <summary>
    /// 代码生成器的模块闸门登记：把「引用了哪个程序集才启用哪个生成器」集中在一处表达（Factory + Strategy），取代原先散落在 GetCodeGenerators /
    /// GetEfCoreCodeGenerator 等处的重复判定。
    /// </summary>
    private static readonly ImmutableArray<CodeGeneratorRegistration> _generatorCatalog = ImmutableArray.Create(
        // 核心生成器按模块名精确匹配（区分大小写）。
        new CodeGeneratorRegistration(
            "Len.StronglyTypedId.dll",
            static modules => modules.Any(static module => module.Name == "Len.StronglyTypedId.dll"),
            StronglyTypedIdCodeGenerator.Instance,
            new DocText("Always", "始终"),
            new DocText(
                "Core implementation → `<Namespace>.<TypeName>.g.cs`",
                "核心实现 → `<命名空间>.<类型名>.g.cs`")),
        new CodeGeneratorRegistration(
            "System.Text.Json.dll",
            static modules => modules.Any(static module => module.Name == "System.Text.Json.dll"),
            SystemTextJsonCodeGenerator.Instance,
            new DocText("`System.Text.Json` is referenced", "引用了 `System.Text.Json`"),
            new DocText(
                "Nested `SystemTextJsonConverter` + `[JsonConverter]` → `…SystemTextJson.g.cs`, plus `StronglyTypedIds.ApplyTo(JsonSerializerOptions)` → `StronglyTypedIds.SystemTextJson.g.cs`",
                "嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性 → `…SystemTextJson.g.cs`，以及 `StronglyTypedIds.ApplyTo(JsonSerializerOptions)` → `StronglyTypedIds.SystemTextJson.g.cs`")),
        new CodeGeneratorRegistration(
            "Newtonsoft.Json.dll",
            static modules => modules.Any(static module => module.Name == "Newtonsoft.Json.dll" && module.Version.Major >= 13),
            NewtonsoftJsonCodeGenerator.Instance,
            new DocText("`Newtonsoft.Json` ≥ 13.0.0 is referenced", "引用了 `Newtonsoft.Json` ≥ 13.0.0"),
            new DocText(
                "Nested `NewtonsoftJsonConverter` + `[JsonConverter]` → `…NewtonsoftJson.g.cs`, plus `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)` → `StronglyTypedIds.NewtonsoftJson.g.cs`",
                "嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性 → `…NewtonsoftJson.g.cs`，以及 `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)` → `StronglyTypedIds.NewtonsoftJson.g.cs`")),
        // 集成生成器走 HasModule（OrdinalIgnoreCase，模块名取自元数据读取器，大小写未必规范）。
        new CodeGeneratorRegistration(
            "Microsoft.EntityFrameworkCore.dll",
            static modules => modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7),
            EfCoreCodeGenerator.Instance,
            new DocText(
                "`Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced **and** a `DbContext` overrides `ConfigureConventions`",
                "引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0 **且**存在重写了 `ConfigureConventions` 的 `DbContext`"),
            new DocText(
                "Nested `{TypeName}Converter` → `…EntityFrameworkCore.g.cs`, plus `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` and `StronglyTypedIds.ApplyTo(ModelBuilder)` → `StronglyTypedIds.EntityFrameworkCore.g.cs`",
                "嵌套 `{TypeName}Converter` → `…EntityFrameworkCore.g.cs`，以及 `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` 与 `StronglyTypedIds.ApplyTo(ModelBuilder)` → `StronglyTypedIds.EntityFrameworkCore.g.cs`")),
        new CodeGeneratorRegistration(
            "Swashbuckle.AspNetCore.SwaggerGen.dll",
            static modules => modules.HasModule("Swashbuckle.AspNetCore.SwaggerGen.dll", 6),
            SwaggerCodeGenerator.Instance,
            new DocText("`Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced", "引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0"),
            new DocText(
                "`StronglyTypedIds.ApplyTo(SwaggerGenOptions)` → `StronglyTypedIds.Swagger.g.cs`",
                "`StronglyTypedIds.ApplyTo(SwaggerGenOptions)` → `StronglyTypedIds.Swagger.g.cs`")),
        new CodeGeneratorRegistration(
            "Dapper.dll",
            static modules => modules.HasModule("Dapper.dll", 2),
            DapperCodeGenerator.Instance,
            new DocText("`Dapper` ≥ 2.0.0 is referenced", "引用了 `Dapper` ≥ 2.0.0"),
            new DocText(
                "Nested `{TypeName}TypeHandler` → `…Dapper.g.cs`, plus `StronglyTypedIds.ApplyTo(IDbConnection)` → `StronglyTypedIds.Dapper.g.cs`",
                "嵌套 `{TypeName}TypeHandler` → `…Dapper.g.cs`，以及 `StronglyTypedIds.ApplyTo(IDbConnection)` → `StronglyTypedIds.Dapper.g.cs`")),
        new CodeGeneratorRegistration(
            "Microsoft.AspNetCore.OpenApi.dll",
            static modules => modules.HasModule("Microsoft.AspNetCore.OpenApi.dll", 9),
            AspNetCoreOpenApiCodeGenerator.Instance,
            new DocText("`Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced", "引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0"),
            new DocText(
                "`StronglyTypedIds.ApplyTo(OpenApiOptions)` → `StronglyTypedIds.AspNetCoreOpenApi.g.cs`",
                "`StronglyTypedIds.ApplyTo(OpenApiOptions)` → `StronglyTypedIds.AspNetCoreOpenApi.g.cs`")),
        // MVC 的「门面 / 实现」拆分随 .NET 版本变化（.NET 8 落在 Abstractions、.NET 10 落在 Core），
        // 三者任一出现即视为 MVC 可用，保证真实消费者与测试画像判定一致。
        new CodeGeneratorRegistration(
            "Microsoft.AspNetCore.Mvc.dll",
            static modules => modules.HasModule("Microsoft.AspNetCore.Mvc.dll", 2)
                || modules.HasModule("Microsoft.AspNetCore.Mvc.Core.dll", 2)
                || modules.HasModule("Microsoft.AspNetCore.Mvc.Abstractions.dll", 2),
            AspNetCoreMvcCodeGenerator.Instance,
            new DocText(
                "`Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (or its `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` facade)",
                "引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` 门面）"),
            new DocText(
                "`StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` → `StronglyTypedIds.AspNetCoreMvc.g.cs` (also powers Minimal API route constraints)",
                "`StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` → `StronglyTypedIds.AspNetCoreMvc.g.cs`（同时支撑 Minimal API 的路由约束）")),
        // 注册入口与核心生成器共用同一程序集门控，但分属两类；取集成生成器时按 IsCore 过滤，不会互相遮蔽。
        // 这两条不单独成行（DocText.Empty）：产物已写进对应核心生成器的 Documentation。
        new CodeGeneratorRegistration(
            "System.Text.Json.dll",
            static modules => modules.Any(static module => module.Name == "System.Text.Json.dll"),
            SystemTextJsonRegistrationCodeGenerator.Instance,
            DocText.Empty,
            DocText.Empty),
        new CodeGeneratorRegistration(
            "Newtonsoft.Json.dll",
            static modules => modules.Any(static module => module.Name == "Newtonsoft.Json.dll" && module.Version.Major >= 13),
            NewtonsoftJsonRegistrationCodeGenerator.Instance,
            DocText.Empty,
            DocText.Empty));

    /// <summary>
    /// README「What gets generated」表的行数据：只含登记了文档的项，顺序即表格行序。
    /// </summary>
    internal static ImmutableArray<CodeGeneratorRegistration> DocumentedGenerators
        => [.. _generatorCatalog.Where(static registration => !registration.Documentation.IsEmpty)];

    /// <summary>
    /// 单个代码生成器的模块闸门登记项（Factory 注册表的一项）。
    /// </summary>
    internal sealed class CodeGeneratorRegistration
    {
        public CodeGeneratorRegistration(
            string assemblyName,
            Func<ImmutableArray<ModuleInfo>, bool> isEnabled,
            ICoreCodeGenerator generator,
            DocText condition,
            DocText documentation)
        {
            AssemblyName = assemblyName;
            IsEnabled = isEnabled;
            CoreGenerator = generator;
            IsCore = true;
            Condition = condition;
            Documentation = documentation;
        }

        public CodeGeneratorRegistration(
            string assemblyName,
            Func<ImmutableArray<ModuleInfo>, bool> isEnabled,
            IIntegrationCodeGenerator generator,
            DocText condition,
            DocText documentation)
        {
            AssemblyName = assemblyName;
            IsEnabled = isEnabled;
            IntegrationGenerator = generator;
            IsCore = false;
            Condition = condition;
            Documentation = documentation;
        }

        public string AssemblyName { get; }

        public Func<ImmutableArray<ModuleInfo>, bool> IsEnabled { get; }

        public ICoreCodeGenerator? CoreGenerator { get; }

        public IIntegrationCodeGenerator? IntegrationGenerator { get; }

        public bool IsCore { get; }

        /// <summary>
        /// README 集成表「Condition in the compilation」列的文案。
        /// </summary>
        public DocText Condition { get; }

        /// <summary>
        /// README 集成表「Generated code」列的文案；空文案表示不单独成行。
        /// </summary>
        public DocText Documentation { get; }
    }

    /// <summary>
    /// 取当前编译引用下所有已启用的核心生成器，按各自 <see cref="ICoreCodeGenerator.Order"/> 升序排列后批量执行。
    /// </summary>
    private static ImmutableArray<ICoreCodeGenerator> GetCoreGenerators(ImmutableArray<ModuleInfo> modules)
        => [.. _generatorCatalog
            .Where(registration => registration.IsCore && registration.IsEnabled(modules))
            .OrderBy(static registration => registration.CoreGenerator!.Order)
            .Select(static registration => registration.CoreGenerator!)];

    /// <summary>
    /// 按登记名取当前编译引用下已启用的集成生成器；未启用时返回 <see langword="null"/>。
    /// </summary>
    private static IIntegrationCodeGenerator? GetIntegrationGenerator(ImmutableArray<ModuleInfo> modules, string assemblyName)
    {
        foreach (var registration in _generatorCatalog)
        {
            if (registration.AssemblyName == assemblyName && !registration.IsCore && registration.IsEnabled(modules))
            {
                return registration.IntegrationGenerator;
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
        var defaultNormalizer = GetAssemblyDefaultNormalizer(context.SemanticModel.Compilation.Assembly);
        var defaultExceptionType = GetAssemblyDefaultExceptionType(context.SemanticModel.Compilation.Assembly);
        var defaultIntegrations = StronglyTypedIdInfo.GetAssemblyDefaultIntegrations(context.SemanticModel.Compilation.Assembly);

        return SupportedPrimitiveTypes.IsSupported(ctorArgType)
            ? new StronglyTypedIdInfo(symbol, defaultValidator, defaultNormalizer, defaultExceptionType, defaultIntegrations)
            : null;
    }

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Validator = …)]</c> 指定的默认验证器方法名。
    /// </summary>
    private static string? GetAssemblyDefaultValidator(IAssemblySymbol assembly)
        => AttributeReader.GetString(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            StronglyTypedIdInfo.ValidatorArgumentName);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(Normalizer = …)]</c> 指定的默认归一化方法名。
    /// </summary>
    private static string? GetAssemblyDefaultNormalizer(IAssemblySymbol assembly)
        => AttributeReader.GetString(
            AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
            StronglyTypedIdInfo.NormalizerArgumentName);

    /// <summary>
    /// 读取 <c>[assembly: StronglyTypedIdDefaults(ExceptionType = typeof(…))]</c> 指定的默认异常类型显示名。
    /// </summary>
    private static string? GetAssemblyDefaultExceptionType(IAssemblySymbol assembly)
        => AttributeReader.GetType(
                AttributeReader.Find(assembly, AttributeReader.StronglyTypedIdDefaultsAttributeName),
                StronglyTypedIdInfo.ExceptionTypeArgumentName)
            ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
