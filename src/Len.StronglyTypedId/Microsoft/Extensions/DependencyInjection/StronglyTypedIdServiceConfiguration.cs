using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

public class StronglyTypedIdServiceConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = new();

    internal List<Func<Type, Type, Attribute>> ConvertHandles { get; } = new()
    {
        (stronglyTypedIdType, primitiveIdType) =>
            new TypeConverterAttribute(typeof(StronglyTypedIdTypeConverter<,>)
                .MakeGenericType(stronglyTypedIdType, primitiveIdType)),
        (stronglyTypedIdType, primitiveIdType) =>
            new JsonConverterAttribute(typeof(StronglyTypedIdJsonConverter<,>)
                .MakeGenericType(stronglyTypedIdType, primitiveIdType))
    };

    /// <summary>
    /// 添加转换处理器
    /// </summary>
    /// <param name="handle"></param>
    public void AddConvertHandler(Func<Type, Type, Attribute> handle)
    {
        ConvertHandles.Add(handle);
    }

    /// <summary>
    /// 从程序集集合中注册转换器
    /// </summary>
    /// <param name="assemblies">程序集集合</param>
    /// <returns></returns>
    public StronglyTypedIdServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        AssembliesToRegister.AddRange(assemblies);

        return this;
    }

    /// <summary>
    /// 从程序集中注册转换器
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns></returns>
    public StronglyTypedIdServiceConfiguration RegisterServicesFromAssembly(Assembly assembly)
    {
        AssembliesToRegister.Add(assembly);

        return this;
    }

    /// <summary>
    /// 从指定类型的程序集中注册转换器
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    public StronglyTypedIdServiceConfiguration RegisterServicesFromAssemblyContaining<T>()
        => RegisterServicesFromAssemblyContaining(typeof(T));

    /// <summary>
    /// 从指定类型的程序集中注册转换器
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns></returns>
    public StronglyTypedIdServiceConfiguration RegisterServicesFromAssemblyContaining(Type type)
        => RegisterServicesFromAssembly(type.Assembly);
}