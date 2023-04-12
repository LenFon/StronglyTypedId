using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

#if DEBUG_Generator
using System.Diagnostics;
#endif

namespace Len.StronglyTypedId.Generator;

[Generator]
internal class StronglyTypedIdGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not StronglyTypedIdSyntaxReceiver syntaxReceiver)
        {
            return;
        }

        if (!syntaxReceiver.TypeSymbols.Any()) return;

        var typeNames = new List<string>();
        foreach (var typeSymbol in syntaxReceiver.TypeSymbols)
        {
            //判断是否已经处理过这个类型了
            if (typeNames.Contains(typeSymbol.Name))
            {
                continue;
            }

            var ctor = typeSymbol.Constructors.FirstOrDefault(w => w.Parameters.Length == 1);
            if (ctor is null)
            {
                continue;
            }

            var primitiveIdTypeName = ctor.Parameters.First().Type.ToString();
            var typeKindName = typeSymbol.TypeKind == TypeKind.Struct ? "struct" : "";
            var sb = new StringBuilder();

            sb.Append($@"using Len.StronglyTypedId;

namespace {typeSymbol.ContainingNamespace};

public partial record {typeKindName} {typeSymbol.Name} : IStronglyTypedId<{primitiveIdTypeName}>
{{
    public static IStronglyTypedId<{primitiveIdTypeName}> Create({primitiveIdTypeName} value) => new {typeSymbol.Name}(value);
}}");

#if DEBUG_Generator
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
#endif
            //添加到源代码，这样IDE才能感知
            context.AddSource($"{typeSymbol.Name}.g.cs", sb.ToString());

            //避免重复生成
            typeNames.Add(typeSymbol.Name);
        }
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new StronglyTypedIdSyntaxReceiver());
    }

    private class StronglyTypedIdSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<INamedTypeSymbol> TypeSymbols { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is not RecordDeclarationSyntax ids || ids.AttributeLists.Count <= 0)
                return;

            //非parital 不处理
            if (!ids.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                return;

            if (ModelExtensions.GetDeclaredSymbol(context.SemanticModel, ids) is not INamedTypeSymbol typeSymbol)
                return;

            //不处理抽象类、泛型类、嵌套类
            if (typeSymbol.IsAbstract || typeSymbol.IsGenericType || typeSymbol.ContainingType is not null)
                return;

            if (!typeSymbol.GetAttributes()
                    .Any(x => x.AttributeClass?.ToDisplayString() == "Len.StronglyTypedId.StronglyTypedIdAttribute"))
                return;

            TypeSymbols.Add(typeSymbol);
        }
    }
}