# StronglyTypedId [![NuGet][badge-nuget]][nuget-package]
A base implementation of strongly typed ids.
# Getting started
1. Install the standard Nuget package into your application or library.

    ```
    Package Manager : Install-Package Len.StronglyTypedId -Version 1.0.2
    CLI : dotnet add package --version 1.0.2 Len.StronglyTypedId
    ```
2. Use record to define strongly-typed ids:
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
## System.Text.Json (STJ) or Newtonsoft
If you need to use strongly typed ids as output parameters for back-end interfaces in ASP. Net Core applications, you need to perform the following steps:

1. Install the **System.Text.Json (STJ)** package or **Newtonsoft** package into the ASP.NET Core program.

   ```
    Package Manager : Install-Package Len.StronglyTypedId.SystemTextJson -Version 1.0.2
    CLI : dotnet add package --version 1.0.2 Len.StronglyTypedId.SystemTextJson
    ```
    or
       
    ```
    Package Manager : Install-Package Len.StronglyTypedId.NewtonsoftJson -Version 1.0.2
    CLI : dotnet add package --version 1.0.2 Len.StronglyTypedId.NewtonsoftJson
    ```
    **Note**: Only one of them can be selected.

2. Enable strongly typed ids in the JsonOptions configuration.
    ```C#
    services.AddControllers().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.UseStronglyTypedId();
    });
    ```
    or
    ```C#
    services.AddControllers().AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.UseStronglyTypedId();
    });
    ```
[nuget-package]: https://www.nuget.org/packages/Len.StronglyTypedId/
[badge-nuget]: https://img.shields.io/nuget/v/Len.StronglyTypedId.svg