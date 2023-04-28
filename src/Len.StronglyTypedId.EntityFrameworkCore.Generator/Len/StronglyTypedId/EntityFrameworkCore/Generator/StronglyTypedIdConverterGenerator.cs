using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Len.StronglyTypedId.EntityFrameworkCore.Generator;

[Generator]
internal class StronglyTypedIdConverterGenerator : ISourceGenerator
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

        sb.Append($@"using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Microsoft.EntityFrameworkCore;

#nullable enable

internal static class ModelConfigurationBuilderExtensions
{{
    public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder)
    {{
");
        foreach (var item in stronglyTypedIds)
        {
            sb.AppendLine($@"        configurationBuilder.Properties<{item.Name}>().HaveConversion(typeof({item.Name}Converter));");
        }

        sb.Append($@"
    }}
");

        foreach (var item in stronglyTypedIds)
        {
            sb.Append($@"
    class {item.Name}Converter : ValueConverter<{item.Name}, {item.Type}>
    {{
        public {item.Name}Converter() : base(v => v.Value, val => new {item.Name}(val))
        {{
        }}
    }}");
        }

        sb.Append($@"
}}

#nullable disable
");
        //if (!Debugger.IsAttached)
        //{
        //    Debugger.Launch();
        //}

        //添加到源代码，这样IDE才能感知
        context.AddSource($"ModelConfigurationBuilderExtensions.cs", sb.ToString());
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new StronglyTypedIdSyntaxReceiver());
    }

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