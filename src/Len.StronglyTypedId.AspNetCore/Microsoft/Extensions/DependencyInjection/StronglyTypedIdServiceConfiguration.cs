using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public class StronglyTypedIdServiceConfiguration
{
    internal List<Assembly> AssembliesToRegister { get; } = new();

    /// <summary>
    /// 从程序集集合中注册转换器
    /// </summary>
    /// <param name="assemblies">程序集集合</param>
    /// <returns></returns>
    public StronglyTypedIdServiceConfiguration RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies, nameof(assemblies));

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
        ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));

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
    {
        ArgumentNullException.ThrowIfNull(type, nameof(type));

        return RegisterServicesFromAssembly(type.Assembly);
    }
}