using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Text;
using FluentAssertions;

namespace Len.StronglyTypedId;

/// <summary>
/// 覆盖由源码生成器生成的强类型 Id 运行时行为： Create / Parse / TryParse（IParsable 契约）、相等性、ToString、接口契约，以及
/// StronglyTypedIdExtensions 在枚举/数组/普通 record 上的边界分支。
/// </summary>
public class StronglyTypedIdBehaviorTests
{
    #region 测试数据：覆盖所有受支持基元类型（record struct 与 record 两种形态）

    /// <summary>
    /// 样例三元组：(Id 类型, 合法值的字符串形式, 非法输入)。三种列都使用可序列化类型，Test Explorer 才能逐行枚举每条数据（若数据行含有不可序列化的
    /// object/Guid，xUnit1042 与 xUnit1045 二者必居其一）。基元值本身由 <see cref="PrimitiveValue"/> 从字符串还原，保持数据源的稳定性。
    /// </summary>
    private static readonly (Type IdType, string Text, string InvalidInput)[] _samples =
    [
        // record struct
        (typeof(GuidId), "d8ac85d4-ed76-4974-b055-8ef3508743f3", "not-a-guid"),
        (typeof(Int32Id), "12", "abc"),
        (typeof(UInt32Id), "14", "abc"),
        (typeof(Int64Id), "13", "abc"),
        (typeof(UInt64Id), "15", "abc"),
        (typeof(ByteId), "16", "abc"),
        (typeof(SByteId), "12", "abc"),
        (typeof(Int16Id), "1234", "abc"),
        (typeof(UInt16Id), "1234", "abc"),
        (typeof(StringId), "Len", ""),
        (typeof(StringIdV3), "Len", ""),
        // char：合法文本必须恰为一个字符，故非法输入取两个字符（空串同样非法，见下面的专项用例）。
        (typeof(CharId), "L", "Len"),

        // record（引用类型）
        (typeof(GuidIdV2), "d8ac85d4-ed76-4974-b055-8ef3508743f3", "not-a-guid"),
        (typeof(Int32IdV2), "12", "abc"),
        (typeof(UInt32IdV2), "14", "abc"),
        (typeof(Int64IdV2), "13", "abc"),
        (typeof(UInt64IdV2), "15", "abc"),
        (typeof(ByteIdV2), "16", "abc"),
        (typeof(SByteIdV2), "12", "abc"),
        (typeof(Int16IdV2), "1234", "abc"),
        (typeof(UInt16IdV2), "1234", "abc"),
        (typeof(StringIdV2), "Len", ""),
    ];

    /// <summary>
    /// 合法样例：(Id 类型, 值的字符串形式)。
    /// </summary>
    public static TheoryData<Type, string> IdSamples()
    {
        var data = new TheoryData<Type, string>();

        foreach (var (idType, text, _) in _samples)
        {
            data.Add(idType, text);
        }

        return data;
    }

    /// <summary>
    /// 非法输入样例：(Id 类型, 非法输入)。
    /// </summary>
    public static TheoryData<Type, string> InvalidIdSamples()
    {
        var data = new TheoryData<Type, string>();

        foreach (var (idType, _, invalidInput) in _samples)
        {
            data.Add(idType, invalidInput);
        }

        return data;
    }

    /// <summary>
    /// 全部 Id 类型，供与具体取值无关的用例使用。
    /// </summary>
    public static TheoryData<Type> IdTypes()
    {
        var data = new TheoryData<Type>();

        foreach (var (idType, _, _) in _samples)
        {
            data.Add(idType);
        }

        return data;
    }

    #endregion

    #region Create

    [Theory]
    [MemberData(nameof(IdSamples))]
    public void Create_Should_ProduceIdWithSameValue(Type idType, string text)
    {
        var value = PrimitiveValue(idType, text);

        var id = Create(idType, value);

        GetValue(id).Should().Be(value);
    }

    [Fact]
    public void Create_Should_ReturnIStronglyTypedId_ForRecordStruct()
    {
        var g = Guid.NewGuid();
        var id = GuidId.Create(g);

        // 编译期契约：生成代码实现了 IStronglyTypedId<TSelf, TPrimitiveId>
        AsStronglyTypedId<GuidId, Guid>(id).Value.Should().Be(g);
    }

    [Fact]
    public void Create_Should_ReturnIStronglyTypedId_ForRecord()
    {
        var g = Guid.NewGuid();
        var id = GuidIdV2.Create(g);

        AsStronglyTypedId<GuidIdV2, Guid>(id).Value.Should().Be(g);
    }

    #endregion

    #region Parse

    [Theory]
    [MemberData(nameof(IdSamples))]
    public void Parse_Should_RoundTripValue(Type idType, string parseInput)
    {
        var id = Parse(idType, parseInput);

        GetValue(id).Should().Be(PrimitiveValue(idType, parseInput));
    }

