using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Len.StronglyTypedId.Sample1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        [HttpGet]
        public IEnumerable<OrderId> Get()
        {
            return Enumerable.Range(0, 10).Select(s => new OrderId(Guid.NewGuid()));
        }

        [HttpGet("{id}")]
        public OrderId Get(OrderId id)
        {
            return id;
        }

        [HttpGet("{id}/products/{productId}")]
        public GetProductViewModel GetProduct(OrderId id, ProductId productId)
        {
            return new GetProductViewModel
            {
                OrderId = id,
                ProductId = productId
            };
        }

        public class GetProductViewModel
        {
            public OrderId OrderId { get; set; }
            public ProductId ProductId { get; set; }
        }
    }

    public record struct OrderId(Guid Value) : IStronglyTypedId<Guid>
    {
        public static IStronglyTypedId<Guid> Create(Guid value) => new OrderId(value);
    }

    public record struct ProductId(int Value) : IStronglyTypedId<int>
    {
        public static IStronglyTypedId<int> Create(int value) => new ProductId(value);
    }
}