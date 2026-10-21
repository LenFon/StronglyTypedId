using System.Numerics;

namespace Len.StronglyTypedId.Testing;

/// <summary>
/// Produces plausible values for every primitive type the generator supports.
/// </summary>
/// <remarks>
/// Values are deliberately "ordinary": finite floats, non-empty strings, dates inside 1970–2100.
/// A test fixture that hands out <see cref="double.NaN"/> or <see cref="DateTime.MaxValue"/> mostly
/// produces failures unrelated to the code under test.
/// </remarks>
internal sealed class PrimitiveValueFactory(Random random, int stringLength, Func<Type, object?>? customFactory)
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private static readonly long _minDateTimeTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    private static readonly long _maxDateTimeTicks = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

    /// <summary>
    /// Creates a value for <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The primitive type to produce a value for.</param>
    /// <returns>A value assignable to <paramref name="type"/>.</returns>
    /// <exception cref="NotSupportedException"><paramref name="type"/> is not a supported primitive.</exception>
    public object? Create(Type type)
    {
        if (customFactory?.Invoke(type) is { } customValue)
        {
            return customValue;
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);

            return values.GetValue(random.Next(values.Length));
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => (byte)random.Next(byte.MinValue, byte.MaxValue + 1),
            TypeCode.SByte => (sbyte)random.Next(sbyte.MinValue, sbyte.MaxValue + 1),
            TypeCode.Int16 => (short)random.Next(short.MinValue, short.MaxValue + 1),
            TypeCode.UInt16 => (ushort)random.Next(ushort.MinValue, ushort.MaxValue + 1),
            TypeCode.Int32 => random.Next(),
            TypeCode.UInt32 => (uint)random.NextInt64(uint.MinValue, (long)uint.MaxValue + 1),
            TypeCode.Int64 => random.NextInt64(),
            TypeCode.UInt64 => (ulong)random.NextInt64(),
            TypeCode.Decimal => CreateDecimal(),
            TypeCode.Single => CreateSingle(),
            TypeCode.Double => CreateDouble(),
            TypeCode.DateTime => CreateDateTime(),
            TypeCode.String => CreateString(),
            TypeCode.Char => Alphabet[random.Next(Alphabet.Length)],
            _ => CreateOther(type),
        };
    }

    private object CreateOther(Type type) => type switch
    {
        _ when type == typeof(Guid) => CreateGuid(),
        _ when type == typeof(Int128) => new Int128((ulong)random.NextInt64(), (ulong)random.NextInt64()),
        _ when type == typeof(UInt128) => new UInt128((ulong)random.NextInt64(), (ulong)random.NextInt64()),
        _ when type == typeof(Half) => (Half)CreateSingle(),
        _ when type == typeof(BigInteger) => new BigInteger(CreateBytes(16)),
        _ when type == typeof(DateTimeOffset) => new DateTimeOffset(CreateDateTime()),
        _ when type == typeof(TimeSpan) => TimeSpan.FromTicks(random.NextInt64(0, TimeSpan.TicksPerDay)),
        _ when type == typeof(DateOnly) => DateOnly.FromDateTime(CreateDateTime()),
        _ when type == typeof(TimeOnly) => new TimeOnly(random.NextInt64(0, TimeOnly.MaxValue.Ticks)),
        _ => throw new NotSupportedException(
            $"'{type}' is not a supported strongly typed id primitive. "
            + $"Provide a value through {nameof(AnyIdOptions)}.{nameof(AnyIdOptions.PrimitiveValueFactory)}."),
    };

    private decimal CreateDecimal()
        => new(random.Next(1, int.MaxValue), random.Next(0, int.MaxValue), 0, false, (byte)random.Next(0, 5));

    private float CreateSingle() => (float)((random.NextDouble() * 1_000_000) + 1);

    private double CreateDouble() => (random.NextDouble() * 1_000_000) + 1;

    private DateTime CreateDateTime() => new(random.NextInt64(_minDateTimeTicks, _maxDateTimeTicks), DateTimeKind.Utc);

    private string CreateString()
    {
        var chars = new char[stringLength];

        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = Alphabet[random.Next(Alphabet.Length)];
        }

        return new string(chars);
    }

    private Guid CreateGuid() => new(CreateBytes(16));

    private byte[] CreateBytes(int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);

        return bytes;
    }
}
