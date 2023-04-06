using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Len.StronglyTypedId
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
            private readonly OrderId id;
            private readonly string json;
            private readonly JsonSerializerSettings newtonsoftJsonSettings = new();
            private readonly string rawJson;
            private readonly JsonSerializerOptions systemTextJsonSettings = new();

            public NewtonsoftJsonSerializeAndDeserialize()
            {
                newtonsoftJsonSettings.UseStronglyTypedId();
                systemTextJsonSettings.UseStronglyTypedId();
                id = new OrderId(Guid.NewGuid());
                json = $"\"{Guid.NewGuid()}\"";
                rawJson = new StringBuilder().Append("{\"Value\":\"" + Guid.NewGuid().ToString() + "\"}").ToString();
            }

            [Benchmark]
            public OrderId NewtonsoftJsonDeserialize() => JsonConvert.DeserializeObject<OrderId>(json, newtonsoftJsonSettings);

            [Benchmark]
            public OrderId NewtonsoftJsonRawDeserialize() => JsonConvert.DeserializeObject<OrderId>(rawJson, new JsonSerializerSettings());

            [Benchmark]
            public string NewtonsoftJsonRawSerialize() => JsonConvert.SerializeObject(id, new JsonSerializerSettings());

            [Benchmark]
            public string NewtonsoftJsonSerialize() => JsonConvert.SerializeObject(id, newtonsoftJsonSettings);

            [Benchmark]
            public OrderId SystemTextJsonDeserialize() => System.Text.Json.JsonSerializer.Deserialize<OrderId>(json, systemTextJsonSettings);

            [Benchmark]
            public OrderId SystemTextJsonRawDeserialize() => System.Text.Json.JsonSerializer.Deserialize<OrderId>(rawJson, new JsonSerializerOptions());

            [Benchmark]
            public string SystemTextJsonRawSerialize() => System.Text.Json.JsonSerializer.Serialize(id, new JsonSerializerOptions());

            [Benchmark]
            public string SystemTextJsonSerialize() => System.Text.Json.JsonSerializer.Serialize(id, systemTextJsonSettings);
        }
    }
}