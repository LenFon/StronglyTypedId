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

        // 集成生成器逐个登记：程序集名与启用判据都取自同一张登记表，新增集成只需加一行登记，
        // 不必再同步维护一个硬编码程序集名的方法（两处漏一处就会静默不生成）。
        foreach (var registration in _generatorCatalog)
        {
            if (registration.IsCore)
            {
                continue;
            }

            context.RegisterSourceOutput(
                discoveredIdsAndModules,
                (context, args) => GenerateIntegrationCode(context, args, registration));
        }
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
        // 名为 var 的类型同样要挡：它会遮蔽 var 关键字，生成代码里的 `out var id` / `is { Value: var x }`
        // 全部失效（CS1503 / CS8508），而这不是靠全限定名能绕开的。分析器报 STIA0026。
        // 与生成成员同名的类型也要挡：成员名不能与封闭类型相同（CS0542）。分析器报 STIA0027。
        // 后者依赖基元类型与集成引用，只能在这里（已拿到符号与模块）判定。
        if (!modifiers.Any(SyntaxKind.PartialKeyword)
            || modifiers.Any(SyntaxKind.AbstractKeyword)
            || modifiers.Any(SyntaxKind.FileKeyword)
            || record.Identifier.ValueText == "var")
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

    /// <summary>
    /// 按登记项取当前编译引用下已启用的集成生成器并执行；未启用时什么也不做。
    /// </summary>
    private static void GenerateIntegrationCode(
        SourceProductionContext context,
        (DiscoveredIds DiscoveredIds, ImmutableArray<ModuleInfo> Modules) args,
        CodeGeneratorRegistration registration)
    {
        if (!registration.IsEnabled(args.Modules))
        {
            return;
        }

        RunGuarded(registration.IntegrationGenerator!, args.DiscoveredIds, args.Modules, context);
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
    private static readonly ImmutableArray<CodeGeneratorRegistration> _generatorCatalog =
    [
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
                "Nested `SystemTextJsonConverter` + `[JsonConverter]`, plus `AddStronglyTypedIds()` on `JsonSerializerOptions` and `WithStronglyTypedIds()` for source-generated contexts",
                "嵌套 `SystemTextJsonConverter` 与 `[JsonConverter]` 特性，以及 `JsonSerializerOptions` 上的 `AddStronglyTypedIds()` 与供源生成 context 使用的 `WithStronglyTypedIds()`"),
            new DocText(
                "`…SystemTextJson.g.cs`, `StronglyTypedIds.SystemTextJson.g.cs`",
                "`…SystemTextJson.g.cs`, `StronglyTypedIds.SystemTextJson.g.cs`")),
        new CodeGeneratorRegistration(
            "Newtonsoft.Json.dll",
            static modules => modules.Any(static module => module.Name == "Newtonsoft.Json.dll" && module.Version.Major >= 13),
            NewtonsoftJsonCodeGenerator.Instance,
            new DocText("`Newtonsoft.Json` ≥ 13.0.0 is referenced", "引用了 `Newtonsoft.Json` ≥ 13.0.0"),
            new DocText(
                "Nested `NewtonsoftJsonConverter` + `[JsonConverter]`, plus `AddStronglyTypedIds()` on `JsonSerializerSettings`",
                "嵌套 `NewtonsoftJsonConverter` 与 `[JsonConverter]` 特性，以及 `JsonSerializerSettings` 上的 `AddStronglyTypedIds()`"),
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
                "Nested `{TypeName}Converter`, plus `AddStronglyTypedIds()` on `ModelConfigurationBuilder` and `ModelBuilder`",
                "嵌套 `{TypeName}Converter`，以及 `ModelConfigurationBuilder` 与 `ModelBuilder` 上的 `AddStronglyTypedIds()`"),
            new DocText(
                "`…EntityFrameworkCore.g.cs`, `StronglyTypedIds.EntityFrameworkCore.g.cs`",
                "`…EntityFrameworkCore.g.cs`, `StronglyTypedIds.EntityFrameworkCore.g.cs`")),
        new CodeGeneratorRegistration(
            "Swashbuckle.AspNetCore.SwaggerGen.dll",
            static modules => modules.HasModule("Swashbuckle.AspNetCore.SwaggerGen.dll", 6),
            SwaggerCodeGenerator.Instance,
            new DocText("`Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0 is referenced", "引用了 `Swashbuckle.AspNetCore.SwaggerGen` ≥ 6.0.0"),
            new DocText(
                "`AddStronglyTypedIds()` on `SwaggerGenOptions`",
                "`SwaggerGenOptions` 上的 `AddStronglyTypedIds()`"),
            new DocText("`StronglyTypedIds.Swagger.g.cs`", "`StronglyTypedIds.Swagger.g.cs`")),
        new CodeGeneratorRegistration(
            "Dapper.dll",
            static modules => modules.HasModule("Dapper.dll", 2),
            DapperCodeGenerator.Instance,
            new DocText("`Dapper` ≥ 2.0.0 is referenced", "引用了 `Dapper` ≥ 2.0.0"),
            new DocText(
                "Nested `{TypeName}TypeHandler`, plus `AddStronglyTypedIds()` (also on `IDbConnection`)",
                "嵌套 `{TypeName}TypeHandler`，以及 `AddStronglyTypedIds()`（`IDbConnection` 上也有一个）"),
            new DocText(
                "`…Dapper.g.cs`, `StronglyTypedIds.Dapper.g.cs`",
                "`…Dapper.g.cs`, `StronglyTypedIds.Dapper.g.cs`")),
        new CodeGeneratorRegistration(
            "MessagePack.dll",
            static modules => modules.HasModule("MessagePack.dll", 2),
            MessagePackCodeGenerator.Instance,
            new DocText("`MessagePack` ≥ 2.0.0 is referenced", "引用了 `MessagePack` ≥ 2.0.0"),
            new DocText(
                "`{TypeName}MessagePackFormatter`, plus `AddStronglyTypedIds()` on `MessagePackSerializerOptions`",
                "`{TypeName}MessagePackFormatter`，以及 `MessagePackSerializerOptions` 上的 `AddStronglyTypedIds()`"),
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
            "HotChocolate.Types.dll",
            static modules => modules.HasModule("HotChocolate.Types.dll", 13),
            HotChocolateCodeGenerator.Instance,
            new DocText("`HotChocolate` ≥ 13.0.0 is referenced", "引用了 `HotChocolate` ≥ 13.0.0"),
            new DocText(
                "`{TypeName}HotChocolateType` scalar, plus `AddStronglyTypedIds()` on `IRequestExecutorBuilder`",
                "`{TypeName}HotChocolateType` scalar，以及 `IRequestExecutorBuilder` 上的 `AddStronglyTypedIds()`"),
            new DocText(
                "`…HotChocolate.g.cs`, `StronglyTypedIds.HotChocolate.g.cs`",
                "`…HotChocolate.g.cs`, `StronglyTypedIds.HotChocolate.g.cs`")),
        // protobuf-net 把特性放在 protobuf-net.Core、Serializer 放在 protobuf-net；两者由同一个包引入，
        // 引用后者必然带上前者，故只查 protobuf-net。核心生成器：surrogate 只能靠特性声明。
        new CodeGeneratorRegistration(
            "protobuf-net.dll",
            static modules => modules.HasModule("protobuf-net.dll", 3),
            ProtobufCodeGenerator.Instance,
            new DocText("`protobuf-net` ≥ 3.0.0 is referenced", "引用了 `protobuf-net` ≥ 3.0.0"),
            new DocText(
                "`[ProtoContract(Surrogate = …)]` on the id plus a nested `{TypeName}Surrogate` with conversion operators",
                "Id 上的 `[ProtoContract(Surrogate = …)]`，以及带转换运算符的嵌套 `{TypeName}Surrogate`"),
            new DocText("`…Protobuf.g.cs`", "`…Protobuf.g.cs`")),
        new CodeGeneratorRegistration(
            "Microsoft.AspNetCore.OpenApi.dll",
            static modules => modules.HasModule("Microsoft.AspNetCore.OpenApi.dll", 9),
            AspNetCoreOpenApiCodeGenerator.Instance,
            new DocText("`Microsoft.AspNetCore.OpenApi` ≥ 9.0.0 is referenced", "引用了 `Microsoft.AspNetCore.OpenApi` ≥ 9.0.0"),
            new DocText(
                "`AddStronglyTypedIds()` on `OpenApiOptions`",
                "`OpenApiOptions` 上的 `AddStronglyTypedIds()`"),
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
                "`AddStronglyTypedIds()` on `MvcOptions` and `RouteOptions` (also powers Minimal API route constraints)",
                "`MvcOptions` 与 `RouteOptions` 上的 `AddStronglyTypedIds()`（同时支撑 Minimal API 的路由约束）"),
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
            DocText.Empty)];

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

    private static StronglyTypedIdInfo? GetStronglyTypedIdInfoOrNull(GeneratorAttributeSyntaxContext context, CancellationToken _)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return null;
        }

        // 同一类型在两个 partial 段各标一次 [StronglyTypedId] 时，ForAttributeWithMetadataName 会
        // 对每个特性应用各触发一次（实测 TargetNode 的 span 不同、TargetSymbol 相同）。两次都产出
        // 就会用同一个 hintName 调两次 AddSource，抛 ArgumentException 并降级成 CS8785，
        // 该 Id 的全部产物随之丢失。编译器已用 CS0579 报出「特性重复」，生成器只需安静地生成一份。
        // 判据取「本次触发落在该符号的第一个特性应用上」：无状态，不破坏按 Id 的增量性。
        // 不能取「第一个声明」：特性可以标在不承载主构造函数的后续段上，那样会一个产物都不生成。
        if (!IsFirstAttributeApplication(context, symbol))
        {
            return null;
        }

        // 不能用 Constructors[0]：没有主构造函数的 record 会把合成的拷贝构造排在首位，于是参数类型
        // 绑到 record 自身，IsSupported 返回 false，生成器静默不产出任何代码。改为遍历实例构造、
        // 跳过参数类型等于自身的那个，与分析器 GetSingleParameterType 同一套判据。
        // 可空引用类型参数不生成任何代码，由分析器的 ParameterCannotBeNullable 报错。
        var ctorArgType = StronglyTypedIdInfo.GetSingleParameterType(symbol, skipNullableAnnotated: true);

        if (ctorArgType is null)
        {
            return null;
        }

        var assembly = context.SemanticModel.Compilation.Assembly;

        if (!SupportedPrimitiveTypes.IsSupported(ctorArgType))
        {
            return null;
        }

        // 生成代码通篇引用 `Value`（接口成员、ToString、CompareTo…），而 `Value` 只由主构造函数合成。
        // 手写单参构造（`public OrderId(int value) { }`）没有主构造，也就没有 `Value` 属性；
        // 特性标在不带主构造的 partial 段上时，`GetSingleParameterType` 仍能从另一段找到构造，
        // 于是生成器会产出引用不存在成员的代码 —— 用户只看到指向生成文件的 CS0535。
        // 分析器对这两种写法分别报 STIA0005 / STIA0007，这里同步不产出，让那条诊断成为唯一可见的错误。
        if (!HasValueProperty(symbol, ctorArgType))
        {
            return null;
        }

        var info = new StronglyTypedIdInfo(symbol, StronglyTypedIdDefaults.Read(assembly));

        // 类型名与生成成员同名会 CS0542。分析器报 STIA0027 并阻断构建，这里同步不产出，
        // 免得在 IDE 里先看到一堆指向生成文件的编译错误。判据与分析器同源。
        if (GeneratedMemberNames.MayCollide(symbol.Name)
            && GeneratedMemberNames.CollidesWithGeneratedMember(
                info,
                ReferencesAssembly(context.SemanticModel.Compilation, "System.Text.Json"),
                ReferencesAssembly(context.SemanticModel.Compilation, "Newtonsoft.Json")))
        {
            return null;
        }

        // 用户自己写的成员与生成成员撞名会 CS0111 / CS0102，而这类声明级编译错误会让编译器跳过
        // 全部分析器诊断 —— 包括本该指出问题所在的 STIA0029。用户于是只看到一条指向生成文件的错误。
        // 这里同步不产出，让 STIA0029 成为唯一可见的错误。判据与分析器同源。
        return GeneratedMemberNames.HasMemberCollision(symbol, info, GetIntegrationReferences(context.SemanticModel.Compilation, info))
            ? null
            : info;
    }

    /// <summary>
    /// 取本次编译引用了哪些会影响「会生成哪些成员」的集成程序集。
    /// </summary>
    /// <param name="compilation">当前编译。</param>
    /// <param name="info">当前 Id 的描述信息。</param>
    /// <remarks>
    /// 与 <c>StronglyTypedIdAnalyzer</c> 的同名计算同源，但这里跑在<b>生成前</b>的编译上：
    /// 用户自带 <c>[ProtoContract]</c> 时生成器本就不产出 surrogate，故直接读
    /// <see cref="StronglyTypedIdInfo.HasProtoContractAttribute"/> 即可，不必像分析器那样
    /// 区分「特性来自用户」还是「来自生成文件」。
    /// </remarks>
    private static GeneratedMemberNames.IntegrationReferences GetIntegrationReferences(
        Compilation compilation,
        StronglyTypedIdInfo info)
        => new(
            ReferencesAssembly(compilation, "System.Text.Json"),
            ReferencesAssembly(compilation, "Newtonsoft.Json"),
            ReferencesAssembly(compilation, "protobuf-net")
                && !info.IsInGenericContainer
                && !info.HasProtoContractAttribute);

    /// <summary>
    /// 判断该 Id 是否真的有一个名为 <c>Value</c>、类型为基元的可读属性。
    /// </summary>
    /// <param name="symbol">声明 Id 的类型。</param>
    /// <param name="primitiveType">从单参构造推出的基元类型。</param>
    /// <remarks>
    /// 生成代码通篇引用 <c>Value</c>，而它只由主构造函数合成。以下两种写法都没有它：
    /// <list type="bullet">
    /// <item>主构造的参数名不是 <c>Value</c>（<c>record OrderId(int Wrong)</c>，分析器报 STIA0007）；</item>
    /// <item>根本没有主构造，只有手写单参构造（分析器报 STIA0005）。</item>
    /// </list>
    /// 两者此前都会让生成器产出引用不存在成员的代码，用户看到的是一条指向生成文件的 CS0535，
    /// 而真正的诊断被编译错误掩盖。这里提前返回，让分析器那条成为唯一可见的错误。
    /// <para>
    /// 必须沿基类链查找：<c>record DerivedId(Guid Value) : BaseId(Value)</c> 的 <c>Value</c> 由基类声明，
    /// 派生类型自己并不合成它，但生成代码照样能解析到。
    /// </para>
    /// <para>
    /// 只认属性而不认字段：record 的主构造合成的是属性。用户显式写的 <c>Value</c> 属性同样满足，
    /// 因为生成代码只按名字访问它。
    /// </para>
    /// </remarks>
    private static bool HasValueProperty(INamedTypeSymbol symbol, ITypeSymbol primitiveType)
    {
        for (var current = symbol; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(StronglyTypedIdInfo.ValuePropertyName)
                .OfType<IPropertySymbol>()
                .Any(property => !property.IsStatic
                    && SymbolEqualityComparer.Default.Equals(property.Type, primitiveType)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断本次触发是否为该符号上最早的那次 <c>[StronglyTypedId]</c> 应用。
    /// </summary>
    /// <remarks>
    /// 同一类型可以在多个 partial 段上各标一次 <c>[StronglyTypedId]</c>（编译器报 CS0579，但生成器仍会被
    /// 触发多次），也可以在同一段里重复标注。只让最早的一次产出，其余静默跳过，避免同一个 hintName 被添加两次。
    /// <para>
    /// 比较的是<b>特性应用</b>的位置而非声明的位置：特性可以标在不承载主构造函数的后续段上，按「第一个声明」
    /// 判定会让这种写法一个产物都不生成。同一段里重复标注时两次触发的声明位置相同，也只有比较特性位置才分得开。
    /// </para>
    /// <para>
    /// 排序键是「声明在 <see cref="ISymbol.DeclaringSyntaxReferences"/> 里的序号 + 树内偏移」，不能只用
    /// <c>TextSpan.Start</c>：它是相对各自语法树的偏移，两个文件里位置相同的特性会得到同一个值，
    /// 于是两次触发都判定为「最早」，重复 <c>AddSource</c> 抛异常、该 Id 的全部产物丢失。
    /// <see cref="ISymbol.DeclaringSyntaxReferences"/> 按源码位置排序，序号因此是跨文件的稳定全序。
    /// </para>
    /// </remarks>
    private static bool IsFirstAttributeApplication(GeneratorAttributeSyntaxContext context, INamedTypeSymbol symbol)
    {
        if (context.Attributes.IsDefaultOrEmpty
            || context.Attributes[0].ApplicationSyntaxReference is not { } current)
        {
            return true;
        }

        var declarations = symbol.DeclaringSyntaxReferences;

        if (declarations.Length <= 1)
        {
            return true;
        }

        var currentKey = GetApplicationOrderKey(declarations, current);

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                    != AttributeReader.StronglyTypedIdAttributeName
                || attribute.ApplicationSyntaxReference is not { } reference)
            {
                continue;
            }

            if (IsEarlier(GetApplicationOrderKey(declarations, reference), currentKey))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 取一次特性应用在「全部声明」里的全序键：声明序号为主、树内偏移为辅。
    /// </summary>
    /// <remarks>
    /// 按「声明 span 包含特性 span」定位所属段，不能只比语法树：同一文件里可以有两个 partial 段，
    /// 那样两个特性都会匹配到第一个声明，序号失去区分度。
    /// <para>
    /// 找不到所属声明时退化为「序号取最大值」：那种特性不属于本符号的任何一个 partial 段，
    /// 让它排在最后，不影响正常声明的判定。
    /// </para>
    /// </remarks>
    private static (int Declaration, int Offset) GetApplicationOrderKey(
        ImmutableArray<SyntaxReference> declarations,
        SyntaxReference application)
    {
        for (var index = 0; index < declarations.Length; index++)
        {
            var declaration = declarations[index];

            if (declaration.SyntaxTree == application.SyntaxTree
                && declaration.Span.Contains(application.Span))
            {
                return (index, application.Span.Start);
            }
        }

        return (int.MaxValue, application.Span.Start);
    }

    /// <summary>
    /// 按「声明序号 → 树内偏移」的字典序比较两个应用位置。
    /// </summary>
    /// <remarks>
    /// 元组本身没有 <c>&lt;</c> 运算符（C# 只给元组生成 <c>==</c>），故显式展开比较。
    /// </remarks>
    private static bool IsEarlier((int Declaration, int Offset) left, (int Declaration, int Offset) right)
        => left.Declaration < right.Declaration
            || (left.Declaration == right.Declaration && left.Offset < right.Offset);

    /// <summary>
    /// 判断编译是否引用了指定简单名的程序集。
    /// </summary>
    private static bool ReferencesAssembly(Compilation compilation, string simpleName)
        => compilation.ReferencedAssemblyNames.Any(identity => string.Equals(identity.Name, simpleName, StringComparison.OrdinalIgnoreCase));
}
