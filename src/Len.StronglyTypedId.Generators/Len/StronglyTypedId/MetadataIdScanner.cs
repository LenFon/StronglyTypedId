using System.Collections.Concurrent;
using System.Reflection.Metadata;

namespace Len.StronglyTypedId;

/// <summary>
/// 直接从元数据读取「哪些类型实现了 <c>IStronglyTypedId&lt;TSelf, TPrimitiveId&gt;</c>」，不构造类型符号。
/// </summary>
/// <remarks>
/// 走符号树要遍历引用程序集的全部类型（<c>System.Private.CoreLib</c> 就有上万个），而每次编译都会重来一遍。
/// 元数据侧只需扫 <c>InterfaceImplementations</c> 表，且结果按模块 MVID 缓存 —— 同一份程序集文件在不同编译里
/// 内容相同，MVID 也就相同，于是 IDE 连续编辑时只算一次。
/// <para>
/// 判据与符号侧保持一致：编译器把接口的<b>传递闭包</b>写进 <c>InterfaceImpl</c> 表，符号侧的
/// <c>ITypeSymbol.Interfaces</c> 也是同一口径（含接口继承、不含基类继承），因此两侧结论相同。
/// 命名空间与元数都要匹配。
/// </para>
/// </remarks>
internal static class MetadataIdScanner
{
    /// <summary>
    /// 接口的元数据名，含泛型元数后缀。
    /// </summary>
    private const string InterfaceMetadataName = "IStronglyTypedId`2";

    /// <summary>
    /// 接口所在命名空间。
    /// </summary>
    private const string InterfaceNamespace = "Len.StronglyTypedId";

    /// <summary>
    /// 运行时程序集的简单名；声明 Id 的模块必然引用它（或它自己就是）。
    /// </summary>
    private const string RuntimeAssemblyName = "Len.StronglyTypedId";

    /// <summary>
    /// 缓存条目上限。只是防止 IDE 长会话里无限增长，超出就整体清空 —— 重算一次比引入淘汰策略简单。
    /// </summary>
    private const int MaxCacheSize = 512;

    /// <summary>
    /// 按模块 MVID 缓存扫描结果。MVID 唯一标识一次构建，内容相同即命中。
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, ImmutableArray<string>> _cache = new();

    /// <summary>
    /// 取单个模块里全部强类型 Id 的元数据名（嵌套类型用 <c>+</c> 分隔，泛型带元数后缀）。
    /// </summary>
    /// <remarks>
    /// 按模块而非整个程序集：多模块程序集里各模块的 Id 集合不同，合并后分派给每个模块会让
    /// <c>GetTypeByMetadataName</c> 在错误的模块上重复解析。
    /// </remarks>
    public static ImmutableArray<string> GetStronglyTypedIdNames(ModuleMetadata module)
    {
        MetadataReader reader;

        try
        {
            reader = module.GetMetadataReader();
        }
        catch (BadImageFormatException)
        {
            // 与 MetadataReferenceExtensions 同一口径：无清单或损坏的模块按「没有 Id」处理，不中断整个发现阶段。
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }

        var mvid = reader.GetGuid(reader.GetModuleDefinition().Mvid);

        if (_cache.TryGetValue(mvid, out var cached))
        {
            return cached;
        }

        var names = Scan(reader);

        if (_cache.Count >= MaxCacheSize)
        {
            _cache.Clear();
        }

        _cache[mvid] = names;

        return names;
    }

    /// <summary>
    /// 扫描单个模块的类型定义表。
    /// </summary>
    private static ImmutableArray<string> Scan(MetadataReader reader)
    {
        // 看不见运行时程序集的模块不可能声明 Id，先按引用表剪枝：这一步只读 AssemblyReferences，
        // 比逐个类型查接口实现便宜几个数量级。
        if (!MayDeclareIds(reader))
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var handle in reader.TypeDefinitions)
        {
            foreach (var implementationHandle in reader.GetTypeDefinition(handle).GetInterfaceImplementations())
            {
                if (IsStronglyTypedIdInterface(reader, reader.GetInterfaceImplementation(implementationHandle).Interface))
                {
                    builder.Add(GetMetadataName(reader, handle));

                    // 一个类型实现多次同一接口没有意义，命中即可跳出。
                    break;
                }
            }
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// 判定模块能否声明 Id：它自己就是运行时程序集，或它引用了运行时程序集。
    /// </summary>
    private static bool MayDeclareIds(MetadataReader reader)
    {
        if (reader.IsAssembly
            && reader.StringComparer.Equals(reader.GetAssemblyDefinition().Name, RuntimeAssemblyName))
        {
            return true;
        }

        foreach (var handle in reader.AssemblyReferences)
        {
            if (reader.StringComparer.Equals(reader.GetAssemblyReference(handle).Name, RuntimeAssemblyName))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判定接口实现句柄是否指向 <c>IStronglyTypedId&lt;,&gt;</c>。
    /// </summary>
    /// <remarks>
    /// 泛型接口在元数据里是 <c>TypeSpecification</c>（<c>GenericInstantiation</c> 签名），要先剥到泛型定义再比较；
    /// 非泛型写法（理论上不会出现）则直接比较。
    /// </remarks>
    private static bool IsStronglyTypedIdInterface(MetadataReader reader, EntityHandle handle)
    {
        if (GetGenericTypeDefinition(reader, handle) is not { } target)
        {
            return false;
        }

        switch (target.Kind)
        {
            case HandleKind.TypeReference:
                var reference = reader.GetTypeReference((TypeReferenceHandle)target);

                return reader.StringComparer.Equals(reference.Name, InterfaceMetadataName)
                    && reader.StringComparer.Equals(reference.Namespace, InterfaceNamespace);

            case HandleKind.TypeDefinition:
                var definition = reader.GetTypeDefinition((TypeDefinitionHandle)target);

                return reader.StringComparer.Equals(definition.Name, InterfaceMetadataName)
                    && reader.StringComparer.Equals(definition.Namespace, InterfaceNamespace);

            default:
                return false;
        }
    }

    /// <summary>
    /// 把 <c>TypeSpecification</c> 剥到它的泛型类型定义；非泛型实例原样返回。
    /// </summary>
    private static EntityHandle? GetGenericTypeDefinition(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind != HandleKind.TypeSpecification)
        {
            return handle;
        }

        var specification = reader.GetTypeSpecification((TypeSpecificationHandle)handle);
        var blob = reader.GetBlobReader(specification.Signature);

        if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
        {
            return null;
        }

        // 先读 class / valuetype 标记，再读泛型类型定义本身。
        _ = blob.ReadSignatureTypeCode();

        return blob.ReadTypeHandle();
    }

    /// <summary>
    /// 拼出 <c>GetTypeByMetadataName</c> 认得的元数据名：嵌套类型用 <c>+</c>，外层带泛型元数后缀。
    /// </summary>
    private static string GetMetadataName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        var name = reader.GetString(definition.Name);
        var declaringType = definition.GetDeclaringType();

        if (declaringType.IsNil)
        {
            var @namespace = reader.GetString(definition.Namespace);

            return @namespace.Length == 0 ? name : @namespace + "." + name;
        }

        // 嵌套类型的 Namespace 为空，必须逐层向外拼。
        return GetMetadataName(reader, declaringType) + "+" + name;
    }
}
