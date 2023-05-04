using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Len.StronglyTypedId.EntityFrameworkCore.Generator;

public class StronglyTypedIdConverterGeneratorTests
{
    [Fact]
    public async Task GeneratorCode()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);


        var code = $@"namespace Len.StronglyTypedId;

[StronglyTypedId]
public partial record OrderId(int Value);
";

        var generatedCode = $@"using Len.StronglyTypedId;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Microsoft.EntityFrameworkCore;

#nullable enable

internal static class ModelConfigurationBuilderExtensions
{{
    public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder)
    {{
        configurationBuilder.Properties<OrderId>().HaveConversion(typeof(OrderIdConverter));
    }}

    class OrderIdConverter : ValueConverter<OrderId, int>
    {{
        public OrderIdConverter() : base(v => v.Value, val => new OrderId(val))
        {{
        }}
    }}
}}

#nullable disable
";

        tester.TestState.Sources.Clear();
        tester.TestState.Sources.Add(code);
        tester.TestState.GeneratedSources.Clear();
        tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdConverterGenerator), $"ModelConfigurationBuilderExtensions.cs", generatedCode));

        await tester.RunAsync();

    }

    [Fact]
    public async Task GeneratorCode_With_Dll()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Microsoft.EntityFrameworkCore;

#nullable enable

internal static class ModelConfigurationBuilderExtensions
{{
    public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder)
    {{
        configurationBuilder.Properties<OrderId>().HaveConversion(typeof(OrderIdConverter));
        configurationBuilder.Properties<GuidId>().HaveConversion(typeof(GuidIdConverter));
        configurationBuilder.Properties<Int32Id>().HaveConversion(typeof(Int32IdConverter));
        configurationBuilder.Properties<UInt32Id>().HaveConversion(typeof(UInt32IdConverter));
        configurationBuilder.Properties<Int64Id>().HaveConversion(typeof(Int64IdConverter));
        configurationBuilder.Properties<UInt64Id>().HaveConversion(typeof(UInt64IdConverter));
        configurationBuilder.Properties<StringId>().HaveConversion(typeof(StringIdConverter));
        configurationBuilder.Properties<ByteId>().HaveConversion(typeof(ByteIdConverter));
    }}

    class OrderIdConverter : ValueConverter<OrderId, int>
    {{
        public OrderIdConverter() : base(v => v.Value, val => new OrderId(val))
        {{
        }}
    }}
    class GuidIdConverter : ValueConverter<GuidId, System.Guid>
    {{
        public GuidIdConverter() : base(v => v.Value, val => new GuidId(val))
        {{
        }}
    }}
    class Int32IdConverter : ValueConverter<Int32Id, int>
    {{
        public Int32IdConverter() : base(v => v.Value, val => new Int32Id(val))
        {{
        }}
    }}
    class UInt32IdConverter : ValueConverter<UInt32Id, uint>
    {{
        public UInt32IdConverter() : base(v => v.Value, val => new UInt32Id(val))
        {{
        }}
    }}
    class Int64IdConverter : ValueConverter<Int64Id, long>
    {{
        public Int64IdConverter() : base(v => v.Value, val => new Int64Id(val))
        {{
        }}
    }}
    class UInt64IdConverter : ValueConverter<UInt64Id, ulong>
    {{
        public UInt64IdConverter() : base(v => v.Value, val => new UInt64Id(val))
        {{
        }}
    }}
    class StringIdConverter : ValueConverter<StringId, string>
    {{
        public StringIdConverter() : base(v => v.Value, val => new StringId(val))
        {{
        }}
    }}
    class ByteIdConverter : ValueConverter<ByteId, byte>
    {{
        public ByteIdConverter() : base(v => v.Value, val => new ByteId(val))
        {{
        }}
    }}
}}

#nullable disable
";

        tester.TestState.Sources.Clear();
        tester.TestState.Sources.Add(code);
        tester.TestState.GeneratedSources.Clear();
        tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdConverterGenerator), $"ModelConfigurationBuilderExtensions.cs", generatedCode));

        await tester.RunAsync();

    }

    [Fact]
    public async Task GeneratorCode_Only_Dll()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
        {
            CompilerDiagnostics = CompilerDiagnostics.None,
        };
        tester.TestState.AdditionalReferences.Add(typeof(StronglyTypedIdAttribute).Assembly);
        tester.TestState.AdditionalReferences.Add(typeof(GuidId).Assembly);

        var generatedCode = $@"using Len.StronglyTypedId;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Microsoft.EntityFrameworkCore;

#nullable enable

internal static class ModelConfigurationBuilderExtensions
{{
    public static void AddStronglyTypedId(this ModelConfigurationBuilder configurationBuilder)
    {{
        configurationBuilder.Properties<GuidId>().HaveConversion(typeof(GuidIdConverter));
        configurationBuilder.Properties<Int32Id>().HaveConversion(typeof(Int32IdConverter));
        configurationBuilder.Properties<UInt32Id>().HaveConversion(typeof(UInt32IdConverter));
        configurationBuilder.Properties<Int64Id>().HaveConversion(typeof(Int64IdConverter));
        configurationBuilder.Properties<UInt64Id>().HaveConversion(typeof(UInt64IdConverter));
        configurationBuilder.Properties<StringId>().HaveConversion(typeof(StringIdConverter));
        configurationBuilder.Properties<ByteId>().HaveConversion(typeof(ByteIdConverter));
    }}

    class GuidIdConverter : ValueConverter<GuidId, System.Guid>
    {{
        public GuidIdConverter() : base(v => v.Value, val => new GuidId(val))
        {{
        }}
    }}
    class Int32IdConverter : ValueConverter<Int32Id, int>
    {{
        public Int32IdConverter() : base(v => v.Value, val => new Int32Id(val))
        {{
        }}
    }}
    class UInt32IdConverter : ValueConverter<UInt32Id, uint>
    {{
        public UInt32IdConverter() : base(v => v.Value, val => new UInt32Id(val))
        {{
        }}
    }}
    class Int64IdConverter : ValueConverter<Int64Id, long>
    {{
        public Int64IdConverter() : base(v => v.Value, val => new Int64Id(val))
        {{
        }}
    }}
    class UInt64IdConverter : ValueConverter<UInt64Id, ulong>
    {{
        public UInt64IdConverter() : base(v => v.Value, val => new UInt64Id(val))
        {{
        }}
    }}
    class StringIdConverter : ValueConverter<StringId, string>
    {{
        public StringIdConverter() : base(v => v.Value, val => new StringId(val))
        {{
        }}
    }}
    class ByteIdConverter : ValueConverter<ByteId, byte>
    {{
        public ByteIdConverter() : base(v => v.Value, val => new ByteId(val))
        {{
        }}
    }}
}}

#nullable disable
";

        tester.TestState.Sources.Clear();
        tester.TestState.GeneratedSources.Clear();
        tester.TestState.GeneratedSources.Add((typeof(StronglyTypedIdConverterGenerator), $"ModelConfigurationBuilderExtensions.cs", generatedCode));

        await tester.RunAsync();

    }

    [Fact]
    public async Task GeneratorCode_Nothing()
    {
        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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

        var tester = new CSharpSourceGeneratorTest<StronglyTypedIdConverterGenerator, XUnitVerifier>()
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