using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.Sample1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController(SampleDbContext db) : ControllerBase
    {
        [HttpGet]
        public async Task<IEnumerable<Order>> Get()
        {
            return await db.Set<Order>().ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<Order?> Get(OrderId id)
        {
            return await db.Set<Order>().FirstOrDefaultAsync(w => w.Id == id);
        }

        /// <summary>
        /// 添加订单
        /// </summary>
        /// <param name="input">订单输入，包含买家 Id 与订单行。</param>
        [HttpPost]
        public async Task Post([FromBody] AddOrderInput input)
        {
            await db.AddAsync(
                new Order
                {
                    Id = new OrderId(Guid.NewGuid()),
                    Buyer = input.Buyer,
                    Items = [.. input.OrderLines.Select(s => new Product { Key = s.Id, Name = s.Name, })]
                }
            );

            await db.SaveChangesAsync();
        }

        public class AddOrderInput
        {
            /// <summary>
            /// 买家Id
            /// </summary>
            public UserId Buyer { get; set; }

            public List<OrderLine> OrderLines { get; set; } = [];

            public class OrderLine
            {
                /// <summary>
                /// 商品Id
                /// </summary>
                public ProductId Id { get; set; }

                /// <summary>
                /// 商品名称
                /// </summary>
                public string Name { get; set; } = default!;
            }
        }

        [HttpDelete("{id}")]
        public async Task Delete(OrderId id)
        {
            var order = await db.Set<Order>().FindAsync(id) ?? throw new Exception("订单未找到");

            db.Remove(order);

            await db.SaveChangesAsync();
        }

        [HttpGet("test/{id}")]
        public UserId Test(UserId id)
        {
            return id;
        }
    }
}
