using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Newtonsoft.Json 转换器。
/// </summary>
internal class NewtonsoftJsonCodeGenerator : ICodeGenerator
{
    internal static readonly ICodeGenerator Instance = new NewtonsoftJsonCodeGenerator();

    public int Order { get; } = 2;

    public void Generate(
        ImmutableArray<StronglyTypedIdInfo> stronglyTypedIdInfos,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        foreach (var idInfo in stronglyTypedIdInfos)
        {
            var header = GeneratedCode.Header(idInfo.Namespace);

            var declaration = $$"""
                [global::Newtonsoft.Json.JsonConverter(typeof(NewtonsoftJsonConverter))]
                partial record{{idInfo.TypeKindSuffix}} {{idInfo.Name}}
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonCodeGenerator), version)}}
                    public class NewtonsoftJsonConverter : global::Newtonsoft.Json.JsonConverter<{{idInfo.Name}}?>
                    {
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonCodeGenerator), version)}}
                        public override {{idInfo.Name}}? ReadJson(
                            global::Newtonsoft.Json.JsonReader reader,
                            global::System.Type objectType,
                            {{idInfo.Name}}? existingValue,
                            bool hasExistingValue,
                            global::Newtonsoft.Json.JsonSerializer serializer){{GetReadTail(idInfo)}}
                
                        /// <inheritdoc/>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(NewtonsoftJsonCodeGenerator), version)}}
                        public override void WriteJson(
                            global::Newtonsoft.Json.JsonWriter writer,
                            {{idInfo.Name}}? value,
                            global::Newtonsoft.Json.JsonSerializer serializer){{GetWriteTail(idInfo)}}
                    }
                }
                """;

            var code = header + GeneratedCode.NestInContainingTypes(idInfo, declaration);

            // 与另外两个内嵌式生成器一致地统一行尾：嵌套包装是按 \n 逐行拼出来的，
            // 与原始字符串字面量（跟随源文件行尾）相接会得到混排行尾。非嵌套时本调用是空操作，
            // 因为整段文本来自同一个字面量、行尾本就一致。
            context.AddSource($"{idInfo.HintName}.NewtonsoftJson.g.cs", GeneratedCode.NormalizeLineEndings(code));
        }
    }

    /// <summary>
    /// 取 <c>ReadJson</c> 成员形参表之后的尾部：基元形态是表达式体，对象形态是方法体。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 基元形态（默认）把 Id 与其基元值视为同一形状，产物与引入本开关之前<b>逐字节一致</b>；
    /// 对象形态读入 <c>{ "value": … }</c>，供「已对外承诺对象形态」的既有契约迁移。
    /// </para>
    /// <para>
    /// 缩进按产物中的层级逐行写死（成员 8 空格、方法体 12 空格，依次递增），不依赖原始字符串字面量的
    /// 裁剪规则；行尾由 <see cref="GeneratedCode.NormalizeLineEndings"/> 在最后统一。
    /// </para>
    /// </remarks>
    private static string GetReadTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        if (!idInfo.IsObjectJsonShape)
        {
            builder.Append(" =>").Append('\n');
            builder.Append("            serializer.Deserialize<").Append(idInfo.PrimitiveIdTypeName).Append("?>(reader) switch").Append('\n');
            builder.Append("            {").Append('\n');
            builder.Append("                { } value => ").Append(idInfo.Name).Append(".Create(value),").Append('\n');
            builder.Append("                null when (objectType.IsClass || global::System.Nullable.GetUnderlyingType(objectType) is not null) => null,").Append('\n');
            builder.Append("                _ => throw new global::System.InvalidOperationException($\"Cannot get the value of a token type '{reader.TokenType}' as a ").Append(idInfo.Name).Append("\")").Append('\n');
            builder.Append("            };");

            return builder.ToString();
        }

        builder.Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append("            if (reader.TokenType == global::Newtonsoft.Json.JsonToken.Null)").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                return null;").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            if (reader.TokenType != global::Newtonsoft.Json.JsonToken.StartObject)").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                throw new global::Newtonsoft.Json.JsonSerializationException($\"Cannot convert the JSON token '{reader.TokenType}' to a ").Append(idInfo.Name).Append(": an object with a 'value' property was expected.\");").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            var json = global::Newtonsoft.Json.Linq.JObject.Load(reader);").Append('\n');
        builder.Append('\n');
        builder.Append("            if (!json.TryGetValue(\"value\", out var valueToken))").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                throw new global::Newtonsoft.Json.JsonSerializationException(\"Cannot convert a JSON object without a 'value' property to a ").Append(idInfo.Name).Append(".\");").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append('\n');
        builder.Append("            return valueToken.ToObject<").Append(idInfo.PrimitiveIdTypeName).Append("?>() switch").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                { } value => ").Append(idInfo.Name).Append(".Create(value),").Append('\n');
        builder.Append("                _ => throw new global::System.InvalidOperationException($\"Cannot get the value of a token type '{valueToken.Type}' as a ").Append(idInfo.Name).Append("\")").Append('\n');
        builder.Append("            };").Append('\n');
        builder.Append("        }");

        return builder.ToString();
    }

    /// <summary>
    /// 取 <c>WriteJson</c> 成员形参表之后的方法体。
    /// </summary>
    /// <remarks>
    /// 基元形态写出基元值本身；对象形态写出 <c>{ "value": … }</c>。属性名固定为 <c>value</c>，
    /// 不跟随宿主的命名策略 —— Id 的 JSON 形状是对外契约，不应随序列化配置漂移。
    /// </remarks>
    private static string GetWriteTail(StronglyTypedIdInfo idInfo)
    {
        var builder = new StringBuilder();

        builder.Append('\n');
        builder.Append("        {").Append('\n');
        builder.Append("            if (value is { Value: var primitiveId })").Append('\n');
        builder.Append("            {").Append('\n');

        if (idInfo.IsObjectJsonShape)
        {
            builder.Append("                writer.WriteStartObject();").Append('\n');
            builder.Append("                writer.WritePropertyName(\"value\");").Append('\n');
            builder.Append("                writer.WriteValue(primitiveId);").Append('\n');
            builder.Append("                writer.WriteEndObject();").Append('\n');
        }
        else
        {
            builder.Append("                writer.WriteValue(primitiveId);").Append('\n');
        }

        builder.Append("            }").Append('\n');
        builder.Append("            else").Append('\n');
        builder.Append("            {").Append('\n');
        builder.Append("                writer.WriteNull();").Append('\n');
        builder.Append("            }").Append('\n');
        builder.Append("        }");

        return builder.ToString();
    }
}
