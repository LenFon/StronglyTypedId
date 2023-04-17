using Len.StronglyTypedId;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace Microsoft.Extensions.DependencyInjection;

public class MvcBuilderExtensionTests
{
    [Fact]
    public void AddStronglyTypedId_Builder_Is_Null()
    {
        IMvcBuilder? builder = null;

        var ex = Assert.Throws<ArgumentNullException>(() =>
        {
            builder!.AddStronglyTypedId(c => { });
        });

        Assert.Equal(nameof(builder), ex.ParamName);
    }

    [Fact]
    public void AddStronglyTypedId()
    {
        IMvcBuilder? builder = new MvcBuilder();

        builder.AddStronglyTypedId(c =>
        {
            c.RegisterServicesFromAssemblyContaining<StringId>();
        });
    }

    [Fact]
    public void AddStronglyTypedId_Argument_Exception()
    {
        IMvcBuilder? builder = new MvcBuilder();

        var ex = Assert.Throws<ArgumentException>(() => builder.AddStronglyTypedId(c =>
        {
        }));
    }

    class MvcBuilder : IMvcBuilder
    {
        public IServiceCollection Services => new ServiceCollection();

        public ApplicationPartManager PartManager => new();
    }
}