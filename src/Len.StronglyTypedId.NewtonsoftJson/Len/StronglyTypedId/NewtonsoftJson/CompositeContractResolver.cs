using Newtonsoft.Json.Serialization;
using System.Collections;

namespace Len.StronglyTypedId.NewtonsoftJson;

internal class CompositeContractResolver : IContractResolver, IEnumerable<IContractResolver>
{
    private readonly IList<IContractResolver> _innerResolvers = new List<IContractResolver>();
    private readonly IContractResolver _resolver;

    public CompositeContractResolver() : this(new DefaultContractResolver())
    {
    }

    public CompositeContractResolver(IContractResolver? resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver, nameof(resolver));

        _resolver = resolver;
        _innerResolvers.Add(_resolver);
    }

    public void Add(IContractResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver, nameof(resolver));

        _innerResolvers.Insert(0, resolver);
    }

    public IEnumerator<IContractResolver> GetEnumerator() => _innerResolvers.GetEnumerator();

    public JsonContract ResolveContract(Type type) =>
        _innerResolvers.Select(s => s.ResolveContract(type)).FirstOrDefault()!;

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}