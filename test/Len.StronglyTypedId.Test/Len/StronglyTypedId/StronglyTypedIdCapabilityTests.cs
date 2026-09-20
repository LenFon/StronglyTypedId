using System.ComponentModel;
using FluentAssertions;
using System.Globalization;

namespace Len.StronglyTypedId;

/// <summary>
/// 强类型 Id 在「排序、格式化、span 解析、字典键、取值校验」上的能力契约。
/// </summary>
/// <remarks>
/// <para>
/// 这些能力都是基元类型本来就有的，本项目只是把它们透出到 Id 上，因此断言一律<b>以基元类型的行为为参照</b>：
/// 例如排序结果要对得上 <c>Guid.CompareTo</c>、格式化结果要对得上 <c>int.ToString(format, provider)</c>。
/// 这样用例守的是「透出没有走样」，而不是把某一版实现的输出抄一遍。
/// </para>
/// <para>
/// 字典键部分按实测行为书写：System.Text.Json 走 <c>ReadAsPropertyName</c> / <c>WriteAsPropertyName</c>，
/// 重写前对非内建键类型一律抛 <c>NotSupportedException</c>；Newtonsoft 则完全不经过 <c>JsonConverter</c>。
/// </para>
/// </remarks>
public class StronglyTypedIdCapabilityTests
{
    private static readonly Guid LowGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid HighGuid = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    #region 排序与比较

