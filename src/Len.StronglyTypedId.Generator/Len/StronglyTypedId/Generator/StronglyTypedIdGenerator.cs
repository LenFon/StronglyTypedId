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

        if (syntaxReceiver.Diagnostics.Any())
        {
            syntaxReceiver.Diagnostics.ForEach(context.ReportDiagnostic);
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
using System.Diagnostics.CodeAnalysis;

namespace {typeSymbol.ContainingNamespace};

#nullable enable

[System.Text.Json.Serialization.JsonConverter(typeof({typeSymbol.Name}JsonConverter))]
public partial record {typeKindName} {typeSymbol.Name} : IStronglyTypedId<{primitiveIdTypeName}>, IParsable<{typeSymbol.Name}>
{{
    public static IStronglyTypedId<{primitiveIdTypeName}> Create({primitiveIdTypeName} value) => new {typeSymbol.Name}(value);

    public static {typeSymbol.Name} Parse(string value, IFormatProvider? provider)
    {{
        if (!TryParse(value, provider, out var id))
        {{
            throw new ArgumentException(""Could not parse supplied value."", nameof(value));
        }}

        return id;
    }}

    public static bool TryParse([NotNullWhen(true)] string? value, IFormatProvider? provider, [MaybeNullWhen(false)] out {typeSymbol.Name} result)
    {{
        if ({(primitiveIdTypeName == "string" ? "!string.IsNullOrEmpty(value)" : $"{primitiveIdTypeName}.TryParse(value, provider, out var val)")})
        {{
            result = new {typeSymbol.Name}({(primitiveIdTypeName == "string" ? "value" : "val")});
            return true;
        }}

        result = default;
        return false;
    }}");

            sb.Append($@"
    class {typeSymbol.Name}JsonConverter : System.Text.Json.Serialization.JsonConverter<{typeSymbol.Name}>
    {{
        public override {typeSymbol.Name} Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {{
            var value = {GetValue(primitiveIdTypeName)};

            return new {typeSymbol.Name}(value);
        }}

        public override void Write(System.Text.Json.Utf8JsonWriter writer, {typeSymbol.Name} value, System.Text.Json.JsonSerializerOptions options)
        {{
            {WriteValue(primitiveIdTypeName)};
        }}
    }}
");
            sb.Append($@"
}}

#nullable disable");
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

    private static string GetValue(string primitiveIdTypeName) => primitiveIdTypeName switch
    {
        "System.Guid" => "reader.GetGuid()",
        "int" => "reader.GetInt32()",
        "long" => "reader.GetInt64()",
        "uint" => "reader.GetUInt32()",
        "ulong" => "reader.GetUInt64()",
        "byte" => "reader.GetByte()",
        "sbyte" => "reader.GetSByte()",
        "short" => "reader.GetInt16()",
        "ushort" => "reader.GetUInt16()",
        _ => "reader.GetString() ?? string.Empty",
    };

    private static string WriteValue(string primitiveIdTypeName) => primitiveIdTypeName switch
    {
        "System.Guid" => "writer.WriteStringValue(value.Value.ToString())",
        "int" => "writer.WriteNumberValue(value.Value)",
        "long" => "writer.WriteNumberValue(value.Value)",
        "uint" => "writer.WriteNumberValue(value.Value)",
        "ulong" => "writer.WriteNumberValue(value.Value)",
        "byte" => "writer.WriteNumberValue(value.Value)",
        "sbyte" => "writer.WriteNumberValue(value.Value)",
        "short" => "writer.WriteNumberValue(value.Value)",
        "ushort" => "writer.WriteNumberValue(value.Value)",
        _ => "writer.WriteStringValue(value.Value)",
    };

    private class StronglyTypedIdSyntaxReceiver : ISyntaxContextReceiver
    {
        static StronglyTypedIdSyntaxReceiver()
        {
            Resources.Culture = System.Globalization.CultureInfo.InstalledUICulture;
        }

        public List<INamedTypeSymbol> TypeSymbols { get; } = new();

        public List<Diagnostic> Diagnostics { get; } = new();

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

            if (!typeSymbol.IsRecord)
            {
                Diagnostics.Add(Diagnostic.Create(new DiagnosticDescriptor(
                 "STIAE003",
                 Resources.Title,
                 Resources.NotUseRecordMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true), syntax.GetLocation(), typeSymbol.Name));

                return;
            }

            //不处理抽象类、泛型类、嵌套类
            if (typeSymbol.IsAbstract || typeSymbol.IsGenericType || typeSymbol.ContainingType is not null)
            {
                Diagnostics.Add(Diagnostic.Create(new DiagnosticDescriptor(
                 "STIAE001",
                 Resources.Title,
                 Resources.InvalidTypeMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true), syntax.GetLocation(), typeSymbol.Name));

                return;
            }

            //非parital 不处理
            if (!syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                Diagnostics.Add(Diagnostic.Create(new DiagnosticDescriptor(
                  "STIAE002",
                  Resources.Title,
                  Resources.NotUsePartialMessage,
                  "StronglyTypedId",
                  DiagnosticSeverity.Error,
                  true), syntax.GetLocation(), typeSymbol.Name));

                return;
            }

            TypeSymbols.Add(typeSymbol);
        }

        private static (ISymbol?, MemberDeclarationSyntax?) GetSymbolAndSyntax(GeneratorSyntaxContext context)
        {
            return context.Node switch
            {
                ClassDeclarationSyntax t and { AttributeLists.Count: > 0 } => (ModelExtensions.GetDeclaredSymbol(context.SemanticModel, t), t),
                StructDeclarationSyntax t and { AttributeLists.Count: > 0 } => (ModelExtensions.GetDeclaredSymbol(context.SemanticModel, t), t),
                RecordDeclarationSyntax t and { AttributeLists.Count: > 0 } => (ModelExtensions.GetDeclaredSymbol(context.SemanticModel, t), t),
                _ => (null, null),
            };
        }
    }
}