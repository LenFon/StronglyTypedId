# StronglyTypedId [![NuGet][badge-nuget]][nuget-package]
A base implementation of strongly typed ids.
# Getting started
1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId -Version 1.0.3
    CLI : dotnet add package --version 1.0.3 Len.StronglyTypedId
    ```
2. Use record to define a strongly typed id:
    ```C#
    public record struct OrderId(Guid Value) : IStronglyTypedId<Guid>
    {
        public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
    }
    ```
    or
    ```C#
    public record OrderId(Guid Value) : IStronglyTypedId<Guid>
    {
        public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
    }
    ```
## Integration with ASP.NET Core
1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId.AspNetCore -Version 1.0.3
    CLI : dotnet add package --version 1.0.3 Len.StronglyTypedId
    ```
2. Add the converter of the strongly type ids to the configuration.
    ```C#
    services.AddControllers().AddStronglyTypedId(options =>
    {
        options.RegisterServicesFromAssemblies(/* assembles */);
    }).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.AddStronglyTypedId();
    });

## Newtonsoft

1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId.AspNetCore.NewtonsoftJson -Version 1.0.3
    CLI : dotnet add package --version 1.0.3 Len.StronglyTypedId.NewtonsoftJson

    ```

2.  Add the converter of the strongly type Id to the configuration.

    ```C#
    services.AddControllers().AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.AddStronglyTypedId();
    });
    ```

## EntityFramework Core
1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId.EntityFrameworkCore -Version 1.0.3
    CLI : dotnet add package --version 1.0.3 Len.StronglyTypedId.EntityFrameworkCore

    ```

2.  Add the converter for a strongly typed id to the configuration of DbContext.

    ```C#
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        //...other

        configurationBuilder.AddStronglyTypedId(/* assembles */);
    }
    ```

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg