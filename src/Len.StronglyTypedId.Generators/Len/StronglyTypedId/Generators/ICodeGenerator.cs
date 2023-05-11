namespace Len.StronglyTypedId.Generators;

internal interface ICodeGenerator
{
    string Name { get; }

    int Order { get; }

    void Excute(ImmutableArray<StronglyTypedIdTypeInfo> stronglyTypedIdInfos, ImmutableArray<ModuleInfo> modules, SourceProductionContext context, string version);
}