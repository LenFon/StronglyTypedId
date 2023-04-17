using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Len.StronglyTypedId
{
    public class StringStronglyTypedIdTypeConverterTests
    {
        [Fact]
        public void ConvertTo()
        {
            var id = new StringId("AA");
            var converter = new StronglyTypedIdTypeConverter<StringId>();

            var val = converter.ConvertTo(id, typeof(string));

            Assert.NotNull(val);
            Assert.IsType<string>(val);
            Assert.Equal(id.Value, val);
        }

        [Fact]
        public void ConvertTo_Not_StronglyTypedId()
        {
            var id = "AA";
            var converter = new StronglyTypedIdTypeConverter<StringId>();

            Assert.Throws<NotSupportedException>(() => converter.ConvertTo(id, typeof(Guid)));
        }

        [Fact]
        public void ConvertFrom()
        {
            var id = "AA";
            var converter = new StronglyTypedIdTypeConverter<StringId>();

            var val = converter.ConvertFrom(id);

            Assert.NotNull(val);
            Assert.IsType<StringId>(val);
            Assert.Equal(id, ((StringId)val).Value);
        }

        [Fact]
        public void ConvertFrom_Int()
        {
            var id = 10;
            var converter = new StronglyTypedIdTypeConverter<StringId>();

            Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(id));
        }

        [Fact]
        public void CanConvertFrom()
        {
            var converter = new StronglyTypedIdTypeConverter<StringId>();

            var result1 = converter.CanConvertFrom(typeof(string));

            Assert.True(result1);

            var result2 = converter.CanConvertFrom(typeof(Guid));

            Assert.False(result2);
        }

        [Fact]
        public void CanConvertTo()
        {
            var converter = new StronglyTypedIdTypeConverter<StringId>();

            var result1 = converter.CanConvertTo(typeof(string));

            Assert.True(result1);

            var result2 = converter.CanConvertTo(typeof(Guid));

            Assert.False(result2);
        }
    }
}
