using Newtonsoft.Json;

namespace Len.StronglyTypedId.NewtonsoftJson
{
    public class NewtonsoftJsonTests
    {
        [Fact]
        public void SerializeAndDeserialize()
        {
            var settings = new JsonSerializerSettings().UseStronglyTypedId();

            var expected = new OrderId(Guid.NewGuid());

            var json = JsonConvert.SerializeObject(expected, settings);

            var actual = JsonConvert.DeserializeObject<OrderId>(json, settings);

            Assert.Equal(expected, actual);
        }
    }
}