    [Fact]
    public void ComparisonOperators_Should_FollowPrimitiveOrder()
    {
        var lower = GuidId.Create(LowGuid);
        var higher = GuidId.Create(HighGuid);
        // 等值边界用另一个变量表示：直接写 lower < lower 会命中 CS1718（对同一变量比较）。
        var equalToLower = GuidId.Create(LowGuid);

        (lower < higher).Should().BeTrue();
        (higher > lower).Should().BeTrue();
        (lower <= higher).Should().BeTrue();
        (higher >= lower).Should().BeTrue();
        (lower < equalToLower).Should().BeFalse();
        (lower > equalToLower).Should().BeFalse();
        (lower <= equalToLower).Should().BeTrue();
        (lower >= equalToLower).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_Should_AgreeWithPrimitiveComparison()
    {
        var lower = GuidId.Create(LowGuid);
        var higher = GuidId.Create(HighGuid);

        lower.CompareTo(higher).Should().Be(LowGuid.CompareTo(HighGuid));
        higher.CompareTo(lower).Should().Be(HighGuid.CompareTo(LowGuid));
        lower.CompareTo(GuidId.Create(LowGuid)).Should().Be(0);
    }

    /// <summary>
    /// 排序不再需要手写 <c>IComparer&lt;T&gt;</c>：生成的 <c>IComparable&lt;TSelf&gt;</c> 就是默认比较器。
    /// </summary>
    [Fact]
    public void Ordering_Should_UseGeneratedComparable_WithoutCustomComparer()
    {
        var ids = new[] { Int32Id.Create(3), Int32Id.Create(1), Int32Id.Create(2) };

        ids.OrderBy(id => id).Select(id => id.Value).Should().Equal(1, 2, 3);

        var set = new SortedSet<Int32Id> { Int32Id.Create(3), Int32Id.Create(1), Int32Id.Create(2) };

        set.Select(id => id.Value).Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// <c>IComparable&lt;TSelf&gt;</c> 的形参在引用类型上是可空的，空值按约定排在有序序列末尾。
    /// </summary>
    /// <remarks>
    /// 这里直接调用 <c>CompareTo</c> 而不经 <c>Comparer&lt;T&gt;.Default</c>：后者会先自行处理空值，
    /// 从而绕开被测的那条分支。
    /// </remarks>
    [Fact]
    public void CompareTo_Should_TreatNullAsLast_ForReferenceTypeId()
    {
        GuidIdV2? nullId = null;

        GuidIdV2.Create(LowGuid).CompareTo(nullId).Should().BePositive();
    }

    #endregion

    #region 文本形态与格式化

    [Fact]
    public void ToString_Should_ReturnPrimitiveText_NotRecordPrintout()
    {
        GuidId.Create(LowGuid).ToString().Should().Be(LowGuid.ToString());
        Int32Id.Create(12).ToString().Should().Be("12");
        StringId.Create("Len").ToString().Should().Be("Len");
    }

    [Fact]
    public void IFormattable_Should_ForwardFormatAndProvider()
    {
        var id = Int32Id.Create(255);

        id.ToString("X4", CultureInfo.InvariantCulture).Should().Be("00FF");
        ((IFormattable)id).ToString("D8", CultureInfo.InvariantCulture).Should().Be("00000255");
    }

    [Fact]
    public void ISpanFormattable_Should_WriteIntoDestinationWithoutIntermediateString()
    {
        var id = Int32Id.Create(255);
        Span<char> destination = stackalloc char[8];

        id.TryFormat(destination, out var charsWritten, "X4", CultureInfo.InvariantCulture).Should().BeTrue();
        destination[..charsWritten].ToString().Should().Be("00FF");
    }

    /// <summary>
    /// 格式化接口按基元类型条件生成：<c>string</c> 未实现 <c>IFormattable</c>，因此 string 基元的 Id 也不实现它。
    /// </summary>
    /// <remarks>
    /// 解析接口用非泛型 <c>BeAssignableTo(Type)</c> 断言：<c>IParsable&lt;T&gt;</c> / <c>ISpanParsable&lt;T&gt;</c> 带静态抽象成员，
    /// 作泛型实参会被 CS8920 拒绝。
    /// </remarks>
    [Fact]
    public void FormattingInterfaces_Should_BeEmittedOnlyWhenPrimitiveSupportsThem()
    {
        typeof(Int32Id).Should().BeAssignableTo<IFormattable>();
        typeof(Int32Id).Should().BeAssignableTo<ISpanFormattable>();
        typeof(GuidId).Should().BeAssignableTo<IFormattable>();

        typeof(StringId).Should().NotBeAssignableTo<IFormattable>();
        typeof(Int32Id).Should().BeAssignableTo(typeof(IParsable<Int32Id>));
        typeof(Int32Id).Should().BeAssignableTo(typeof(ISpanParsable<Int32Id>));
        typeof(StringId).Should().BeAssignableTo(typeof(IParsable<StringId>));
    }

    [Fact]
    public void SpanParse_Should_AcceptSpanInput()
    {
        Int32Id.Parse("42".AsSpan(), CultureInfo.InvariantCulture).Value.Should().Be(42);
        Int32Id.TryParse("42".AsSpan(), CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
        parsed.Value.Should().Be(42);
        Int32Id.TryParse("not-a-number".AsSpan(), CultureInfo.InvariantCulture, out _).Should().BeFalse();

        StringId.Parse("Len".AsSpan(), CultureInfo.InvariantCulture).Value.Should().Be("Len");
        StringId.TryParse(ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }

    #endregion

    #region System.Text.Json：强类型 Id 作字典键

    private static string SerializeDictionary<TId>(TId key, int value)
        where TId : notnull
        => System.Text.Json.JsonSerializer.Serialize(new Dictionary<TId, int> { [key] = value });

    private static TId DeserializeDictionary<TId>(string json)
        where TId : notnull
        => System.Text.Json.JsonSerializer.Deserialize<Dictionary<TId, int>>(json)!.Keys.Single();

    [Fact]
    public void SystemTextJson_Should_RoundTripGuidKeyedDictionary()
    {
        var json = SerializeDictionary(GuidId.Create(LowGuid), 1);

        json.Should().Be($$"""{"{{LowGuid}}":1}""");
        DeserializeDictionary<GuidId>(json).Should().Be(GuidId.Create(LowGuid));
    }

    [Fact]
    public void SystemTextJson_Should_RoundTripInt32KeyedDictionary()
    {
        var json = SerializeDictionary(Int32Id.Create(42), 1);

        json.Should().Be("""{"42":1}""");
        DeserializeDictionary<Int32Id>(json).Should().Be(Int32Id.Create(42));
    }

    /// <summary>
    /// <c>string</c> 基元的属性名就是取值本身，因此不需要解析、也不受「非空串」解析约束。
    /// </summary>
    [Fact]
    public void SystemTextJson_Should_RoundTripStringKeyedDictionary()
    {
        var json = SerializeDictionary(StringId.Create("abc"), 1);

        json.Should().Be("""{"abc":1}""");
        DeserializeDictionary<StringId>(json).Should().Be(StringId.Create("abc"));
    }

    [Fact]
    public void SystemTextJson_Should_RoundTripReferenceTypeIdKeyedDictionary()
    {
        var json = SerializeDictionary(GuidIdV2.Create(LowGuid), 1);

        json.Should().Be($$"""{"{{LowGuid}}":1}""");
        DeserializeDictionary<GuidIdV2>(json).Should().Be(GuidIdV2.Create(LowGuid));
    }

    [Fact]
    public void SystemTextJson_Should_ThrowJsonException_WhenPropertyNameIsNotParseable()
    {
        var deserialize = () => System.Text.Json.JsonSerializer.Deserialize<Dictionary<Int32Id, int>>("""{"abc":1}""");

        deserialize.Should().Throw<System.Text.Json.JsonException>();
    }

    #endregion

    #region Newtonsoft.Json：字典键的写路径

    /// <summary>
    /// Newtonsoft 写字典键时取的是键类型的 <c>ToString()</c>，因此键名必须是基元文本而不是 record 的默认打印串。
    /// </summary>
    /// <remarks>
    /// 读路径不经 <c>JsonConverter</c>：Newtonsoft 用 <c>TypeDescriptor</c> 把键文本还原成键类型，
    /// 即使重写了 <c>ReadJson</c> 也接管不到（实测抛
    /// <c>Could not convert string … Create a TypeConverter to convert from the string to the key type object</c>）。
    /// 要补齐得给 Id 类型标 <c>[TypeConverter]</c> —— 那会改变类型的全局语义，且与使用者已自标的同名 attribute
    /// 直接冲突，因此本项目不自动生成；本用例只锁定「写出来的键名是基元文本」这一半。
    /// </remarks>
    [Fact]
    public void NewtonsoftJson_Should_WritePrimitiveTextAsDictionaryKey()
    {
        Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<GuidId, int> { [GuidId.Create(LowGuid)] = 1 })
            .Should().Be($$"""{"{{LowGuid}}":1}""");

        Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<Int32Id, int> { [Int32Id.Create(42)] = 1 })
            .Should().Be("""{"42":1}""");
    }

    #endregion

    #region 取值校验（Validator 钩子）

    [Fact]
    public void Validator_Should_RejectInvalidValue_OnCreate()
    {
        var create = () => NonEmptyGuidId.Create(Guid.Empty);

        create.Should().Throw<ArgumentException>()
            .WithParameterName("value")
            .Which.Message.Should().Contain(nameof(NonEmptyGuidId));
    }

    [Fact]
    public void Validator_Should_AcceptValidValue_OnCreate()
    {
        NonEmptyGuidId.Create(LowGuid).Value.Should().Be(LowGuid);
        NonEmptyInt32Id.Create(1).Value.Should().Be(1);
    }

    /// <summary>
    /// <c>TryParse</c> 的契约是「非法即返回 false」，校验因此并入解析条件而不是借抛异常实现。
    /// </summary>
    [Fact]
    public void Validator_Should_MakeTryParseReturnFalse_InsteadOfThrowing()
    {
        NonEmptyGuidId.TryParse(Guid.Empty.ToString(), CultureInfo.InvariantCulture, out var result).Should().BeFalse();
        result.Should().Be(default(NonEmptyGuidId));

        NonEmptyGuidId.TryParse(LowGuid.ToString(), CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
        parsed.Value.Should().Be(LowGuid);

        NonEmptyInt32Id.TryParse("0", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        NonEmptyInt32Id.TryParse("-1", CultureInfo.InvariantCulture, out _).Should().BeFalse();
        NonEmptyInt32Id.TryParse("1", CultureInfo.InvariantCulture, out _).Should().BeTrue();
    }

    [Fact]
    public void Validator_Should_MakeParseThrow_ForInvalidValue()
    {
        var parse = () => NonEmptyGuidId.Parse(Guid.Empty.ToString(), CultureInfo.InvariantCulture);

        parse.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// <c>string</c> 基元的 span 重载先转成字符串再交给验证器，覆盖校验参数由 span 派生的那条分支。
    /// </summary>
    [Fact]
    public void Validator_Should_ApplyToSpanParsing_ForStringPrimitive()
    {
        ShortStringId.TryParse("12345678".AsSpan(), CultureInfo.InvariantCulture, out _).Should().BeTrue();
        ShortStringId.TryParse("123456789".AsSpan(), CultureInfo.InvariantCulture, out _).Should().BeFalse();
        ShortStringId.TryParse(ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }

    /// <summary>
    /// 生成代码把 JSON 与 Entity Framework Core 的构造入口一并收敛到 <c>Create</c>，
    /// 校验因此也覆盖反序列化路径。
    /// </summary>
    [Fact]
    public void Validator_Should_BeAppliedByJsonDeserialization()
    {
        var systemTextJson = () => System.Text.Json.JsonSerializer.Deserialize<NonEmptyGuidId>($"\"{Guid.Empty}\"");
        var newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<NonEmptyGuidId>($"\"{Guid.Empty}\"");

        systemTextJson.Should().Throw<ArgumentException>();
        newtonsoft.Should().Throw<ArgumentException>();

        System.Text.Json.JsonSerializer.Deserialize<NonEmptyGuidId>($"\"{LowGuid}\"").Value.Should().Be(LowGuid);
        Newtonsoft.Json.JsonConvert.DeserializeObject<NonEmptyGuidId>($"\"{LowGuid}\"")!.Value.Should().Be(LowGuid);
    }

    #endregion

    #region 嵌套类型

    [Fact]
    public void NestedId_Should_DeclareGeneratedMembers_OnNestedType()
    {
        // 关键判据：生成代码必须把 partial 段嵌回容器里。若它落在命名空间层级，会另起一个同名顶层类型，
        // 整份代码照样编译通过 —— 只有「从容器里取这个类型」并要求它具备生成成员，才能发现区别。
        typeof(OrderAggregate).GetNestedType(nameof(OrderAggregate.OrderId)).Should().Be(typeof(OrderAggregate.OrderId));
        typeof(ShipmentBatch).GetNestedType(nameof(ShipmentBatch.BatchId)).Should().Be(typeof(ShipmentBatch.BatchId));
        typeof(Inventory).GetNestedType(nameof(Inventory.SkuId)).Should().Be(typeof(Inventory.SkuId));

        var value = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var orderId = OrderAggregate.OrderId.Create(value);

        orderId.Value.Should().Be(value);
        // 以 Type 重载断言可赋值：IStronglyTypedId 带静态抽象成员，把它本身当类型参数用会 CS8920。
        typeof(OrderAggregate.OrderId).Should().BeAssignableTo(typeof(IStronglyTypedId<OrderAggregate.OrderId, Guid>));
        OrderAggregate.OrderId.TryParse(orderId.ToString(), null, out var parsed).Should().BeTrue();
        parsed.Should().Be(orderId);
    }

    [Fact]
    public void NestedId_Should_TransferThroughBothJsonLibraries()
    {
        var value = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        const string Json = "\"d8ac85d4-ed76-4974-b055-8ef3508743f3\"";

        var orderId = OrderAggregate.OrderId.Create(value);
        var batchId = ShipmentBatch.BatchId.Create(value);
        var skuId = Inventory.SkuId.Create("SKU-1");

        System.Text.Json.JsonSerializer.Serialize(orderId).Should().Be(Json);
        System.Text.Json.JsonSerializer.Deserialize<OrderAggregate.OrderId>(Json).Should().Be(orderId);
        Newtonsoft.Json.JsonConvert.SerializeObject(orderId).Should().Be(Json);
        Newtonsoft.Json.JsonConvert.DeserializeObject<OrderAggregate.OrderId>(Json).Should().Be(orderId);

        // 结构容器与 record 容器里的 Id 走同一条路径（容器种类只影响生成代码如何重开容器）。
        System.Text.Json.JsonSerializer.Serialize(batchId).Should().Be(Json);
        System.Text.Json.JsonSerializer.Deserialize<ShipmentBatch.BatchId>(Json).Should().Be(batchId);

        // string 基元的嵌套 Id：解析与序列化都直接以取值本身为准。
        System.Text.Json.JsonSerializer.Serialize(skuId).Should().Be("\"SKU-1\"");
        System.Text.Json.JsonSerializer.Deserialize<Inventory.SkuId>("\"SKU-1\"").Should().Be(skuId);
        Newtonsoft.Json.JsonConvert.DeserializeObject<Inventory.SkuId>("\"SKU-1\"").Should().Be(skuId);
    }

    [Fact]
    public void NestedId_Should_CompareAndFormat_LikeItsPrimitive()
    {
        var low = OrderAggregate.OrderId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var high = OrderAggregate.OrderId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        // 等值边界用另一个变量表示：直接写 low <= low 会命中 CS1718（对同一变量比较）。
        var equalToLow = OrderAggregate.OrderId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        (low < high).Should().BeTrue();
        (high > low).Should().BeTrue();
        (low <= equalToLow).Should().BeTrue();
        low.CompareTo(high).Should().BeLessThan(0);
        low.ToString().Should().Be("00000000-0000-0000-0000-000000000001");
        ((IFormattable)low).ToString("N", CultureInfo.InvariantCulture)
            .Should().Be(low.Value.ToString("N", CultureInfo.InvariantCulture));
    }

    #endregion

    #region 工厂方法 TryCreate / UTF-8 接口 / 类型转换器 / 只读结构

    /// <summary>
    /// <c>TryCreate</c> 是 <c>Create</c> 的非抛异常版本：合法取值直接返回目标 Id。
    /// </summary>
    [Fact]
    public void TryCreate_Should_ReturnTrue_ForValidValue()
    {
        GuidId.TryCreate(LowGuid, out var guidId).Should().BeTrue();
        guidId.Should().Be(GuidId.Create(LowGuid));

        Int32Id.TryCreate(42, out var intId).Should().BeTrue();
        intId.Should().Be(Int32Id.Create(42));
    }

    /// <summary>
    /// <c>TryCreate</c> 经由验证器拒绝非法取值：仅当 Id 设了 <c>Validator</c> 时才会失败。
    /// </summary>
    [Fact]
    public void TryCreate_Should_ReturnFalse_WhenValidatorRejects()
    {
        // 值类型 Id：TryCreate 失败时 result 为 default 值（零结构体）。
        NonEmptyGuidId.TryCreate(Guid.Empty, out var emptyId).Should().BeFalse();
        emptyId.Should().Be(default(NonEmptyGuidId));

        // 引用类型 Id：TryCreate 失败时 result 为 null（default 引用）。
        NonEmptyInt32Id.TryCreate(0, out var zeroId).Should().BeFalse();
        (zeroId is null).Should().BeTrue();

        // 无验证器的 Id：任何取值都合法，TryCreate 恒返回 true。
        GuidId.TryCreate(Guid.Empty, out var anyId).Should().BeTrue();
        anyId.Should().Be(GuidId.Create(Guid.Empty));
    }

    /// <summary>
    /// <c>IUtf8SpanFormattable</c> 自 .NET 8 起即可用，两个框架的 Guid / Int32 都实现，故生成的 Id 恒实现。
    /// </summary>
    [Fact]
    public void Utf8Formattable_Should_BeEmitted_WhenPrimitiveSupportsIt()
    {
        typeof(GuidId).Should().BeAssignableTo<IUtf8SpanFormattable>();
        typeof(Int32Id).Should().BeAssignableTo<IUtf8SpanFormattable>();
    }

#if NET10_0_OR_GREATER
    /// <summary>
    /// <c>Guid</c> 自 .NET 10 起实现 <c>IUtf8SpanParsable&lt;Guid&gt;</c>，生成器据此为
    /// <c>GuidId</c> 实现 <c>IUtf8SpanParsable&lt;GuidId&gt;</c> 并提供 UTF-8 的 <c>Parse</c> / <c>TryParse</c>。
    /// 该接口仅 net10.0+ 可用，用例以编译期 <c>#if</c> 门控避免 net8.0 约束违例（CS0315）。
    /// </summary>
    [Fact]
    public void Utf8Parsable_Should_BeEmitted_WhenPrimitiveSupportsIt()
    {
        typeof(IUtf8SpanParsable<GuidId>).IsAssignableFrom(typeof(GuidId)).Should().BeTrue();

        var utf8 = System.Text.Encoding.UTF8.GetBytes(LowGuid.ToString());
        GuidId.Parse(utf8.AsSpan(), CultureInfo.InvariantCulture).Should().Be(GuidId.Create(LowGuid));
        GuidId.TryParse(utf8.AsSpan(), CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
        parsed.Should().Be(GuidId.Create(LowGuid));
        GuidId.TryParse(
                System.Text.Encoding.UTF8.GetBytes("not-a-guid").AsSpan(),
                CultureInfo.InvariantCulture,
                out _)
            .Should().BeFalse();
    }
#endif

    /// <summary>
    /// 逐类型开启 <c>TypeConverter</c> 后，<c>TypeDescriptor</c> 能在字符串与强类型 Id 间往返。
    /// </summary>
    /// <remarks>
    /// 生成的类型必须自带 <c>[TypeConverter(typeof(...))]</c>，且转换器自身是 public sealed 嵌套类、
    /// 带无参构造函数，<c>Activator.CreateInstance</c> 可达。
    /// </remarks>
    [Fact]
    public void TypeConverter_Should_RoundTripViaTypeDescriptor()
    {
        var attribute = (TypeConverterAttribute)Attribute.GetCustomAttribute(
            typeof(ConvertibleGuidId), typeof(TypeConverterAttribute))!;
        attribute.ConverterTypeName.Should().Contain("ConvertibleGuidIdTypeConverter");

        var converter = TypeDescriptor.GetConverter(typeof(ConvertibleGuidId));
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
        converter.CanConvertTo(typeof(string)).Should().BeTrue();

        var id = (ConvertibleGuidId)converter.ConvertFrom(LowGuid.ToString())!;
        id.Should().Be(ConvertibleGuidId.Create(LowGuid));

        converter.ConvertTo(id, typeof(string)).Should().Be(LowGuid.ToString());
    }

    /// <summary>
    /// 回归用例：开启 <c>TypeConverter</c> 后，Newtonsoft.Json 字典键的<b>读</b>路径也通了。
    /// </summary>
    /// <remarks>
    /// Newtonsoft 还原字典键文本走 <c>TypeDescriptor</c> 而非 <c>JsonConverter.ReadJson</c>，
    /// 因此只有类型自带转换器时键才能被还原。未开启 TypeConverter 的 <c>GuidId</c> 经由同一路径会失败，
    /// 见 <see cref="StronglyTypedIdCapabilityTests"/> 中 Newtonsoft 写路径用例的注释。
    /// </remarks>
    [Fact]
    public void NewtonsoftJson_Should_RoundTripKeyedDictionary_WhenTypeConverterEnabled()
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(
            new Dictionary<ConvertibleGuidId, int> { [ConvertibleGuidId.Create(LowGuid)] = 1 });

        var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<ConvertibleGuidId, int>>(json);

        parsed.Should().NotBeNull();
        parsed!.Keys.Single().Should().Be(ConvertibleGuidId.Create(LowGuid));
    }

    /// <summary>
    /// <c>readonly record struct</c> 形态的强类型 Id 必须先编译通过：生成器必须把 <c>readonly</c> 修饰符
    /// 透出到生成的 partial 段，否则与使用者写下的 readonly 段对「是否 readonly struct」不一致而编译失败。
    /// </summary>
    [Fact]
    public void ReadonlyRecordStruct_Should_CompileAndBehaveLikeRecordStruct()
    {
        var value = Guid.Parse("d8ac85d4-ed76-4974-b055-8ef3508743f3");
        var id = ReadonlyGuidId.Create(value);

        id.Value.Should().Be(value);
        (id == ReadonlyGuidId.Create(value)).Should().BeTrue();
        ReadonlyGuidId.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var parsed).Should().BeTrue();
        parsed.Should().Be(id);
        id.ToString().Should().Be(value.ToString());
    }

    #endregion
}
