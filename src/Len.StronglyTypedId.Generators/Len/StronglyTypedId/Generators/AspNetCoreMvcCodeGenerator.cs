namespace Len.StronglyTypedId.Generators;

/// <summary>
/// 为强类型 Id 生成 ASP.NET Core MVC 的模型绑定与路由约束注册入口。
/// </summary>
internal class AspNetCoreMvcCodeGenerator : IIntegrationCodeGenerator
{
    internal static readonly IIntegrationCodeGenerator Instance = new AspNetCoreMvcCodeGenerator();

    public int Order { get; } = 7;

    public void Generate(
        DiscoveredIds discoveredIds,
        ImmutableArray<ModuleInfo> modules,
        SourceProductionContext context,
        string version)
    {
        var stronglyTypedIds = discoveredIds.ExternalFor(IntegrationFlags.AspNetCoreMvc).ToList();

        // 空注册入口没有任何作用：Integrations = None 时必须连这个文件也不产出。
        if (stronglyTypedIds.Count == 0)
        {
            return;
        }

        // 每个 Id 注册为路由约束，模板里写 {id:MyId} 即可校验分段格式。同名 Id 按字典覆盖，属边缘冲突。
        // 键用 Name 而非 EscapedName：路由约束令牌是路由模板里的字面量，不是 C# 代码，
        // 写成 "@class" 会让用户必须在路由里写 {id:@class}，与约束语义相反。
        var constraintRegistrations = stronglyTypedIds
            .Select(idInfo => $$"""        options.ConstraintMap["{{idInfo.Name}}"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<{{idInfo.FullyQualifiedName}}>);""");

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>
                /// 在 ASP.NET Core MVC 模型绑定中启用强类型 Id。
                /// </summary>
                /// <param name="options"><see cref="global::Microsoft.AspNetCore.Mvc.MvcOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.AspNetCore.Mvc.MvcOptions options)
                {
                    // 幂等：重复调用时不再插入，避免 provider 列表增长。
                    foreach (var provider in options.ModelBinderProviders)
                    {
                        if (provider is StronglyTypedIdModelBinderProvider)
                        {
                            return;
                        }
                    }

                    options.ModelBinderProviders.Insert(0, new StronglyTypedIdModelBinderProvider());
                }

                /// <summary>
                /// 把强类型 Id 用作 ASP.NET Core 路由约束。
                /// </summary>
                /// <param name="options"><see cref="global::Microsoft.AspNetCore.Routing.RouteOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.AspNetCore.Routing.RouteOptions options)
                {
            {{string.Join("\n", constraintRegistrations)}}
                }

                /// <summary>
                /// 把实现了 <see cref="global::Len.StronglyTypedId.IStronglyTypedId{TSelf, TPrimitiveId}"/> 的类型
                /// 交给 <see cref="StronglyTypedIdModelBinder{TSelf}"/>，其余类型交还 MVC 默认绑定链路。
                /// </summary>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                private sealed class StronglyTypedIdModelBinderProvider : global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinderProvider
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                    public global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder? GetBinder(
                        global::Microsoft.AspNetCore.Mvc.ModelBinding.ModelBinderProviderContext context)
                    {
                        var modelType = context.Metadata.ModelType;

                        // Nullable<T> 不实现任何接口，先拆壳才能扫到 IStronglyTypedId<,>，也才能满足 IParsable<TId> 约束；
                        // 否则参数会落到框架的 TryParseModelBinder，按来源文化解析，与不变文化契约不符。
                        var idType = global::System.Nullable.GetUnderlyingType(modelType) ?? modelType;

                        // 只接管实现了 IStronglyTypedId<,> 的类型，避免误伤其它可解析类型。
                        foreach (var interfaceType in idType.GetInterfaces())
                        {
                            if (interfaceType.IsGenericType
                                && interfaceType.GetGenericTypeDefinition() == typeof(global::Len.StronglyTypedId.IStronglyTypedId<,>))
                            {
                                var binderType = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdModelBinder<>).MakeGenericType(idType);

                                return (global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder)global::System.Activator.CreateInstance(binderType)!;
                            }
                        }

                        return null;
                    }
                }

                /// <summary>
                /// 通用模型绑定器：用 <see cref="global::System.IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/>
                /// 把字符串还原为强类型 Id。
                /// </summary>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                private sealed class StronglyTypedIdModelBinder<TId> : global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder where TId : global::System.IParsable<TId>
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                    public global::System.Threading.Tasks.Task BindModelAsync(
                        global::Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingContext bindingContext)
                    {
                        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);

                        if (valueProviderResult == global::Microsoft.AspNetCore.Mvc.ModelBinding.ValueProviderResult.None)
                        {
                            return global::System.Threading.Tasks.Task.CompletedTask;
                        }

                        var value = valueProviderResult.FirstValue;

                        if (global::System.String.IsNullOrEmpty(value))
                        {
                            return global::System.Threading.Tasks.Task.CompletedTask;
                        }

                        if (TId.TryParse(value, global::System.Globalization.CultureInfo.InvariantCulture, out var id))
                        {
                            bindingContext.Result = global::Microsoft.AspNetCore.Mvc.ModelBinding.ModelBindingResult.Success(id);
                        }
                        else
                        {
                            bindingContext.ModelState.TryAddModelError(
                                bindingContext.ModelName,
                                global::System.String.Concat("The value '", value, "' is not a valid strongly typed id."));
                        }

                        return global::System.Threading.Tasks.Task.CompletedTask;
                    }
                }

                /// <summary>
                /// 通用路由约束：分段值能经 <see cref="global::System.IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/>
                /// 还原即通过，使 <c>{id:MyId}</c> 这类模板命中强类型 Id 的解析规则。
                /// </summary>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                private sealed class StronglyTypedIdRouteConstraint<TId> : global::Microsoft.AspNetCore.Routing.IRouteConstraint where TId : global::System.IParsable<TId>
                {
                    /// <inheritdoc/>
                    {{GeneratedCode.CompilerGeneratedAttribute}}
                    {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                    public bool Match(
                        global::Microsoft.AspNetCore.Http.HttpContext? httpContext,
                        global::Microsoft.AspNetCore.Routing.IRouter? route,
                        string routeKey,
                        global::Microsoft.AspNetCore.Routing.RouteValueDictionary values,
                        global::Microsoft.AspNetCore.Routing.RouteDirection routeDirection)
                    {
                        if (!values.TryGetValue(routeKey, out var rawValue))
                        {
                            return false;
                        }

                        // 与框架内置约束一致：先把路由值转成字符串再解析。链接生成阶段传进来的是
                        // Id 实例本身，直接判 string 会让带 {id:MyId} 的路由生成不出 URL。
                        var text = global::System.Convert.ToString(rawValue, global::System.Globalization.CultureInfo.InvariantCulture);

                        return text is not null && TId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out _);
                    }
                }
            }
            """;

        context.AddSource("StronglyTypedIds.AspNetCoreMvc.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }
}
