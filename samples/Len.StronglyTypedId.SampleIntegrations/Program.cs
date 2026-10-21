namespace Len.StronglyTypedId.SampleIntegrations;

public static class Program
{
    public static async Task Main()
    {
        await EfCoreDemo.RunAsync();
        await DapperDemo.RunAsync();
        BinarySerializationDemo.Run();

        Demo.End();
    }
}
