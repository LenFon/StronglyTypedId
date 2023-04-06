using Newtonsoft.Json.Serialization;
using System.Collections;

namespace StronglyTypedId.NewtonsoftJson;

internal class CompositeContractResolver : IContractResolver, IEnumerable<IContractResolver>
{
    private readonly IList<IContractResolver> contractResolvers = new List<IContractResolver>();
    private readonly DefaultContractResolver defaultContractResolver = new();

    public IEnumerator<IContractResolver> GetEnumerator() => contractResolvers.GetEnumerator();

    public JsonContract ResolveContract(Type type) =>
        contractResolvers.Select(s => s.ResolveContract(type)).FirstOrDefault()!;

    public void Add(IContractResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver, nameof(resolver));

        if (contractResolvers.Contains(defaultContractResolver))
        {
            contractResolvers.Remove(defaultContractResolver);
        }

        contractResolvers.Add(resolver);
        contractResolvers.Add(defaultContractResolver);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
