using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Len.StronglyTypedId.Swagger.Generator;

[Generator]
internal class StronglyTypedIdSwaggerGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not StronglyTypedIdSyntaxReceiver syntaxReceiver)
        {
            return;
        }

        var namespaces = new HashSet<string>();
        var stronglyTypedIds = new List<(string Name, string Type)>();

        foreach (var item in syntaxReceiver.TypeSymbols)
        {
            var ctor = item.Constructors.FirstOrDefault(w => w.Parameters.Length == 1);
            if (ctor is null)
            {
                continue;
            }

            var primitiveIdTypeName = ctor.Parameters.First().Type.ToString();

            namespaces.Add(item.ContainingNamespace.ToString());
            stronglyTypedIds.Add((item.Name, primitiveIdTypeName));
        }

        foreach (var reference in context.Compilation.References
                        .Where(w => w.Properties.Kind == MetadataImageKind.Assembly &&
                            !string.IsNullOrEmpty(w.Display) &&
                            w.Display!.EndsWith(".dll") &&
                            !w.Display.Contains("Microsoft.")))
        {
            if (context.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
            {
                continue;
            }

            var typeSymbols = new List<ITypeSymbol>();

            GetTypeSymbols(assemblySymbol.GlobalNamespace, typeSymbols);

            foreach (var item in typeSymbols)
            {
                if (stronglyTypedIds.Any(w => w.Name == item.Name))
                {
                    continue;
                }

                namespaces.Add(item.ContainingNamespace.ToString());
                stronglyTypedIds.Add((
                    item.Name,
                    item.Interfaces
                        .First(w => w.Name == "IStronglyTypedId")
                        .TypeArguments
                        .First()
                        .ToString()));
            }
        }

        var sb = new StringBuilder();

        foreach (var item in namespaces)
        {
            sb.AppendLine($"using {item};");
        }

        sb.Append($@"using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace Microsoft.Extensions.DependencyInjection;

#nullable enable
internal static class SwaggerGenOptionsExtensions
{{
    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions)
    {{
");
        foreach (var item in stronglyTypedIds)
        {
            sb.AppendLine($@"        swaggerGenOptions.MapType<{item.Name}>(() => {GetOpenApiSchema(item.Type)});");
        }

        sb.Append($@"    }}
}}
#nullable disable
");
        //if (!Debugger.IsAttached)
        //{
        //    Debugger.Launch();
        //}

        //添加到源代码，这样IDE才能感知
        context.AddSource($"SwaggerGenOptionsExtensions.cs", sb.ToString());
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new StronglyTypedIdSyntaxReceiver());
    }

    private static string GetOpenApiSchema(string primitiveIdTypeName) => primitiveIdTypeName switch
    {
        "System.Guid" => "new OpenApiSchema { Type = \"string\", Format = \"uuid\" }",
        "Guid" => "new OpenApiSchema { Type = \"string\", Format = \"uuid\" }",
        "int" => "new OpenApiSchema { Type = \"integer\", Format = \"int32\" }",
        "long" => "new OpenApiSchema { Type = \"integer\", Format = \"int64\" }",
        "uint" => "new OpenApiSchema { Type = \"integer\", Format = \"uint32\" }",
        "ulong" => "new OpenApiSchema { Type = \"integer\", Format = \"uint64\" }",
        "byte" => "new OpenApiSchema { Type = \"integer\", Format = \"byte\" }",
        "sbyte" => "new OpenApiSchema { Type = \"integer\", Format = \"sbyte\" }",
        "short" => "new OpenApiSchema { Type = \"integer\", Format = \"int16\" }",
        "ushort" => "new OpenApiSchema { Type = \"integer\", Format = \"uint16\" }",
        _ => "new OpenApiSchema { Type = \"string\" }",
    };

    private void GetTypeSymbols(INamespaceOrTypeSymbol symbol, List<ITypeSymbol> typeSymbols)
    {
        if (symbol is ITypeSymbol typeSymbol && typeSymbol.Interfaces.Any(w => w.Name == "IStronglyTypedId"))
        {
            typeSymbols.Add(typeSymbol);
        }

        foreach (var memberSymbol in symbol.GetMembers().OfType<INamespaceOrTypeSymbol>())
        {
            GetTypeSymbols(memberSymbol, typeSymbols);
        }
    }

    private class StronglyTypedIdSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<INamedTypeSymbol> TypeSymbols { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            (var symbol, var syntax) = GetSymbolAndSyntax(context);

            if (symbol is not INamedTypeSymbol typeSymbol || syntax is null)
            {
                return;
            }

            if (!typeSymbol.GetAttributes()
                    .Any(x => x.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute"))
                return;

            //不处理抽象类、泛型类、嵌套类
            if (typeSymbol.IsAbstract || typeSymbol.IsGenericType || typeSymbol.ContainingType is not null)
            {
                return;
            }

            //非parital 不处理
            if (!syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return;
            }

            TypeSymbols.Add(typeSymbol);
        }

        private static (ISymbol?, MemberDeclarationSyntax?) GetSymbolAndSyntax(GeneratorSyntaxContext context)
        {
            return context.Node switch
            {
                RecordDeclarationSyntax t and { AttributeLists.Count: > 0 } => (ModelExtensions.GetDeclaredSymbol(context.SemanticModel, t), t),
                _ => (null, null),
            };
        }
    }
}