    [Theory]
    [MemberData(nameof(InvalidIdSamples))]
    public void Parse_Should_ThrowFormatException_ForInvalidInput(Type idType, string invalidInput)
    {
        // 通过反射调用，真实异常会被 TargetInvocationException 包裹，内层才是 FormatException
        var act = () => Parse(idType, invalidInput);

        act.Should().Throw<TargetInvocationException>().WithInnerException<FormatException>();
    }

    /// <summary>
    /// C1：char 基元的解析按「长度恰为 1」判定——空串与多字符都必须失败，string 与 span 两个重载一致。
    /// </summary>
    [Fact]
    public void CharId_Should_RejectTextWhoseLengthIsNotOne()
    {
        CharId.TryParse("", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        CharId.TryParse("ab", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        CharId.TryParse("a".AsSpan(), CultureInfo.InvariantCulture, out var fromSpan).Should().BeTrue();
        fromSpan.Value.Should().Be('a');

        CharId.Parse("z", CultureInfo.InvariantCulture).Value.Should().Be('z');
        CharId.Create('q').ToString().Should().Be("q");
        // IFormattable 入口：char 的实现是显式的，生成代码经接口调用（多一次装箱），结果仍是单字符。
        CharId.Create('q').ToString(null, CultureInfo.InvariantCulture).Should().Be("q");
    }

    #endregion

    #region TryParse

    [Theory]
    [MemberData(nameof(IdSamples))]
    public void TryParse_Should_ReturnTrue_ForValidInput(Type idType, string parseInput)
    {
        var ok = TryParse(idType, parseInput, out var result);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
        GetValue(result!).Should().Be(PrimitiveValue(idType, parseInput));
    }

    [Theory]
    [MemberData(nameof(IdTypes))]
    public void TryParse_Should_ReturnFalse_ForNull(Type idType)
    {
        var ok = TryParse(idType, null, out var result);

        ok.Should().BeFalse();
        // 失败时 out 参数被置为 default(TSelf)：值类型（record struct）为零值结构体，引用类型（record）为 null
        result.Should().Be(DefaultOf(idType));
    }

    [Theory]
    [MemberData(nameof(InvalidIdSamples))]
    public void TryParse_Should_ReturnFalse_ForInvalidInput(Type idType, string invalidInput)
    {
        var ok = TryParse(idType, invalidInput, out var result);

        ok.Should().BeFalse();
        result.Should().Be(DefaultOf(idType));
    }

    #endregion

    #region 相等性

    [Theory]
    [MemberData(nameof(IdSamples))]
    public void Equality_Should_BeReflexiveAndTypeAware(Type idType, string parseInput)
    {
        var value = PrimitiveValue(idType, parseInput);
        var a = Create(idType, value);
        var b = Create(idType, value);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
        // 强类型 Id 与底层基元类型不相等（类型安全）
        a.Equals(value).Should().BeFalse();
        a.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void EqualityOperators_Should_DistinguishDifferentValues()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var a = GuidId.Create(g1);
        var b = GuidId.Create(g2);

        (a == b).Should().BeFalse();
        (a != b).Should().BeTrue();
        a.Equals(b).Should().BeFalse();
        a.GetHashCode().Should().NotBe(b.GetHashCode());
    }

    [Fact]
    public void EqualityOperators_Should_MatchForEqualValues()
    {
        var g = Guid.NewGuid();
        var a = GuidId.Create(g);
        var b = GuidId.Create(g);

        (a == b).Should().BeTrue();
        (a != b).Should().BeFalse();
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_Should_ContainValue()
    {
        GuidId.Create(Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3")).ToString()
            .Should().Contain("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        StringId.Create("Len").ToString().Should().Contain("Len");
        Int32Id.Create(12).ToString().Should().Contain("12");
        SByteId.Create((sbyte)12).ToString().Should().Contain("12");
        Int16Id.Create((short)1234).ToString().Should().Contain("1234");
        UInt16Id.Create((ushort)1234).ToString().Should().Contain("1234");
        StringIdV3.Create("V3").ToString().Should().Contain("V3");
    }

    #endregion

    #region C10 文本前缀

    // 前缀进入规范文本形：ToString 前置、Parse/TryParse 剥离。宽松（默认）下前缀可选，严格下必须存在。

    [Fact]
    public void Prefix_ToString_Should_PrependPrefix()
    {
        var g = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");

        PrefixedGuidId.Create(g).ToString().Should().Be("ord_d8ac85d4-ed76-4974-b055-8ef3508743f3");
        PrefixedStringId.Create("x").ToString().Should().Be("ord_x");
    }

    [Fact]
    public void Prefix_Parse_Should_StripPrefix_ForLenient()
    {
        var g = Guid.NewGuid();

        PrefixedGuidId.Parse("ord_" + g.ToString("D"), null).Value.Should().Be(g);
        PrefixedStringId.Parse("ord_x", null).Value.Should().Be("x");
    }

    [Fact]
    public void Prefix_Parse_Should_AcceptBareValue_ForLenient()
    {
        // 宽松读入：前缀可选，裸值同样可解析（迁移期友好）。
        var g = Guid.NewGuid();

        PrefixedGuidId.Parse(g.ToString("D"), null).Value.Should().Be(g);
        PrefixedStringId.Parse("x", null).Value.Should().Be("x");
    }

    /// <summary>
    /// N3：配置 <c>ExceptionType</c> 后，<c>Create</c> 的验证失败抛该异常而不是 <see cref="ArgumentException"/>。
    /// </summary>
    [Fact]
    public void Create_Should_ThrowConfiguredException_WhenValidatorRejects()
    {
        var act = () => DomainValidatedGuidId.Create(Guid.Empty);

        act.Should().Throw<DomainRuleViolationException>()
            .WithMessage("*not a valid DomainValidatedGuidId*");

        DomainValidatedGuidId.Create(Guid.NewGuid()).Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// 自定义异常只作用于 <c>Create</c>：<c>Parse</c> 的失败还包含「文本不是 Id」，仍按 <see cref="IParsable{TSelf}"/>
    /// 的惯例抛 <see cref="FormatException"/>。
    /// </summary>
    [Fact]
    public void Parse_Should_ThrowFormatException_WhenExceptionTypeIsConfigured()
    {
        var parse = () => DomainValidatedGuidId.Parse(Guid.Empty.ToString("D"), null);

        parse.Should().Throw<FormatException>();
    }

    [Fact]
    public void Prefix_Parse_Should_RequirePrefix_ForStrict()
    {
        var g = Guid.NewGuid();

        StrictPrefixedGuidId.Parse("ord_" + g.ToString("D"), null).Value.Should().Be(g);

        var act = () => StrictPrefixedGuidId.Parse(g.ToString("D"), null);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Prefix_TryParse_Should_HandleBothForms_ForLenient()
    {
        var g = Guid.NewGuid();

        PrefixedGuidId.TryParse("ord_" + g.ToString("D"), null, out var prefixed).Should().BeTrue();
        prefixed.Value.Should().Be(g);

        PrefixedGuidId.TryParse(g.ToString("D"), null, out var bare).Should().BeTrue();
        bare.Value.Should().Be(g);
    }

    [Fact]
    public void Prefix_TryParse_Should_ReturnFalse_ForStrictMissingPrefix()
    {
        var g = Guid.NewGuid();

        StrictPrefixedGuidId.TryParse(g.ToString("D"), null, out var missing).Should().BeFalse();
        missing.Should().Be(default);

        StrictPrefixedGuidId.TryParse("ord_" + g.ToString("D"), null, out var ok).Should().BeTrue();
        ok.Value.Should().Be(g);
    }

    [Fact]
    public void Prefix_TryParse_Should_ReturnFalse_ForWrongPrefix()
    {
        var g = Guid.NewGuid();

        // 前缀不匹配：宽松只剥离「恰好匹配」的前缀，随后按基元解析失败；严格直接失败。
        PrefixedGuidId.TryParse("xyz_" + g.ToString("D"), null, out _).Should().BeFalse();
        StrictPrefixedGuidId.TryParse("xyz_" + g.ToString("D"), null, out _).Should().BeFalse();
    }

    [Fact]
    public void Prefix_ToString_Should_RoundTripThroughParse()
    {
        var g = Guid.NewGuid();
        var id = PrefixedGuidId.Create(g);

        PrefixedGuidId.Parse(id.ToString(), null).Should().Be(id);
    }

    [Fact]
    public void Prefix_Formattable_Should_IncludePrefix_WhenFormatIsNullOrEmpty()
    {
        var g = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var id = PrefixedGuidId.Create(g);

        // format 为空：$"{id}" / string.Format / 结构化日志走 IFormattable，必须带前缀。
        ((IFormattable)id).ToString(null, null).Should().Be("ord_d8ac85d4-ed76-4974-b055-8ef3508743f3");
        ((IFormattable)id).ToString(string.Empty, null).Should().Be("ord_d8ac85d4-ed76-4974-b055-8ef3508743f3");

        // format 非空：前缀会污染 format 语义，故保持裸基元。
        ((IFormattable)id).ToString("N", null).Should().Be(g.ToString("N", null));
    }

    [Fact]
    public void Prefix_TryFormat_Should_WritePrefix_WhenFormatIsEmpty()
    {
        var g = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var id = PrefixedGuidId.Create(g);

        Span<char> destination = stackalloc char[64];

        id.TryFormat(destination, out var charsWritten, default, null).Should().BeTrue();
        destination[..charsWritten].ToString().Should().Be("ord_d8ac85d4-ed76-4974-b055-8ef3508743f3");

        // format 非空时保持裸基元。
        id.TryFormat(destination, out var bareWritten, "N", null).Should().BeTrue();
        destination[..bareWritten].ToString().Should().Be(g.ToString("N", null));
    }

    [Fact]
    public void Prefix_TryFormatUtf8_Should_WritePrefix_WhenFormatIsEmpty()
    {
        var g = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var id = PrefixedGuidId.Create(g);

        Span<byte> destination = stackalloc byte[64];

        id.TryFormat(destination, out var bytesWritten, default, null).Should().BeTrue();
        Encoding.UTF8.GetString(destination[..bytesWritten]).Should().Be("ord_d8ac85d4-ed76-4974-b055-8ef3508743f3");
    }

    [Fact]
    public void Prefix_Validated_Should_ReceiveStrippedValue()
    {
        // 前缀 + 验证器：验证器收到的是剥离后的基元值。
        var g = Guid.NewGuid();

        PrefixedValidatedGuidId.TryParse("ord_" + g.ToString("D"), null, out var ok).Should().BeTrue();
        ok.Value.Should().Be(g);

        // 剥离后是 Guid.Empty，验证器拒绝。
        PrefixedValidatedGuidId.TryParse("ord_" + Guid.Empty.ToString("D"), null, out _).Should().BeFalse();
    }

    #endregion

    #region StronglyTypedIdExtensions 边界分支

    // NotStronglyTypedId 带有 [StronglyTypedId] 但没有生成接口实现（无 Value 主键）
    [Fact]
    public void IsStronglyTypedId_Should_ReturnFalse_ForPlainRecordWithoutGeneratedInterface()
        => typeof(NotStronglyTypedId).IsStronglyTypedId().Should().BeFalse();

    [Fact]
    public void TryGetPrimitiveIdType_Should_ReturnFalse_ForEnum() => typeof(DayOfWeek).TryGetPrimitiveIdType(out _).Should().BeFalse();

    [Fact]
    public void TryGetPrimitiveIdType_Should_ReturnFalse_ForArray() => typeof(int[]).TryGetPrimitiveIdType(out _).Should().BeFalse();

    [Fact]
    public void GetPrimitiveIdType_Should_ReturnNull_ForPlainRecord() => typeof(NotStronglyTypedId).GetPrimitiveIdType().Should().BeNull();

    #endregion

    #region 反射辅助方法

    // 把样例的字符串形式还原为底层基元值（Guid / 整型 / 字符串）。
    // 用例据此拿到真实基元值，而无需把不可序列化的 object 放进测试数据行。
    private static object PrimitiveValue(Type idType, string text)
    {
        var primitiveType = idType.GetPrimitiveIdType()!;

        return primitiveType == typeof(Guid) ? Guid.Parse(text)
            : primitiveType == typeof(string) ? text
            : Convert.ChangeType(text, primitiveType, CultureInfo.InvariantCulture);
    }

    private static object Create(Type idType, object value)
    {
        var method = idType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Create 在 {idType.Name} 上未找到");

        return method.Invoke(null, [value])!;
    }

    private static object Parse(Type idType, string value)
    {
        var method = idType.GetMethod("Parse", [typeof(string), typeof(IFormatProvider)])
            ?? throw new InvalidOperationException($"Parse 在 {idType.Name} 上未找到");

        return method.Invoke(null, [value, null])!;
    }

    private static bool TryParse(Type idType, string? value, [NotNullWhen(true)] out object? result)
    {
        var method = idType.GetMethod("TryParse", [typeof(string), typeof(IFormatProvider), idType.MakeByRefType()])
            ?? throw new InvalidOperationException($"TryParse 在 {idType.Name} 上未找到");

        var args = new object?[] { value, null, null };
        var ok = (bool)method.Invoke(null, args)!;
        result = args[2];

        return ok;
    }

    private static object? DefaultOf(Type idType)
        => idType.IsValueType ? Activator.CreateInstance(idType) : null;

    // 编译期契约断言：TSelf 必须实现 IStronglyTypedId<TSelf, TPrimitiveId>，否则此处无法通过编译。
    // 用泛型约束替代「声明为接口类型的局部变量」——同为编译期校验，但不触发 CA1859。
    private static TSelf AsStronglyTypedId<TSelf, TPrimitiveId>(TSelf self)
        where TSelf : IStronglyTypedId<TSelf, TPrimitiveId>
        where TPrimitiveId : notnull, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>
        => self;

    // 通过 dynamic 读取生成属性 Value，再以静态 object 返回，
    // 以便 FluentAssertions 的扩展方法 Should() 能在静态类型上正常解析。
    private static object GetValue(object id) => ((dynamic)id).Value;

    #endregion
}
