namespace Len.StronglyTypedId.Generators;

// 同 StronglyTypedIdAnalyzer：合并后引用 Workspaces 触发 RS1038，局部豁免。
#pragma warning disable RS1038
[Generator]
internal class StronglyTypedIdGenerator : IIncrementalGenerator
{
    private static readonly string _version = GeneratedCode.Version;

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

        // 枚举全部引用程序集的类型是发现阶段的主要开销，这里一次算好，交给各集成生成器取用。
        var discoveredIds = stronglyTypedIdInfos
            .Collect()
            .Combine(modules)
            .Select(static (source, _) => DiscoveredIds.Create(source.Left, source.Right));

        var idsAndModules = stronglyTypedIdInfos.Combine(modules);
        var discoveredIdsAndModules = discoveredIds.Combine(modules);

        // 各集成生成器是否启用，只由是否引用了对应程序集决定（见 _generatorCatalog）。
        context.RegisterSourceOutput(idsAndModules, GenerateCoreCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateEfCoreCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateSwaggerCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateDapperCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateAspNetCoreOpenApiCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateAspNetCoreMvcCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateSystemTextJsonRegistrationCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateNewtonsoftJsonRegistrationCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateMessagePackCode);
        context.RegisterSourceOutput(discoveredIdsAndModules, GenerateMongoDbCode);
    }

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

        // 带主构造函数时参数名与可空性可提前排除；不带主构造函数的段必须放行，attribute 可能标在
        // 不承载主构造函数的 partial 段上，一律要求 (Value) 形状会让这种写法既不产出代码也不报诊断。
        if (record.ParameterList is { } parameterList
            && parameterList.Parameters is not [{ Type: not NullableTypeSyntax, Identifier.ValueText: "Value" }])
        {
            return false;
        }

        // file 修饰符也要挡：容器循环只查了外层类型，record 自身是 file 局部类型时生成器放行就会产出
        // 跨文件的冲突 partial 声明（CS0535）。分析器对同一写法报 STIA0025，保证「报错的那条」就是「不生成的那条」。
        if (!modifiers.Any(SyntaxKind.PartialKeyword)
            || modifiers.Any(SyntaxKind.AbstractKeyword)
            || modifiers.Any(SyntaxKind.FileKeyword))
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
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "Microsoft.EntityFrameworkCore.dll");

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

    private static void GenerateMessagePackCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "MessagePack.dll");

    private static void GenerateMongoDbCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args)
        => GenerateIntegrationCode(context, args, "MongoDB.Bson.dll");

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
    /// 代码生成器的模块闸门登记：把「引用了哪个程序集才启用哪个生成器」集中在一处表达（Factory + Strategy），
    /// 取代原先散落在各处按程序集名做重复判定的写法。
    /// </summary>
    private static readonly ImmutableArray<CodeGeneratorRegistration> _generatorCatalog = ImmutableArray.Create(
        // 核心生成器按模块名精确匹配（区分大小写）。
        new CodeGeneratorRegistration(
            "Len.StronglyTypedId.dll",
            static modules => modules.Any(static module => module.Name == "Len.StronglyTypedId.dll"),
            StronglyTypedIdCodeGenerator.Instance,
            new DocText("Always", "始终"),
            new DocText("Core implementation", "核心实现"),
            new DocText("`<Namespace>.<TypeName>.g.cs`", "`<命名空间>.<类型名>.g.cs`")),
        new CodeGeneratorRegistration(
            "System.Text.Json.dll",
            static modules => modules.Any(static module => module.Name == "System.Text.Json.dll"),
            SystemTextJsonCodeGenerator.Instance,
            new DocText("`System.Text.Json` is referenced", "引用了 `System.Text.Json`"),
            new DocText(
                "Nested `SystemTextJsonConverter` + `[JsonConverter]`, plus `StronglyTypedIds.ApplyTo(JsonSerializerOptions)`",
                "嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性，以及 `StronglyTypedIds.ApplyTo(JsonSerializerOptions)`"),
            new DocText(
                "`…SystemTextJson.g.cs`, `StronglyTypedIds.SystemTextJson.g.cs`",
                "`…SystemTextJson.g.cs`, `StronglyTypedIds.SystemTextJson.g.cs`")),
        new CodeGeneratorRegistration(
            "Newtonsoft.Json.dll",
            static modules => modules.Any(static module => module.Name == "Newtonsoft.Json.dll" && module.Version.Major >= 13),
            NewtonsoftJsonCodeGenerator.Instance,
            new DocText("`Newtonsoft.Json` ≥ 13.0.0 is referenced", "引用了 `Newtonsoft.Json` ≥ 13.0.0"),
            new DocText(
                "Nested `NewtonsoftJsonConverter` + `[JsonConverter]`, plus `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)`",
                "嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性，以及 `StronglyTypedIds.AddStronglyTypedId(this JsonSerializerSettings)`"),
            new DocText(
                "`…NewtonsoftJson.g.cs`, `StronglyTypedIds.NewtonsoftJson.g.cs`",
                "`…NewtonsoftJson.g.cs`, `StronglyTypedIds.NewtonsoftJson.g.cs`")),
        // 集成生成器走 HasModule（OrdinalIgnoreCase，模块名取自元数据读取器，大小写未必规范）。
        new CodeGeneratorRegistration(
            "Microsoft.EntityFrameworkCore.dll",
            static modules => modules.HasModule("Microsoft.EntityFrameworkCore.dll", 7),
            EfCoreCodeGenerator.Instance,
            new DocText(
                "`Microsoft.EntityFrameworkCore` ≥ 7.0.0 is referenced",
                "引用了 `Microsoft.EntityFrameworkCore` ≥ 7.0.0"),
            new DocText(
                "Nested `{TypeName}Converter`, plus `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` and `StronglyTypedIds.ApplyTo(ModelBuilder)`",
                "嵌套 `{TypeName}Converter`，以及 `StronglyTypedIds.ApplyTo(ModelConfigurationBuilder)` 与 `StronglyTypedIds.ApplyTo(ModelBuilder)`"),
            new DocText(
                "`…EntityFrameworkCore.g.cs`, `StronglyTypedIds.EntityFrameworkCore.g.cs`",
                "`…EntityFrameworkCore.g.cs`, `StronglyTypedIds.EntityFrameworkCore.g.cs`")),
        new CodeGeneratorRegistration(
            "Swashbuckle.AspNetCore.SwaggerGen.dll",
            static modules => modules.HasModule("Swashbuckle.AspNetCore.SwaggerGen.dll", 6),
            SwaggerCodeGenerator.Instance,
            new DocText("`Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced", "引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0"),
            new DocText(
                "`StronglyTypedIds.ApplyTo(SwaggerGenOptions)`",
                "`StronglyTypedIds.ApplyTo(SwaggerGenOptions)`"),
            new DocText("`StronglyTypedIds.Swagger.g.cs`", "`StronglyTypedIds.Swagger.g.cs`")),
        new CodeGeneratorRegistration(
            "Dapper.dll",
            static modules => modules.HasModule("Dapper.dll", 2),
            DapperCodeGenerator.Instance,
            new DocText("`Dapper` ≥ 2.0.0 is referenced", "引用了 `Dapper` ≥ 2.0.0"),
            new DocText(
                "Nested `{TypeName}TypeHandler`, plus `StronglyTypedIds.ApplyTo()`",
                "嵌套 `{TypeName}TypeHandler`，以及 `StronglyTypedIds.ApplyTo()`"),
            new DocText(
                "`…Dapper.g.cs`, `StronglyTypedIds.Dapper.g.cs`",
                "`…Dapper.g.cs`, `StronglyTypedIds.Dapper.g.cs`")),
        new CodeGeneratorRegistration(
            "MessagePack.dll",
            static modules => modules.HasModule("MessagePack.dll", 2),
            MessagePackCodeGenerator.Instance,
            new DocText("`MessagePack` ≥ 2.0.0 is referenced", "引用了 `MessagePack` ≥ 2.0.0"),
            new DocText(
                "`{TypeName}MessagePackFormatter`, plus `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)`",
                "`{TypeName}MessagePackFormatter`，以及 `StronglyTypedIds.ApplyTo(MessagePackSerializerOptions)`"),
            new DocText(
                "`…MessagePack.g.cs`, `StronglyTypedIds.MessagePack.g.cs`",
                "`…MessagePack.g.cs`, `StronglyTypedIds.MessagePack.g.cs`")),
        new CodeGeneratorRegistration(
            "MongoDB.Bson.dll",
            static modules => modules.HasModule("MongoDB.Bson.dll", 3),
            MongoDbCodeGenerator.Instance,
            new DocText("`MongoDB.Bson` ≥ 3.0.0 is referenced", "引用了 `MongoDB.Bson` ≥ 3.0.0"),
            new DocText(
                "`{TypeName}BsonSerializer`, plus `StronglyTypedIds.RegisterBsonSerializers()`",
                "`{TypeName}BsonSerializer`，以及 `StronglyTypedIds.RegisterBsonSerializers()`"),
            new DocText(
                "`…MongoDb.g.cs`, `StronglyTypedIds.MongoDb.g.cs`",
                "`…MongoDb.g.cs`, `StronglyTypedIds.MongoDb.g.cs`")),
        new CodeGeneratorRegistration(
            "Microsoft.AspNetCore.OpenApi.dll",
            static modules => modules.HasModule("Microsoft.AspNetCore.OpenApi.dll", 9),
            AspNetCoreOpenApiCodeGenerator.Instance,
            new DocText("`Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced", "引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0"),
            new DocText(
                "`StronglyTypedIds.ApplyTo(OpenApiOptions)`",
                "`StronglyTypedIds.ApplyTo(OpenApiOptions)`"),
            new DocText(
                "`StronglyTypedIds.AspNetCoreOpenApi.g.cs`",
                "`StronglyTypedIds.AspNetCoreOpenApi.g.cs`")),
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
                "`StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)` (also powers Minimal API route constraints)",
                "`StronglyTypedIds.ApplyTo(MvcOptions)` + `ApplyTo(RouteOptions)`（同时支撑 Minimal API 的路由约束）"),
            new DocText(
                "`StronglyTypedIds.AspNetCoreMvc.g.cs`",
                "`StronglyTypedIds.AspNetCoreMvc.g.cs`")),
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
            DocText documentation,
            DocText? fileNames = null)
        {
            AssemblyName = assemblyName;
            IsEnabled = isEnabled;
            CoreGenerator = generator;
            IsCore = true;
            Condition = condition;
            Documentation = documentation;
            FileNames = fileNames ?? DocText.Empty;
        }

        public CodeGeneratorRegistration(
            string assemblyName,
            Func<ImmutableArray<ModuleInfo>, bool> isEnabled,
            IIntegrationCodeGenerator generator,
            DocText condition,
            DocText documentation,
            DocText? fileNames = null)
        {
            AssemblyName = assemblyName;
            IsEnabled = isEnabled;
            IntegrationGenerator = generator;
            IsCore = false;
            Condition = condition;
            Documentation = documentation;
            FileNames = fileNames ?? DocText.Empty;
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

        /// <summary>
        /// docs 的「Generated files」表「Generated files」列的文案：该集成产出的文件名。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="Documentation"/> 分开存放是因为读者不同：README 首屏只要「生成什么 + 入口签名」，文件名属于调试细节，合在一列会让表格宽到必须横向滚动。
        /// </remarks>
        public DocText FileNames { get; }
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
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return null;
        }

        // 不能用 Constructors[0]：没有主构造函数的 record 会把合成的拷贝构造排在首位，于是参数类型
        // 绑到 record 自身，IsSupported 返回 false，生成器静默不产出任何代码。改为遍历实例构造、
        // 跳过参数类型等于自身的那个，与分析器 GetSingleParameterType 同一套判据。
        var ctorArgType = GetSingleParameterType(symbol);

        if (ctorArgType is null)
        {
            return null;
        }

        var assembly = context.SemanticModel.Compilation.Assembly;
        var defaultValidator = StronglyTypedIdInfo.GetAssemblyDefaultValidator(assembly);
        var defaultNormalizer = StronglyTypedIdInfo.GetAssemblyDefaultNormalizer(assembly);
        var defaultExceptionType = StronglyTypedIdInfo.GetAssemblyDefaultExceptionType(assembly);
        var defaultIntegrations = StronglyTypedIdInfo.GetAssemblyDefaultIntegrations(assembly);
        var defaultRedact = StronglyTypedIdInfo.GetAssemblyDefaultRedactValidationMessage(assembly);

        return SupportedPrimitiveTypes.IsSupported(ctorArgType)
            ? new StronglyTypedIdInfo(symbol, defaultValidator, defaultNormalizer, defaultExceptionType, defaultIntegrations, defaultRedact)
            : null;
    }

    /// <summary>
    /// 取强类型 Id 的基元类型，即唯一一个「单参数、且参数类型不是自身」的实例构造函数所带的参数类型。
    /// </summary>
    /// <remarks>
    /// 跳过参数类型等于自身的构造，是为了滤掉 record 合成的拷贝构造，也顺带覆盖了用户手写的单参数构造
    /// 排在主构造之前的情况。可空引用类型返回 <see langword="null"/>，由分析器的 <c>ParameterCannotBeNullable</c> 报错。
    /// </remarks>
    private static ITypeSymbol? GetSingleParameterType(INamedTypeSymbol type)
    {
        foreach (var constructor in type.InstanceConstructors)
        {
            if (constructor.Parameters is [var parameter]
                && parameter.NullableAnnotation != NullableAnnotation.Annotated
                && !SymbolEqualityComparer.Default.Equals(parameter.Type, type))
            {
                return parameter.Type;
            }
        }

        return null;
    }
}
