using Len.StronglyTypedId.SampleConsole.Demos;

namespace Len.StronglyTypedId.SampleConsole;

public static class Program
{
    public static void Main()
    {
        CoreDemo.Run();
        ValidationDemo.Run();
        TextDemo.Run();
        InstanceDemo.Run();
        ExceptionDemo.Run();
        FlagsDemo.Run();
        CultureDemo.Run();
        IntegrationsDemo.Run();
        SystemTextJsonDemo.Run();
        NewtonsoftJsonDemo.Run();
        StructureDemo.Run();
        ReflectionDemo.Run();
        TestingDemo.Run();

        Demo.End();
    }
}
