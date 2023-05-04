# StronglyTypedId
[![codecov][badge-codecov]](https://codecov.io/gh/LenFon/StronglyTypedId)
[![NuGet][badge-nuget]][nuget-package]

A base implementation of strongly typed ids.
# Getting started
1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId
    CLI : dotnet add package Len.StronglyTypedId 
    ```
2. Use record to define a strongly typed id:
    ```C#
    [StronglyTypedId]
    public partial record struct OrderId(Guid Value);
    ```
    or
    ```C#
    [StronglyTypedId]
    public partial record OrderId(Guid Value);
    ```
    **Note**: Only the record type is supported and cannot be nested, abstract, or generic

## Newtonsoft

1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId.AspNetCore.NewtonsoftJson
    CLI : dotnet add package Len.StronglyTypedId.NewtonsoftJson
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
    Package Manager : Install-Package Len.StronglyTypedId.EntityFrameworkCore
    CLI : dotnet add package Len.StronglyTypedId.EntityFrameworkCore 
    ```

2.  Add the converter for a strongly typed id to the configuration of DbContext.

    ```C#
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        //...other

        configurationBuilder.AddStronglyTypedId();
    }
    ```
## Swashbuckle.AspNetCore
1. Install the package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId.Swagger
    CLI : dotnet add package Len.StronglyTypedId.Swagger
    ```

2.  Add the converter for a strongly typed id to the configuration of DbContext.

    ```C#
    services.AddSwaggerGen(options =>
    {
        options.AddStronglyTypedId();
    });
    ```

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/master/graph/badge.svg?token=S3PBV7W190
