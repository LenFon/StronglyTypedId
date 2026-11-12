using System.Text;

namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 Entity Framework Core 的 <c>ValueConverter</c> 与统一注册入口。
/// </summary>
internal class EfCoreCodeGenerator : IIntegrationCodeGenerator
{
    /// <summary>
    /// <c>AddStronglyTypedIds</c> 方法体内语句的缩进（产物中为 8 空格）。
    /// </summary>
    private static readonly string _registrationIndent = new(' ', 8);

    internal static readonly IIntegrationCodeGenerator Instance = new EfCoreCodeGenerator();

    public int Order { get; } = 3;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.EntityFrameworkCore).ToList();

        // 空注册入口没有任何作用：Integrations = None 时必须连这个文件也不产出。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        // 同名 Id 会生成进同一个 StronglyTypedIds 类并撞 CS0102；仅在确实重名时才用「命名空间_类型名」消歧，
        // 以免改动无冲突场景下既有的转换器类名。
        var ambiguousNames = IntegrationNaming.GetAmbiguousNames(stronglyTypedIds);
        var collidingNames = IntegrationNaming.GetCollidingNames(stronglyTypedIds, ambiguousNames, "Converter");

        var registrations = new StringBuilder();
        var modelBuilderRegistrations = new StringBuilder();

        foreach (var idInfo in stronglyTypedIds)
        {
            var converterName = IntegrationNaming.GetTypeName(idInfo, ambiguousNames, "Converter", collidingNames);

            // 自定义基元的 provider 类型必须是数据库认得的类型。直接用基元本身会让 EF 报
            // 「The '<Primitive>' property could not be mapped because the database provider does not support
            // this type」—— 数据库不认识用户自定义的 CLR 类型。故与 JSON / 二进制 / Dapper 同一口径，
            // 落库的是规范文本（string）。
            // BigInteger / Int128 / UInt128 / Half 同理：它们没有 provider 类型，直接作为 provider 会拒绝映射整个属性。
            // 引用类型基元为 null 时落库为 null，而不是在 ToString() 上抛 NRE。表达式树里允许条件表达式，
            // 只是不允许 throw（CS8188），故这里可以内联判空。
            // provider 类型写成 string?：判空分支会返回 null，非空 string 会触发 CS8603。运行时
            // ProviderClrType 仍是 System.String，EF 的映射不受影响。
            var providerType = idInfo.IsDatabaseTextPrimitive
                ? idInfo.IsReferenceTypePrimitive ? "string?" : "string"
                : idInfo.PrimitiveIdTypeName;
            var toProvider = idInfo.IsDatabaseTextPrimitive
                ? idInfo.GetPrimitiveTextExpressionOrNull("v.Value")
                : "v.Value";
            // 一律经辅助方法还原：表达式树不允许 throw（CS8188），而「验证器拒绝」与「枚举取值未定义」
            // 都必须抛 EF Core 认得的异常，不能把 Create 的 ArgumentException 漏给物化管线。
            var fromProvider = "CreateFromProvider(val)";

            var fromProviderHelper = GetFromProviderMember(idInfo, providerType, version);

            var converterCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
                internal static partial class StronglyTypedIds
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                    class {{converterName}} : global::Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<{{idInfo.FullyQualifiedName}}, {{providerType}}>
                    {
                        /// <summary>
                        /// 默认构造函数。
                        /// </summary>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public {{converterName}}() : base(v => {{toProvider}}, val => {{fromProvider}})
                        {
                        }{{fromProviderHelper}}

                        /// <summary>
                        /// 通过约定把 {{idInfo.EscapedName}} 转换器应用到 Entity Framework Core。请在 <c>ConfigureConventions(ModelConfigurationBuilder)</c> 中调用。
                        /// </summary>
                        /// <param name="configurationBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"/></param>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder) =>
                            configurationBuilder.Properties<{{idInfo.FullyQualifiedName}}>().HaveConversion(typeof({{converterName}}));

                        /// <summary>
                        /// 通过扫描模型把 {{idInfo.EscapedName}} 转换器应用到 Entity Framework Core。
                        /// 约定不可用时在 <c>OnModelCreating(ModelBuilder)</c> 末尾调用。
                        /// </summary>
                        /// <param name="modelBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelBuilder"/></param>
                        {{GeneratedCode.CompilerGeneratedAttribute}}
                        {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                        public static void ApplyTo(global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
                        {
                            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                            {
                                // 不能遍历 entityType.GetProperties()：EF 的约定集不会把「既不是受支持基元、
                                // 也不是合法实体类型」的 CLR 属性加进模型，强类型 Id 正好落在这一档，
                                // 于是那个循环永远空转。必须按 CLR 反射枚举属性，再用 Property(...) 显式纳入模型。
                                foreach (var clrProperty in entityType.ClrType.GetProperties(
                                    global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Instance))
                                {
                                    // 可空 Id 属性的声明类型是 Nullable<Id>，不拆壳就匹配不到。
                                    var propertyType = global::System.Nullable.GetUnderlyingType(clrProperty.PropertyType)
                                        ?? clrProperty.PropertyType;

                                    if (propertyType != typeof({{idInfo.FullyQualifiedName}}))
                                    {
                                        continue;
                                    }

                                    // 用户用 [NotMapped] 或 fluent Ignore 明确排除的属性必须尊重：
                                    // Property(...) 会把它们重新拉回模型，等于推翻用户的决定。
                                    if (entityType is global::Microsoft.EntityFrameworkCore.Metadata.IConventionEntityType conventionEntityType
                                        && conventionEntityType.IsIgnored(clrProperty.Name))
                                    {
                                        continue;
                                    }

                                    // 可空属性挂非可空转换器：EF 自己处理 null，挂 ValueConverter<Id?, T?> 反而仍报「无法映射」。
                                    modelBuilder.Entity(entityType.ClrType)
                                        .Property(clrProperty.Name)
                                        .HasConversion(new {{converterName}}());
                                }
                            }
                        }
                    }
                }
                """;

            GeneratedCode.AddGeneratedSource(context, $"{idInfo.HintName}.EntityFrameworkCore.g.cs", converterCode);

            registrations.Append($"{GeneratedCode.NewLine}{_registrationIndent}{converterName}.ApplyTo(configurationBuilder);");
            modelBuilderRegistrations.Append($"{GeneratedCode.NewLine}{_registrationIndent}{converterName}.ApplyTo(modelBuilder);");
        }

        var registrationCode = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// 通过约定在 Entity Framework Core 中启用强类型 Id。
                /// </summary>
                /// <param name="configurationBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"/></param>
                /// <returns>同一个 <paramref name="configurationBuilder"/>，便于链式调用。</returns>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder AddStronglyTypedIds(this global::Microsoft.EntityFrameworkCore.ModelConfigurationBuilder configurationBuilder)
                {{{registrations}}
                    return configurationBuilder;
                }

                /// <summary>
                /// 通过扫描模型在 Entity Framework Core 中启用强类型 Id。
                /// 上下文未重写 <c>ConfigureConventions</c> 时在 <c>OnModelCreating</c> 末尾调用，只扫描已加入模型的实体类型。
                /// </summary>
                /// <param name="modelBuilder"><see cref="global::Microsoft.EntityFrameworkCore.ModelBuilder"/></param>
                /// <returns>同一个 <paramref name="modelBuilder"/>，便于链式调用。</returns>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version)}}
                public static global::Microsoft.EntityFrameworkCore.ModelBuilder AddStronglyTypedIds(this global::Microsoft.EntityFrameworkCore.ModelBuilder modelBuilder)
                {{{modelBuilderRegistrations}}
                    return modelBuilder;
                }
            }
            """;

        GeneratedCode.AddGeneratedSource(context, "StronglyTypedIds.EntityFrameworkCore.g.cs", registrationCode);
    }

    /// <summary>
    /// 取「把数据库文本还原成基元」的条件表达式，结果放在局部变量 <c>value</c> 中。
    /// </summary>
    /// <remarks>
    /// provider 类型对引用类型基元是 <c>string?</c>，而自定义基元的 <c>TryParse</c> 常把首参声明成非可空
    /// <c>string</c>。直接转发会在消费方构建里留下 CS8604，故先挡 null —— 空文本本来就不是合法取值。
    /// 首参接受 null 的基元不加这一档，以免改变它们对空文本的既定处理。
    /// </remarks>
    private static string GetTextParseCondition(StronglyTypedIdInfo idInfo)
    {
        var condition = idInfo.GetPrimitiveTextTryParseCondition("val", "value");

        return idInfo.IsReferenceTypePrimitive && !idInfo.CustomPrimitiveTryParseAcceptsNull
            ? $"val is not null && {condition}"
            : condition;
    }

    /// <summary>
    /// 取 <c>CreateFromProvider</c> 辅助成员：把数据库里的值还原成 Id，拒绝时抛
    /// <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <remarks>
    /// 不能把校验直接写进 <c>base(…, val => …)</c> 的 lambda：那个 lambda 会被编译成表达式树，
    /// 而表达式树不允许 <c>throw</c>（CS8188）。故校验与拒绝异常都放进这个普通方法，lambda 只调用它。
    /// <para>
    /// 校验口径与其余集成共用 <see cref="StronglyTypedIdInfo.GetTryCreateCondition"/>：<c>TryCreate</c>
    /// 跑用户配的 Validator，另附枚举已定义性（普通枚举查 <c>IsDefined</c>，<c>[Flags]</c> 查未定义位）。
    /// 失败时抛 <see cref="InvalidOperationException"/>——EF Core 的转换器契约没有专用异常类型，
    /// 而 <c>Create</c> 的 <c>ArgumentException</c> 会穿透物化管线。
    /// </para>
    /// <para>
    /// 自定义基元与没有 provider 类型的基元（<c>BigInteger</c> / <c>Int128</c> / <c>UInt128</c> / <c>Half</c>）
    /// 的 provider 是规范文本，故先 <c>Parse</c> 回基元再交给 <c>TryCreate</c>；其余基元的 provider 就是基元本身。
    /// </para>
    /// </remarks>
    private static string GetFromProviderMember(StronglyTypedIdInfo idInfo, string providerType, string version)
    {
        // 形参名必须显式传给 GetTryCreateCondition：它默认用 value，而这里的形参叫 val。
        var condition = idInfo.IsDatabaseTextPrimitive
            ? GetTextParseCondition(idInfo)
                + $" && {idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName, "value", "id")}"
            : idInfo.GetTryCreateCondition(idInfo.FullyQualifiedName, "val", "id");

        // 洞紧跟在构造函数右大括号之后，故首行自带一个换行；其余各行自带产物中的绝对缩进（成员级 8 空格）。
        return string.Join(
            GeneratedCode.NewLine,
            string.Empty,
            "        /// <summary>",
            $"        /// 把数据库里的值还原成 {idInfo.Name}；验证器拒绝或枚举取值未定义时抛 <see cref=\"global::System.InvalidOperationException\"/>。",
            "        /// </summary>",
            "        " + GeneratedCode.CompilerGeneratedAttribute,
            "        " + GeneratedCode.GeneratedCodeAttribute(nameof(EfCoreCodeGenerator), version),
            $"        private static {idInfo.FullyQualifiedName} CreateFromProvider({providerType} val)",
            "        {",
            $"            if ({condition})",
            "            {",
            "                return id;",
            "            }",
            string.Empty,
            $"            throw new global::System.InvalidOperationException($\"Cannot convert the database value '{{val}}' to a {idInfo.Name}.\");",
            "        }");
    }
}
