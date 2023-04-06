using System.Text.Json;
using StronglyTypedId.SystemTextJson;

namespace StronglyTypedId.Test
{
    public class SystemTextJsonTests
    {
        [Fact]
        public void SerializeAndDeserialize()
        {
            var settings = new JsonSerializerOptions().UseStronglyTypedId();

            var expected = new OrderId(Guid.NewGuid());

            var json = JsonSerializer.Serialize(expected, settings);

            var actual = JsonSerializer.Deserialize<OrderId>(json, settings);

            Assert.Equal(expected, actual);
        }
    }
}
