using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Len.StronglyTypedId.Swagger.Generator;

public class StronglyTypedIdSwaggerGeneratorTests
{
    [Fact]
    public async Task GeneratorCode()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);


        var code = $@"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record OrderId(int Value);
";

        var generatedCode = $@"using Len.StronglyTypedId;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace Microsoft.Extensions.DependencyInjection;

#nullable enable
internal static class SwaggerGenOptionsExtensions
{{
    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions)
    {{
        swaggerGenOptions.MapType<OrderId>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""int32"" }});
    }}
}}
#nullable disable
";

        tester.TestState.Sources.Clear();
        tester.TestState.Sources.Add(code);
        tester.TestState.GeneratedSources.Clear();
        tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdSwaggerGenerator), $"SwaggerGenOptionsExtensions.cs", generatedCode));

        await tester.RunAsync();

    }

    [Fact]
    public async Task GeneratorCode_With_Dll()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.AdditionalReferences.Add(typeof(GuidId).Assembly);

        var code = $@"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record OrderId(int Value);
";

        var generatedCode = $@"using Len.StronglyTypedId;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace Microsoft.Extensions.DependencyInjection;

#nullable enable
internal static class SwaggerGenOptionsExtensions
{{
    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions)
    {{
        swaggerGenOptions.MapType<OrderId>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""int32"" }});
        swaggerGenOptions.MapType<GuidId>(() => new OpenApiSchema {{ Type = ""string"", Format = ""uuid"" }});
        swaggerGenOptions.MapType<Int32Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""int32"" }});
        swaggerGenOptions.MapType<UInt32Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""uint32"" }});
        swaggerGenOptions.MapType<Int64Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""int64"" }});
        swaggerGenOptions.MapType<UInt64Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""uint64"" }});
        swaggerGenOptions.MapType<StringId>(() => new OpenApiSchema {{ Type = ""string"" }});
        swaggerGenOptions.MapType<ByteId>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""byte"" }});
    }}
}}
#nullable disable
";

        tester.TestState.Sources.Clear();
        tester.TestState.Sources.Add(code);
        tester.TestState.GeneratedSources.Clear();
        tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdSwaggerGenerator), $"SwaggerGenOptionsExtensions.cs", generatedCode));

        await tester.RunAsync();

    }

    [Fact]
    public async Task GeneratorCode_Only_Dll()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.AdditionalReferences.Add(typeof(GuidId).Assembly);

        var generatedCode = $@"using Len.StronglyTypedId;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace Microsoft.Extensions.DependencyInjection;

#nullable enable
internal static class SwaggerGenOptionsExtensions
{{
    public static void AddStronglyTypedId(this SwaggerGenOptions swaggerGenOptions)
    {{
        swaggerGenOptions.MapType<GuidId>(() => new OpenApiSchema {{ Type = ""string"", Format = ""uuid"" }});
        swaggerGenOptions.MapType<Int32Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""int32"" }});
        swaggerGenOptions.MapType<UInt32Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""uint32"" }});
        swaggerGenOptions.MapType<Int64Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""int64"" }});
        swaggerGenOptions.MapType<UInt64Id>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""uint64"" }});
        swaggerGenOptions.MapType<StringId>(() => new OpenApiSchema {{ Type = ""string"" }});
        swaggerGenOptions.MapType<ByteId>(() => new OpenApiSchema {{ Type = ""integer"", Format = ""byte"" }});
    }}
}}
#nullable disable
";

        tester.TestState.Sources.Clear();
        tester.TestState.GeneratedSources.Clear();
        tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdSwaggerGenerator), $"SwaggerGenOptionsExtensions.cs", generatedCode));

        await tester.RunAsync();

    }

    [Fact]
    public async Task GeneratorCode_Nothing()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        await tester.RunAsync();
    }

    [Fact]
    public async Task InvalidType_AbstractClass()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public abstract partial record GuidId(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        await tester.RunAsync();
    }

    [Fact]
    public async Task InvalidType_GenericType()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public abstract partial record GuidId<T>(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

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

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        await tester.RunAsync();
    }

    [Fact]
    public async Task NotUsePartial()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public record struct GuidId(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code },
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        await tester.RunAsync();
    }

    [Fact]
    public async Task NotUseRecord()
    {
        var code = @"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial struct GuidId(Guid Value);
";

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdSwaggerGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestState =
            {
                Sources = { code }
            },
        };

        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);

        await tester.RunAsync();
    }
}