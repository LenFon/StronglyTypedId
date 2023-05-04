using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using System.Text;

namespace Len.StronglyTypedId.Generator;

public class StronglyTypedIdGeneratorTests
{
    [Fact]
    public async Task GeneratorCode_Record_Struct()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        var types = new[] { "Guid", "System.Guid", "int", "long", "short", "byte", "uint", "ulong", "ushort", "sbyte", "string", "AA" };
        foreach (var type in types)
        {
            var code = $@"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record struct OrderId({type} Value);
";

            var generatedCode = GeneratedCode("Len.StronglyTypedId", "OrderId", type, "struct");

            tester.TestState.Sources.Clear();
            tester.TestState.Sources.Add(code);
            tester.TestState.GeneratedSources.Clear();
            tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdGenerator), $"OrderId.g.cs", generatedCode));

            await tester.RunAsync();
        }
    }

    [Fact]
    public async Task GeneratorCode_Record_Class()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        var types = new[] { "Guid", "System.Guid", "int", "long", "short", "byte", "uint", "ulong", "ushort", "sbyte", "string", "AA" };
        foreach (var type in types)
        {
            var code = $@"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record OrderId({type} Value);
";

            var generatedCode = GeneratedCode("Len.StronglyTypedId", "OrderId", type, "");

            tester.TestState.Sources.Clear();
            tester.TestState.Sources.Add(code);
            tester.TestState.GeneratedSources.Clear();
            tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdGenerator), $"OrderId.g.cs", generatedCode));

            await tester.RunAsync();
        }
    }

    [Fact]
    public async Task InvalidType_AbstractClass()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public abstract partial record GuidId(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(new DiagnosticDescriptor(
                 "STIAE001",
                 Resources.Title,
                 Resources.InvalidTypeMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true)).WithSpan(3, 1, 4, 51).WithArguments("GuidId"));

        await tester.RunAsync();
    }

    [Fact]
    public async Task InvalidType_GenericType()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public abstract partial record GuidId<T>(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(new DiagnosticDescriptor(
                 "STIAE001",
                 Resources.Title,
                 Resources.InvalidTypeMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true)).WithSpan(3, 1, 4, 54).WithArguments("GuidId"));

        await tester.RunAsync();
    }

    [Fact]
    public async Task InvalidType_NestedType()
    {
        var code = @"namespace Len.StronglyTypedId;

public class AA
{
    [StronglyTypedId]
    public abstract partial record GuidId<T>(Guid Value);
}
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(new DiagnosticDescriptor(
                 "STIAE001",
                 Resources.Title,
                 Resources.InvalidTypeMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true)).WithSpan(5, 5, 6, 58).WithArguments("GuidId"));

        await tester.RunAsync();
    }

    [Fact]
    public async Task NotUsePartial()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public record struct GuidId(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(new DiagnosticDescriptor(
                 "STIAE002",
                 Resources.Title,
                 Resources.NotUsePartialMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true)).WithSpan(3, 1, 4, 41).WithArguments("GuidId"));

        await tester.RunAsync();
    }

    [Fact]
    public async Task NotUseRecord()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial struct GuidId(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code }
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.ExpectedDiagnostics.Add(new DiagnosticResult(new DiagnosticDescriptor(
                 "STIAE003",
                 Resources.Title,
                 Resources.NotUseRecordMessage,
                 "StronglyTypedId",
                 DiagnosticSeverity.Error,
                 true)).WithSpan(3, 1, 4, 29).WithArguments("GuidId"));

        await tester.RunAsync();
    }

    private static string GeneratedCode(string containingNamespace, string stronglyTypedIdName, string primitiveIdTypeName, string typeKindName = "")
    {
        var sb = new StringBuilder();

        sb.Append($@"using Len.StronglyTypedId;
using System.Diagnostics.CodeAnalysis;

namespace {containingNamespace};

#nullable enable

[System.Text.Json.Serialization.JsonConverter(typeof({stronglyTypedIdName}JsonConverter))]
public partial record {typeKindName} {stronglyTypedIdName} : IStronglyTypedId<{primitiveIdTypeName}>, IParsable<{stronglyTypedIdName}>
{{
    public static IStronglyTypedId<{primitiveIdTypeName}> Create({primitiveIdTypeName} value) => new {stronglyTypedIdName}(value);

    public static {stronglyTypedIdName} Parse(string value, IFormatProvider? provider)
    {{
        if (!TryParse(value, provider, out var id))
        {{
            throw new ArgumentException(""Could not parse supplied value."", nameof(value));
        }}

        return id;
    }}

    public static bool TryParse([NotNullWhen(true)] string? value, IFormatProvider? provider, [MaybeNullWhen(false)] out {stronglyTypedIdName} result)
    {{
        if ({(primitiveIdTypeName == "string" ? "!string.IsNullOrEmpty(value)" : $"{primitiveIdTypeName}.TryParse(value, provider, out var val)")})
        {{
            result = new {stronglyTypedIdName}({(primitiveIdTypeName == "string" ? "value" : "val")});
            return true;
        }}

        result = default;
        return false;
    }}

    class {stronglyTypedIdName}JsonConverter : System.Text.Json.Serialization.JsonConverter<{stronglyTypedIdName}>
    {{
        public override {stronglyTypedIdName}{(string.IsNullOrEmpty(typeKindName) ? "?" : "")} Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
        {{
            var value = {GetValue(primitiveIdTypeName)};

            return new {stronglyTypedIdName}(value);
        }}

        public override void Write(System.Text.Json.Utf8JsonWriter writer, {stronglyTypedIdName} value, System.Text.Json.JsonSerializerOptions options)
        {{
            {WriteValue(primitiveIdTypeName)};
        }}
    }}
}}");

        return sb.ToString();
    }

    private static string GetValue(string primitiveIdTypeName) => primitiveIdTypeName switch
    {
        "System.Guid" => "reader.GetGuid()",
        "Guid" => "reader.GetGuid()",
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
        "Guid" => "writer.WriteStringValue(value.Value.ToString())",
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
}