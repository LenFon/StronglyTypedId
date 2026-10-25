using System.Globalization;
using System.Numerics;
using FluentAssertions;
using Newtonsoft.Json;

namespace Len.StronglyTypedId;

/// <summary>
/// 全基元契约矩阵：对每一种受支持的基元类型跑同一组断言，验证「创建 / 规范文本 / 解析 / 相等 / 比较 /
/// 两个 JSON 库往返」六类能力的行为一致。
/// </summary>
/// <remarks>
/// 逐基元的实现分支散落在生成器各处（<c>char</c> 的显式接口实现、<c>Int128</c> 的运行期解析、浮点的最短往返格式），单点用例容易漏支。
/// 矩阵用同一套断言扫过全部基元，新增基元时补一行就能获得同等强度的覆盖。
/// </remarks>
public class PrimitiveContractMatrixTests
{
    private static readonly Dictionary<string, PrimitiveCase> _cases = BuildCases();

    public static TheoryData<string> Labels
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var label in _cases.Keys)
            {
                data.Add(label);
            }

            return data;
        }
    }

    /// <summary>
    /// 矩阵必须覆盖 23 种具名基元 + 枚举（普通 / <c>[Flags]</c> 各一），共 25 项。
    /// </summary>
    [Fact]
    public void Matrix_Should_CoverEveryPrimitive()
    {
        _cases.Count.Should().Be(25);
        _cases.Values.Select(testCase => testCase.PrimitiveType).Distinct().Should().HaveCount(25);
    }

    [Theory]
    [MemberData(nameof(Labels))]
    public void Contract_Should_Hold_ForEveryPrimitive(string label)
    {
        var testCase = _cases[label];
        var id = testCase.Create();

        // 1. Create 原样保留基元值。
        testCase.GetValue(id).Should().Be(testCase.Value, $"{label}：Create 必须原样保留基元值");

        // 2. 规范文本形 = 基元的不变文化文本。
        var text = testCase.ToText(id);
        text.Should().Be(testCase.ExpectedText, $"{label}：ToString 必须是不变文化的基元文本");

        // 3. Parse / TryParse 读回同一个 Id。
        testCase.Parse(text).Should().Be(id, $"{label}：Parse 必须读回 ToString 写出的文本");
        testCase.TryParse(text).Should().BeTrue($"{label}：TryParse 必须接受 ToString 写出的文本");

        // 4. 非法文本：TryParse 返回 false，Parse 抛 FormatException。
        if (testCase.Garbage is not null)
        {
            testCase.TryParse(testCase.Garbage).Should().BeFalse($"{label}：TryParse 必须拒绝 \"{testCase.Garbage}\"");

            var parse = () => testCase.Parse(testCase.Garbage);

            parse.Should().Throw<FormatException>($"{label}：Parse 的失败必须是 FormatException");
        }

        // 5. 相等性与哈希一致性。
        testCase.Equal(id, testCase.Create()).Should().BeTrue($"{label}：相同基元值的两个 Id 必须相等");
        testCase.Hash(id).Should().Be(testCase.Hash(testCase.Create()), $"{label}：相等的 Id 必须有相同哈希");

        var other = testCase.CreateOther();
        testCase.Equal(id, other).Should().BeFalse($"{label}：不同基元值的 Id 必须不相等");

        // 6. 比较与基元比较同向。string 基元例外：生成代码按序数比较，而 IComparable.CompareTo 是
        //    区域性敏感的 —— 拿它当期望值会复刻被修复的行为，永远测不出偏差。
        testCase.Compare(id, id).Should().Be(0, $"{label}：自比较必须为 0");

        var expectedSign = testCase.PrimitiveType == typeof(string)
            ? Math.Sign(string.CompareOrdinal((string)testCase.Value, (string)testCase.OtherValue))
            : Math.Sign(((IComparable)testCase.Value).CompareTo(testCase.OtherValue));

        Math.Sign(testCase.Compare(id, other))
            .Should().Be(expectedSign, $"{label}：比较方向必须与基元一致");

        // 7. System.Text.Json 往返。
        var stjJson = testCase.SerializeStj(id);
        testCase.DeserializeStj(stjJson).Should().Be(id, $"{label}：STJ 往返必须保真（{stjJson}）");

        // 7.1 Id 的写出必须与 STJ 默认写出基元逐字节一致。生成的转换器改成「直写基元」而不再外包给
        // JsonSerializer 之后，这条就是契约的锚点：任何一处直写表达式写错，输出就会与 STJ 默认分道扬镳。
        // BigInteger 例外：STJ 没有它的内置转换器，按值写会退化成 {"IsPowerOfTwo":…,"Sign":1}（读回来是
        // default），故本库固定写规范文本形 —— 见 BigInteger_Should_SerializeAsJsonString_ForSystemTextJson。
        if (testCase.PrimitiveType != typeof(BigInteger))
        {
            testCase.SerializeStj(id)
                .Should().Be(testCase.SerializePrimitiveStj(id), $"{label}：Id 的 STJ 写出必须与 STJ 默认写出基元逐字节一致");
        }

        // 8. Newtonsoft.Json 往返。
        var newtonsoftJson = testCase.SerializeNewtonsoft(id);
        testCase.DeserializeNewtonsoft(newtonsoftJson).Should().Be(id, $"{label}：Newtonsoft 往返必须保真（{newtonsoftJson}）");
    }

    /// <summary>
    /// <c>BigInteger</c> 没有 System.Text.Json 的内置转换器：按值写会退化成
    /// <c>{"IsPowerOfTwo":false,…,"Sign":1}</c>，读回来是 <c>default</c>。故它固定写规范文本形。
    /// </summary>
    [Fact]
    public void BigInteger_Should_SerializeAsJsonString_ForSystemTextJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(BigIntegerId.Create(42));

        json.Should().Be("\"42\"");
        System.Text.Json.JsonSerializer.Deserialize<BigIntegerId>(json).Value.Should().Be(new BigInteger(42));
    }

    /// <summary>
    /// Newtonsoft.Json 的 <c>JsonWriter.WriteValue</c> 不认识这几个类型（没有对应的
    /// <c>PrimitiveTypeCode</c>），按值写会抛 <c>JsonWriterException</c>。故它们固定写规范文本形。
    /// </summary>
    [Fact]
    public void NewtonsoftUnsupportedPrimitives_Should_SerializeAsJsonString()
    {
        JsonConvert.SerializeObject(Int128Id.Create(42)).Should().Be("\"42\"");
        JsonConvert.SerializeObject(UInt128Id.Create(42)).Should().Be("\"42\"");
        JsonConvert.SerializeObject(HalfId.Create((Half)1.5)).Should().Be("\"1.5\"");
        JsonConvert.SerializeObject(DateOnlyId.Create(new DateOnly(2020, 1, 2))).Should().Be("\"01/02/2020\"");
        JsonConvert.SerializeObject(TimeOnlyId.Create(new TimeOnly(3, 4, 0))).Should().Be("\"03:04\"");
    }

    /// <summary>
    /// 逐基元登记一行。期望文本一律手写为字面量，而不是用与实现相同的公式现算 ——
    /// 否则断言会跟着实现一起错。
    /// </summary>
    private static Dictionary<string, PrimitiveCase> BuildCases()
    {
        var cases = new[]
        {
            Case<GuidId, Guid>(
                "Guid",
                Guid.Parse("3f2c1b4e-0000-4000-8000-000000000001"),
                Guid.Parse("3f2c1b4e-0000-4000-8000-000000000002"),
                "3f2c1b4e-0000-4000-8000-000000000001",
                "not-a-guid"),
            // 取值刻意选软连字符对：区域性比较会把 "a\u00ADb" 与 "ab" 判为相等（符号 0），
            // 序数比较给出正号。这样第 6 步的期望值能真正区分两种实现。
            Case<StringId, string>("string", "a\u00ADb", "ab", "a\u00ADb", garbage: null),
            Case<CharId, char>("char", 'A', 'B', "A", "AB"),
            Case<ByteId, byte>("byte", 42, 43, "42", "256"),
            Case<SByteId, sbyte>("sbyte", -42, -41, "-42", "128"),
            Case<Int16Id, short>("short", -1234, -1233, "-1234", "40000"),
            Case<UInt16Id, ushort>("ushort", 1234, 1235, "1234", "70000"),
            Case<Int32Id, int>("int", 42, 43, "42", "not-a-number"),
            Case<UInt32Id, uint>("uint", 42, 43, "42", "-1"),
            Case<Int64Id, long>("long", 42, 43, "42", "not-a-number"),
            Case<UInt64Id, ulong>("ulong", 42, 43, "42", "-1"),
            // Int128 / UInt128 / Half / DateOnly / TimeOnly 没有对应的 Newtonsoft PrimitiveTypeCode，
            // BigInteger 没有 System.Text.Json 的内置转换器 —— 两侧都改走规范文本形，故同样参与往返断言。
            Case<Int128Id, Int128>("Int128", 42, 43, "42", "not-a-number"),
            Case<UInt128Id, UInt128>("UInt128", 42, 43, "42", "-1"),
            Case<DecimalId, decimal>("decimal", 1.5m, 2.5m, "1.5", "not-a-number"),
            Case<HalfId, Half>("Half", (Half)1.5, (Half)2.5, "1.5", "not-a-number"),
            Case<SingleId, float>("float", 1.5f, 2.5f, "1.5", "not-a-number"),
            Case<DoubleId, double>("double", 1.5, 2.5, "1.5", "not-a-number"),
            Case<DateTimeId, DateTime>(
                "DateTime",
                new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc),
                "01/02/2020 03:04:05",
                "not-a-date"),
            Case<DateTimeOffsetId, DateTimeOffset>(
                "DateTimeOffset",
                new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.FromHours(8)),
                new DateTimeOffset(2020, 1, 2, 3, 4, 6, TimeSpan.FromHours(8)),
                "01/02/2020 03:04:05 +08:00",
                "not-a-date"),
            Case<TimeSpanId, TimeSpan>(
                "TimeSpan",
                TimeSpan.FromMinutes(90),
                TimeSpan.FromMinutes(91),
                "01:30:00",
                "not-a-timespan"),
            Case<DateOnlyId, DateOnly>(
                "DateOnly",
                new DateOnly(2020, 1, 2),
                new DateOnly(2020, 1, 3),
                "01/02/2020",
                "not-a-date"),
            Case<TimeOnlyId, TimeOnly>(
                "TimeOnly",
                new TimeOnly(3, 4, 0),
                new TimeOnly(3, 5, 0),
                "03:04",
                "not-a-time"),
            Case<BigIntegerId, BigInteger>("BigInteger", 42, 43, "42", "not-a-number"),
            Case<SampleStatusId, SampleStatus>("enum", SampleStatus.Active, SampleStatus.Suspended, "Active", "999"),
            Case<SampleAccessId, SampleAccess>(
                "[Flags] enum",
                SampleAccess.Read | SampleAccess.Write,
                SampleAccess.Read,
                "Read, Write",
                "999"),
        };

        return cases.ToDictionary(testCase => testCase.Label, StringComparer.Ordinal);
    }

    private static PrimitiveCase Case<TId, TPrimitive>(
        string label,
        TPrimitive value,
        TPrimitive other,
        string expectedText,
        string? garbage)
        where TId : IStronglyTypedId<TId, TPrimitive>, IComparable<TId>
        where TPrimitive : notnull, IComparable
        => new(
            label,
            typeof(TId),
            typeof(TPrimitive),
            value,
            other,
            expectedText,
            garbage,
            () => TId.Create(value),
            () => TId.Create(other),
            id => ((TId)id).Value,
            id => id.ToString()!,
            text => TId.Parse(text, CultureInfo.InvariantCulture),
            text => TId.TryParse(text, CultureInfo.InvariantCulture, out _),
            (left, right) => (TId)left == (TId)right,
            id => id.GetHashCode(),
            (left, right) => ((TId)left).CompareTo((TId)right),
            id => System.Text.Json.JsonSerializer.Serialize((TId)id),
            json => System.Text.Json.JsonSerializer.Deserialize<TId>(json),
            id => System.Text.Json.JsonSerializer.Serialize(((TId)id).Value),
            id => JsonConvert.SerializeObject((TId)id),
            json => JsonConvert.DeserializeObject<TId>(json));

    /// <summary>
    /// 一个基元的全部断言入口。委托在 <see cref="Case{TId, TPrimitive}"/> 里闭包捕获泛型实参，
    /// 于是断言代码本身不必知道具体类型。
    /// </summary>
    private sealed record PrimitiveCase(
        string Label,
        Type IdType,
        Type PrimitiveType,
        object Value,
        object OtherValue,
        string ExpectedText,
        string? Garbage,
        Func<object> Create,
        Func<object> CreateOther,
        Func<object, object> GetValue,
        Func<object, string> ToText,
        Func<string, object> Parse,
        Func<string, bool> TryParse,
        Func<object, object, bool> Equal,
        Func<object, int> Hash,
        Func<object, object, int> Compare,
        Func<object, string> SerializeStj,
        Func<string, object?> DeserializeStj,
        Func<object, string> SerializePrimitiveStj,
        Func<object, string> SerializeNewtonsoft,
        Func<string, object?> DeserializeNewtonsoft);
}
