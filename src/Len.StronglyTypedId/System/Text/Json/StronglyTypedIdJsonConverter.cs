﻿using System.Text.Json.Serialization;

namespace System.Text.Json;

internal class StronglyTypedIdJsonConverter<TStronglyTypedId, TPrimitiveId> : JsonConverter<TStronglyTypedId>
    where TStronglyTypedId : IStronglyTypedId<TPrimitiveId>
    where TPrimitiveId : notnull, IComparable, IComparable<TPrimitiveId>, IEquatable<TPrimitiveId>
{
    public override TStronglyTypedId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = (TPrimitiveId)GetValue(reader);

        return (TStronglyTypedId)TStronglyTypedId.Create(value);
    }

    public override void Write(Utf8JsonWriter writer, TStronglyTypedId value, JsonSerializerOptions options)
    {
        var writeAction = GetWriteAction(writer, value);
        writeAction();
    }

    private static object GetValue(Utf8JsonReader reader)
    {
        return typeof(TPrimitiveId) switch
        {
            { } t when t == typeof(Guid) => reader.GetGuid(),
            { } t when t == typeof(int) => reader.GetInt32(),
            { } t when t == typeof(long) => reader.GetInt64(),
            { } t when t == typeof(uint) => reader.GetUInt32(),
            { } t when t == typeof(ulong) => reader.GetUInt64(),
            { } t when t == typeof(string) => reader.GetString() ?? string.Empty,
            _ => throw new NotSupportedException()
        };
    }

    private static Action GetWriteAction(Utf8JsonWriter writer, TStronglyTypedId value)
    {
        return value.Value switch
        {
            Guid val => () => writer.WriteStringValue(val.ToString()),
            int val => () => writer.WriteNumberValue(val),
            long val => () => writer.WriteNumberValue(val),
            uint val => () => writer.WriteNumberValue(val),
            ulong val => () => writer.WriteNumberValue(val),
            string val => () => writer.WriteStringValue(val),
            _ => throw new NotSupportedException()
        };
    }
}