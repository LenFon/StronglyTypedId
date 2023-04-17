namespace Microsoft.Extensions.DependencyInjection;

public class StronglyTypedIdServiceConfigurationTests
{
    [Fact]
    public void RegisterServicesFromAssembly()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        c.RegisterServicesFromAssembly(typeof(StronglyTypedIdServiceConfigurationTests).Assembly);

        Assert.Single(c.AssembliesToRegister);
    }

    [Fact]
    public void RegisterServicesFromAssembly_Argument_Is_Null()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        var ex = Assert.Throws<ArgumentNullException>(() => c.RegisterServicesFromAssembly(null!));

        Assert.Equal("assembly", ex.ParamName);
    }

    [Fact]
    public void RegisterServicesFromAssemblies()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        c.RegisterServicesFromAssemblies(typeof(StronglyTypedIdServiceConfigurationTests).Assembly);

        Assert.Single(c.AssembliesToRegister);
    }

    [Fact]
    public void RegisterServicesFromAssemblies_Argument_Is_Null()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        var ex = Assert.Throws<ArgumentNullException>(() => c.RegisterServicesFromAssemblies(null!));

        Assert.Equal("assemblies", ex.ParamName);
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        c.RegisterServicesFromAssemblyContaining<StronglyTypedIdServiceConfigurationTests>();

        Assert.Single(c.AssembliesToRegister);
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_Of_Type()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        c.RegisterServicesFromAssemblyContaining(typeof(StronglyTypedIdServiceConfigurationTests));

        Assert.Single(c.AssembliesToRegister);
    }

    [Fact]
    public void RegisterServicesFromAssemblyContaining_Argument_Is_Null()
    {
        var c = new StronglyTypedIdServiceConfiguration();

        var ex = Assert.Throws<ArgumentNullException>(() => c.RegisterServicesFromAssemblyContaining(null!));

        Assert.Equal("type", ex.ParamName);
    }
}