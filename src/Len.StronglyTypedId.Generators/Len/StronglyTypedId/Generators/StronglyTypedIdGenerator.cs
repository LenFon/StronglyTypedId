namespace Len.StronglyTypedId.Generators;

[Generator]
internal class StronglyTypedIdGenerator : IIncrementalGenerator
{
    private static readonly string _version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //Debugger.Launch();
        var stronglyTypedIdInfos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Len.StronglyTypedId.StronglyTypedIdAttribute",
                CouldBeStronglyTypedId,
                GetStronglyTypedIdInfoOrNull)
           .Where(static w => w is not null)
           .Select(static (s, _) => s!.Value)
           .Collect();

        var modules = context.MetadataReferencesProvider
            .Combine(context.CompilationProvider)
            .SelectMany(static (s, _) => s.Left.GetModules(s.Right))
            .WithComparer(new ModuleInfo.Comparer())
            .Collect();

        var dbContexts = context.SyntaxProvider
            .CreateSyntaxProvider(ClouldBeEfCoreDbContext, static (context, _) => true)
            .Collect();

        var swaggers = context.SyntaxProvider.CreateSyntaxProvider(
            static (syntaxNode, _) => syntaxNode is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "AddSwaggerGen" },
            static (context, _) => true)
            .Collect();

        context.RegisterSourceOutput(stronglyTypedIdInfos.Combine(modules), GenerateCode);
        context.RegisterSourceOutput(stronglyTypedIdInfos.Combine(modules).Combine(dbContexts), EfCoreGenerateCode);
        context.RegisterSourceOutput(stronglyTypedIdInfos.Combine(modules).Combine(swaggers), SwaggerGenerateCode);
    }

    private static bool ClouldBeEfCoreDbContext(SyntaxNode syntaxNode, CancellationToken cancellationToken)
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

        if (!modifiers.Any(SyntaxKind.OverrideKeyword) || !modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return false;
        }

        return true;
    }

    private static bool CouldBeStronglyTypedId(SyntaxNode syntaxNode, CancellationToken cancellationToken)
    {
        if (syntaxNode is not RecordDeclarationSyntax
            {
                ParameterList.Parameters: [{ Type: not NullableTypeSyntax, Identifier.ValueText: "Value" }],
                TypeParameterList: null, //非泛型
                Parent: BaseNamespaceDeclarationSyntax, //非嵌套类型，并且有命名空间
                Modifiers: var modifiers and not [],
            })
        {
            return false;
        }

        if (!modifiers.Any(SyntaxKind.PartialKeyword) || modifiers.Any(SyntaxKind.AbstractKeyword))
        {
            return false;
        }

        return true;
    }

    private static void EfCoreGenerateCode(SourceProductionContext context, ((ImmutableArray<StronglyTypedIdTypeInfo>, ImmutableArray<ModuleInfo>), ImmutableArray<bool>) args)
    {
        var ((stronglyTypedIdInfos, modules), dbContexts) = args;

        if (dbContexts.IsDefaultOrEmpty) return;

        GetEfCoreCodeGenerator(modules)?.Excute(stronglyTypedIdInfos, modules, context, _version);
    }

    private static void GenerateCode(SourceProductionContext context, (ImmutableArray<StronglyTypedIdTypeInfo>, ImmutableArray<ModuleInfo>) args)
    {
        var (stronglyTypedIdInfos, modules) = args;

        var generators = GetCodeGenerators(modules);

        if (generators.IsDefaultOrEmpty) return;

        foreach (var generator in generators
            .Where(w => w.Name is not nameof(EfCoreCodeGenerator) or nameof(SwaggerCodeGenerator))
            .OrderBy(o => o.Order))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            generator.Excute(stronglyTypedIdInfos, modules, context, _version);
        }
    }

    private static ImmutableArray<ICodeGenerator> GetCodeGenerators(ImmutableArray<ModuleInfo> modules)
    {
        var codeGenerators = modules
            .Select(s => s switch
            {
                { Name: "System.Text.Json.dll" } => SystemTextJsonCodeGenerator.Instance,
                { Name: "Len.StronglyTypedId.dll" } => StronglyTypedIdCodeGenerator.Instance,
                { Name: "Newtonsoft.Json.dll", Version.Major: >= 13 } => NewtonsoftJsonCodeGenerator.Instance,
                _ => null,
            })
            .OfType<ICodeGenerator>();

        return codeGenerators.Any() ? ImmutableArray.CreateRange(codeGenerators) : ImmutableArray<ICodeGenerator>.Empty;
    }

    private static ICodeGenerator? GetEfCoreCodeGenerator(ImmutableArray<ModuleInfo> modules)
    {
        foreach (var item in modules)
        {
            if (item is { Name: "Microsoft.EntityFrameworkCore.dll", Version.Major: >= 7 })
            {
                return EfCoreCodeGenerator.Instance;
            }
        }

        return null;
    }

    private static StronglyTypedIdTypeInfo? GetStronglyTypedIdInfoOrNull(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
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

        var supportedTypeNames = new string[]
        {
            nameof(Guid),
            nameof(String),
            nameof(Byte),
            nameof(SByte),
            nameof(Int16),
            nameof(Int32),
            nameof(Int64),
            nameof(UInt16),
            nameof(UInt32),
            nameof(UInt64)
        };

        if (!supportedTypeNames.Contains(ctorArgType.Name))
        {
            return null;
        }

        return new(symbol);
    }

    private static ICodeGenerator? GetSwaggerCodeGenerator(ImmutableArray<ModuleInfo> modules)
    {
        foreach (var item in modules)
        {
            if (item is { Name: "Swashbuckle.AspNetCore.SwaggerGen.dll", Version.Major: >= 6 })
            {
                return SwaggerCodeGenerator.Instance;
            }
        }

        return null;
    }

    private static void SwaggerGenerateCode(SourceProductionContext context, ((ImmutableArray<StronglyTypedIdTypeInfo>, ImmutableArray<ModuleInfo>), ImmutableArray<bool>) args)
    {
        var ((stronglyTypedIdInfos, modules), swaggers) = args;

        if (swaggers.IsDefaultOrEmpty) return;

        GetSwaggerCodeGenerator(modules)?.Excute(stronglyTypedIdInfos, modules, context, _version);
    }
}