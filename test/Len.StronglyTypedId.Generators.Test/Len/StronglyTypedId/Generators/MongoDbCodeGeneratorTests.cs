// xUnit1051：本文件中的 CSharpSyntaxTree.ParseText 均为测试内同步调用，取消令牌无意义，
// 统一在此文件禁用该测试框架分析器警告。
#pragma warning disable xUnit1051
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// <see cref="MongoDbCodeGenerator"/> 的基元序列化器预注册分支。
/// </summary>
/// <remarks>
/// 注册语句按基元短名分派，每个分支对应一个驱动里的具体序列化器类型名。漏一个分支的表现是
/// 该基元的 Id 在 Native AOT 下抛 <c>MissingMethodException</c>（驱动靠反射创建默认实例），
/// 而普通运行时下一切正常 —— 只有 AOT 才暴露，故必须逐分支断言产物文本。
/// </remarks>
public class MongoDbCodeGeneratorTests
{
    #region 基元 → 预注册序列化器

    [Theory]
    [InlineData("byte", "ByteSerializer")]
    [InlineData("sbyte", "SByteSerializer")]
    [InlineData("short", "Int16Serializer")]
    [InlineData("ushort", "UInt16Serializer")]
    [InlineData("int", "Int32Serializer")]
    [InlineData("uint", "UInt32Serializer")]
    [InlineData("long", "Int64Serializer")]
    [InlineData("ulong", "UInt64Serializer")]
    [InlineData("float", "SingleSerializer")]
    [InlineData("double", "DoubleSerializer")]
    [InlineData("decimal", "DecimalSerializer")]
    [InlineData("char", "CharSerializer")]
    [InlineData("string", "StringSerializer")]
    [InlineData("DateTime", "DateTimeSerializer")]
    [InlineData("DateTimeOffset", "DateTimeOffsetSerializer")]
    [InlineData("TimeSpan", "TimeSpanSerializer")]
    [InlineData("DateOnly", "DateOnlySerializer")]
    [InlineData("TimeOnly", "TimeOnlySerializer")]
    [InlineData("Half", "HalfSerializer")]
    public void RegisterBsonSerializers_Should_PreRegisterPrimitiveSerializer(string primitive, string serializerName)
    {
        var registration = GetRegistration(primitive);

        registration.Should().Contain(
            $"global::MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(typeof({PrimitiveTypeName(primitive)}), new global::MongoDB.Bson.Serialization.Serializers.{serializerName}());");
    }

    /// <summary>
    /// 枚举基元按 <c>EnumSerializer&lt;TEnum&gt;</c> 注册，而不是某个具体整型序列化器。
    /// </summary>
    [Fact]
    public void RegisterBsonSerializers_Should_PreRegisterEnumSerializer()
    {
        var registration = GetRegistration(enumSource: true);

        registration.Should().Contain(
            "new global::MongoDB.Bson.Serialization.Serializers.EnumSerializer<global::Probe.Access>()");
    }

    /// <summary>
    /// 走规范文本形的基元（<c>BigInteger</c> / <c>Int128</c> / <c>UInt128</c>）不预注册：
    /// 驱动没有对应序列化器，载荷本身是字符串，注册反而会引入一个不存在的类型。
    /// </summary>
    [Theory]
    [InlineData("System.Numerics.BigInteger")]
    [InlineData("Int128")]
    [InlineData("UInt128")]
    public void RegisterBsonSerializers_Should_NotPreRegisterTextPrimitive(string primitive)
    {
        var registration = GetRegistration(primitive);

        registration.Should().NotContain("TryRegisterSerializer(typeof(" + PrimitiveTypeName(primitive) + ")");
    }

    /// <summary>
    /// Guid 不预注册：驱动 3.0 起没有全局默认表示，消费者必须自选表示后自行注册。
    /// </summary>
    /// <remarks>
    /// 断言注册语句而不是「产物不含 GuidSerializer」：产物注释里刻意提到了该类型名。
    /// </remarks>
    [Fact]
    public void RegisterBsonSerializers_Should_NotPreRegisterGuid()
    {
        var registration = GetRegistration("Guid");

        registration.Should().NotContain("TryRegisterSerializer(typeof(global::System.Guid)");
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 取 <c>StronglyTypedIds.MongoDb.g.cs</c> 的产物文本。
    /// </summary>
    private static string GetRegistration(string? primitive = null, bool enumSource = false)
    {
        var source = enumSource
            ? """
              using System;
              using Len.StronglyTypedId;

              namespace Probe;

              public enum Access { None = 0, Read = 1 }

              [StronglyTypedId]
              public partial record struct OrderId(Access Value);
              """
            : $$"""
              using System;
              using Len.StronglyTypedId;

              namespace Probe;

              [StronglyTypedId]
              public partial record struct OrderId({{primitive}} Value);
              """;

        var compilation = CSharpCompilation.Create(
            "Probe",
            [CSharpSyntaxTree.ParseText(source)],
            [
                AssemblyReferenceCache.FromAssembly(typeof(StronglyTypedIdAttribute).Assembly),
                AssemblyReferenceCache.FromAssembly(typeof(object).Assembly),
                AssemblyReferenceCache.FromAssembly(typeof(System.Numerics.BigInteger).Assembly),
                SyntheticAssembly.GetOrCreate("MongoDB.Bson", "3.0.0.0"),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = CSharpGeneratorDriver
            .Create(new StronglyTypedIdGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        return result.Results[0].GeneratedSources
            .Single(source => source.HintName == "StronglyTypedIds.MongoDb.g.cs")
            .SourceText.ToString();
    }

    /// <summary>
    /// 注册语句里 <c>typeof(...)</c> 用的基元显示名：特型基元是关键字，其余带 <c>global::</c>。
    /// </summary>
    private static string PrimitiveTypeName(string primitive)
        => primitive switch
        {
            "bool" or "byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong"
                or "float" or "double" or "decimal" or "char" or "string" => primitive,
            _ => "global::System." + primitive,
        };

    #endregion
}
