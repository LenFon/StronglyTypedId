using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using System.Text;
using Xunit.Abstractions;

namespace StronglyTypedId.Test
{
    public class BenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public BenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SerializeAndDeserializeWithBenchmark()
        {
            var logger = new AccumulationLogger();

            var config = ManualConfig.Create(DefaultConfig.Instance)
                .AddLogger(logger)
                .WithOptions(ConfigOptions.DisableOptimizationsValidator);

            BenchmarkRunner.Run<NewtonsoftJsonSerializeAndDeserialize>(config);

            // write benchmark summary
            _output.WriteLine(logger.GetLog());
        }

        [MemoryDiagnoser]
        public class NewtonsoftJsonSerializeAndDeserialize
        {
            private readonly Newtonsoft.Json.JsonSerializerSettings newtonsoftJsonSettings = new();
            private readonly System.Text.Json.JsonSerializerOptions systemTextJsonSettings = new();
            private readonly OrderId id;
            private readonly string json;
            private readonly string rawJson;
            public NewtonsoftJsonSerializeAndDeserialize()
            {
                NewtonsoftJson.JsonSerializerOptionsExtensions.UseStronglyTypedId(newtonsoftJsonSettings);
                SystemTextJson.JsonSerializerOptionsExtensions.UseStronglyTypedId(systemTextJsonSettings);
                id = new OrderId(Guid.NewGuid());
                json = $"\"{Guid.NewGuid()}\"";
                rawJson = new StringBuilder().Append("{\"Value\":\"" + Guid.NewGuid().ToString() + "\"}").ToString();
            }

            [Benchmark]
            public string NewtonsoftJsonSerialize() => Newtonsoft.Json.JsonConvert.SerializeObject(id, newtonsoftJsonSettings);

            [Benchmark]
            public string NewtonsoftJsonRawSerialize() => Newtonsoft.Json.JsonConvert.SerializeObject(id, new Newtonsoft.Json.JsonSerializerSettings());

            [Benchmark]
            public string SystemTextJsonSerialize() => System.Text.Json.JsonSerializer.Serialize(id, systemTextJsonSettings);

            [Benchmark]
            public string SystemTextJsonRawSerialize() => System.Text.Json.JsonSerializer.Serialize(id, new System.Text.Json.JsonSerializerOptions());


            [Benchmark]
            public OrderId NewtonsoftJsonDeserialize() => Newtonsoft.Json.JsonConvert.DeserializeObject<OrderId>(json, newtonsoftJsonSettings);

            [Benchmark]
            public OrderId NewtonsoftJsonRawDeserialize() => Newtonsoft.Json.JsonConvert.DeserializeObject<OrderId>(rawJson, new Newtonsoft.Json.JsonSerializerSettings());

            [Benchmark]
            public OrderId SystemTextJsonDeserialize() => System.Text.Json.JsonSerializer.Deserialize<OrderId>(json, systemTextJsonSettings);
            
            [Benchmark]
            public OrderId SystemTextJsonRawDeserialize() => System.Text.Json.JsonSerializer.Deserialize<OrderId>(rawJson, new System.Text.Json.JsonSerializerOptions());

        }
    }
}
