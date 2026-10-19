namespace Len.StronglyTypedId.Generators;

// 同 StronglyTypedIdAnalyzer：程序集合并后引用 Microsoft.CodeAnalysis.Workspaces 触发 RS1038，局部豁免。
#pragma warning disable RS1038
[Generator]
internal class StronglyTypedIdGenerator : IIncrementalGenerator
{
    private static readonly string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 核心生成器按「单个 Id」注册输出：任一 Id 改动只重跑它自己那一次输出，
        // 而不是把全部 Id 的产物重算一遍（.Collect() 会让整批一起失效）。
        var stronglyTypedIdInfos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Len.StronglyTypedId.StronglyTypedIdAttribute",
                CouldBeStronglyTypedId,
                GetStronglyTypedIdInfoOrNull)
           .Where(static info => info is not null)
           .Select(static (info, _) => info!.Value);

        // 模块枚举拆成两级，把 Compilation 依赖下沉到确实需要它的那一步：
        //   第一级只依赖引用列表（编辑期间不变），元数据解码不会在每次按键后重跑；
        //   第二级才 Combine(CompilationProvider)，只做「引用 → 程序集符号」的查表。
        // 两级都按「名 + 版本」比较，故下游（发现、各集成生成器）仍保持稳定。
        var moduleIdentities = context.MetadataReferencesProvider
            .SelectMany(static (references, _) => CollectModuleIdentities(references))
            .WithComparer(new ModuleIdentity.Comparer())
            .Collect();

        var modules = moduleIdentities
            .Combine(context.CompilationProvider)
            .SelectMany(static (source, _) => ResolveModuleAssemblies(source.Left, source.Right))
            .WithComparer(new ModuleInfo.Comparer())
            .Collect();

        // 只需知道「是否存在」DbContext 约定配置，故把匹配结果收敛为单个布尔值，
        // 避免把无意义的 bool 数组一路带到输出阶段。
        var hasDbContextConventions = context.SyntaxProvider
            .CreateSyntaxProvider(CouldBeEfCoreDbContext, static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => !matches.IsDefaultOrEmpty);

        // 枚举全部引用程序集的所有类型是发现阶段的主要开销，此前 5 个集成生成器各调一次、
        // 一次生成要枚举 5 遍。这里一次算好，交给各集成生成器取用。
        var discoveredIds = stronglyTypedIdInfos
            .Collect()
            .Combine(modules)
            .Select(static (source, _) => DiscoveredIds.Create(source.Left, source.Right));

        var idsAndModules = stronglyTypedIdInfos.Combine(modules);
        var discoveredIdsAndModules = discoveredIds.Combine(modules);

        // Swagger 代码是否生成，仅由是否引用了 Swashbuckle.AspNetCore.SwaggerGen.dll 决定，
        // 不再依赖源码中是否出现 AddSwaggerGen（参见 SwaggerCodeGenerator）。
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
        // 另行报错（STIA0004 / STIA0009），先挡掉是为了不再为已知非法的写法生成一份编译不过的代码。
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

    /// <summary>
    /// 取单个引用所代表的全部模块身份（不解析程序集符号，故不依赖 <see cref="Compilation"/>）。
    /// </summary>
    /// <remarks>
    /// 单独抽成方法而非内联 lambda：<c>SelectMany</c> 对 <c>IncrementalValueProvider&lt;ImmutableArray&lt;TSource&gt;&gt;</c>
    /// 有专门的展平重载，其选择器逐元素调用、必须返回 <see cref="ImmutableArray{T}"/>。显式返回类型可让推断稳定。
    /// </remarks>
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

        // 集成生成器由各自的 RegisterSourceOutput 单独注册，这里只跑核心生成器。
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
    /// <remarks>
    /// <para>
    /// 生成器内部抛异常时，Roslyn 会丢弃该生成器本次的<b>全部</b>产出，使用者只看到一条 CS8785 警告
    /// （无位置、无原因、无堆栈），表现为「生成的代码凭空消失」。这里就地兜住异常并报出一条带原因的诊断，
    /// 把黑盒故障变成可定位的错误。
    /// </para>
    /// <para>
    /// 取消不是故障：取消请求必须原样上抛，否则生成器会被当成「已完成」而留下不完整的产出。
    /// </para>
    /// <para>
    /// 拆成 <see cref="Action"/> + <see cref="Action{T}"/> 两个委托而不是直接吃 <see cref="SourceProductionContext"/>，
    /// 是为了让失败路径可以在单元测试里被真实触发（<see cref="SourceProductionContext"/> 无法在测试中构造）。
    /// </para>
    /// </remarks>
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
    private static readonly ImmutableArray<CodeGeneratorRegistration> _generatorCatalog = ImmutableArray.Create(
        // 核心生成器：原 switch 按模块名精确匹配（区分大小写），此处用同样的 Any(m => m.Name == …) 复刻。
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
        // 集成生成器：HasModule 本身按 OrdinalIgnoreCase 比较（模块名取自元数据读取器，大小写未必规范）。
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
            AspNetCoreMvcCodeGenerator.Instance,
            new DocText(
                "`Microsoft.AspNetCore.Mvc` ≥ 2.0.0 is referenced (or its `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` facade)",
                "引用了 `Microsoft.AspNetCore.Mvc` ≥ 2.0.0（或其 `Microsoft.AspNetCore.Mvc.Core` / `.Abstractions` 门面）"),
            new DocText(
                "`StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` → `StronglyTypedIds.AspNetCoreMvc.g.cs` (also powers Minimal API route constraints)",
                "`StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` → `StronglyTypedIds.AspNetCoreMvc.g.cs`（同时支撑 Minimal API 的路由约束）")),
        // STJ / Newtonsoft 的全局注册入口与各自的核心生成器共用同一程序集门控（同一份引用决定两者是否启用），
        // 但分属两类：核心生成器逐 Id 产出转换器，注册生成器产出一次性的 StronglyTypedIds 聚合入口。
        // 取集成生成器时按 IsCore 过滤，故同名登记不会互相遮蔽。
        // 这两条不单独成行（DocText.Empty）：它们的产物已经写进对应核心生成器的 Documentation 里，
        // 因为「引用同一个程序集」同时决定了两者是否启用，拆成两行只会让表格看起来像有两套触发条件。
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
    /// <remarks>
    /// 由 <c>tools/Len.StronglyTypedId.DocGen</c> 消费。之所以不另立一张「文档专用」的表，
    /// 是因为那样一来「新增集成生成器却忘了写文档」就无人察觉；从门控登记里直接取，
    /// 漏写 <see cref="CodeGeneratorRegistration.Documentation"/> 的那一项会当场从表格里消失。
    /// </remarks>
    internal static ImmutableArray<CodeGeneratorRegistration> DocumentedGenerators
        => [.. _generatorCatalog.Where(static registration => !registration.Documentation.IsEmpty)];

    /// <summary>
    /// 单个代码生成器的模块闸门登记项（Factory 注册表的一项）。
    /// </summary>
    /// <remarks>
    /// <see cref="Condition"/> 与 <see cref="Documentation"/> 是给 README 用的：集成触发条件表由
    /// <c>tools/Len.StronglyTypedId.DocGen</c> 从这里直接生成，不再手写维护。两者都留空的登记
    /// （STJ / Newtonsoft 的全局注册入口）不单独成行 —— 它们的产物已经写进对应核心生成器的
    /// <see cref="Documentation"/> 里，因为「引用同一个程序集」同时决定了两者是否启用。
    /// </remarks>
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

        /// <summary>README 集成表「Condition in the compilation」列的文案。</summary>
        public DocText Condition { get; }

        /// <summary>README 集成表「Generated code」列的文案；空文案表示不单独成行。</summary>
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
    /// <remarks>
    /// 必须跳过核心生成器：STJ / Newtonsoft 的注册生成器与各自的核心生成器登记在同一个程序集名下，
    /// 若不过滤 <see cref="CodeGeneratorRegistration.IsCore"/>，循环会先命中核心项并因其
    /// <see cref="CodeGeneratorRegistration.IntegrationGenerator"/> 为 <see langword="null"/> 而返回空。
    /// </remarks>
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
