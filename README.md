# StronglyTypedId
[![codecov][badge-codecov]](https://codecov.io/gh/LenFon/StronglyTypedId)
[![NuGet][badge-nuget]][nuget-package]

A base implementation of strongly typed ids that supports Newtonsoft.Json, System.Text.Json, EntityFramework Core, and Swashbuckle.AspNetCore.
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
**Note**: Only the record type is supported and cannot be nested, abstract, or generic.Support for serialization and deserialization of **Newtonsoft.Json(version 13.0.0 or above)** and **System.Text.Json**.

## EntityFramework Core (version 7.0.0 or above)
Add the converter for a strongly typed id to the configuration of DbContext.
```C#
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        //...other

        StronglyTypedIds.ApplyTo(configurationBuilder);
    }
```
## Swashbuckle.AspNetCore
Add the converter for a strongly typed id to the configuration of DbContext.

```C#
    services.AddSwaggerGen(options =>
    {
        StronglyTypedIds.ApplyTo(options);
    });
```

[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg
[badge-codecov]: https://codecov.io/github/LenFon/StronglyTypedId/branch/master/graph/badge.svg?token=S3PBV7W190
