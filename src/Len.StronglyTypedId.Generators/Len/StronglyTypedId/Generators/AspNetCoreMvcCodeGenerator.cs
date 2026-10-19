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
        var stronglyTypedIds = discoveredIds.External.ToList();

        // 每个 Id 注册为路由约束，模板里写 {id:OrderId} 即可校验分段格式。同名 Id 按字典覆盖，属边缘冲突。
        var constraintRegistrations = stronglyTypedIds
            .Select(idInfo => $$"""        options.ConstraintMap["{{idInfo.Name}}"] = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdRouteConstraint<{{idInfo.FullyQualifiedName}}>);""");

        var code = GeneratedCode.Header("Len.StronglyTypedId") + $$"""
            internal static partial class StronglyTypedIds
            {
                /// <summary>Employ strongly typed IDs in ASP.NET Core MVC model binding.</summary>
                /// <param name="options"><see cref="global::Microsoft.AspNetCore.Mvc.MvcOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.AspNetCore.Mvc.MvcOptions options)
                {
                    options.ModelBinderProviders.Insert(0, new StronglyTypedIdModelBinderProvider());
                }

                /// <summary>Employ strongly typed IDs as ASP.NET Core route constraints.</summary>
                /// <param name="options"><see cref="global::Microsoft.AspNetCore.Routing.RouteOptions"/>.</param>
                {{GeneratedCode.CompilerGeneratedAttribute}}
                {{GeneratedCode.GeneratedCodeAttribute(nameof(AspNetCoreMvcCodeGenerator), version)}}
                public static void ApplyTo(global::Microsoft.AspNetCore.Routing.RouteOptions options)
                {
            {{string.Join("\n", constraintRegistrations)}}
                }

                /// <summary>把「实现了 <see cref="global::Len.StronglyTypedId.IStronglyTypedId{TSelf, TPrimitiveId}"/> 的强类型 Id」路由到 <see cref="StronglyTypedIdModelBinder{TSelf}"/>；其余类型交还 MVC 默认绑定链路。</summary>
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

                        // 仅接管实现了 IStronglyTypedId<,> 的类型，避免误伤其它可解析类型。
                        foreach (var interfaceType in modelType.GetInterfaces())
                        {
                            if (interfaceType.IsGenericType
                                && interfaceType.GetGenericTypeDefinition() == typeof(global::Len.StronglyTypedId.IStronglyTypedId<,>))
                            {
                                var binderType = typeof(global::Len.StronglyTypedId.StronglyTypedIds.StronglyTypedIdModelBinder<>).MakeGenericType(modelType);

                                return (global::Microsoft.AspNetCore.Mvc.ModelBinding.IModelBinder)global::System.Activator.CreateInstance(binderType)!;
                            }
                        }

                        return null;
                    }
                }

                /// <summary>通用强类型 Id 模型绑定器：以静态 <see cref="global::System.IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/> 把字符串值还原为强类型 Id（每个强类型 Id 都经 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c> 间接实现 <see cref="global::System.IParsable{TSelf}"/>）。</summary>
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

                /// <summary>通用强类型 Id 路由约束：分段值能经 <see cref="global::System.IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/> 还原即视为通过，使 <c>{id:OrderId}</c> 这类模板命中强类型 Id 的解析规则。</summary>
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
                        if (!values.TryGetValue(routeKey, out var rawValue) || rawValue is not string text)
                        {
                            return false;
                        }

                        return TId.TryParse(text, global::System.Globalization.CultureInfo.InvariantCulture, out _);
                    }
                }
            }
            """;

        context.AddSource("StronglyTypedIds.AspNetCoreMvc.g.cs", GeneratedCode.NormalizeLineEndings(code));
    }
}
