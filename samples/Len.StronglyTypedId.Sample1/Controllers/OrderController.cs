using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Len.StronglyTypedId.Sample1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly SampleDbContext _db;

        public OrderController(SampleDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IEnumerable<Order>> Get()
        {
            return await _db.Set<Order>().ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<Order?> Get(OrderId id)
        {
            return await _db.Set<Order>().FirstOrDefaultAsync(w => w.Id == id);
        }

        /// <summary>
        /// ��Ӷ���
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task Post([FromBody] AddOrderInput input)
        {
            await _db.AddAsync(
                new Order
                {
                    Id = new OrderId(Guid.NewGuid()),
                    Buyer = input.Buyer,
                    Items = input.OrderLines.Select(s => new Product { Key = s.Id, Name = s.Name, }).ToList()
                }
            );

            await _db.SaveChangesAsync();
        }

        public class AddOrderInput
        {
            /// <summary>
            /// ���Id
            /// </summary>
            public UserId Buyer { get; set; }

            public List<OrderLine> OrderLines { get; set; }

            public class OrderLine
            {
                /// <summary>
                /// ��ƷId
                /// </summary>
                public ProductId Id { get; set; }

                /// <summary>
                /// ��Ʒ����
                /// </summary>
                public string Name { get; set; } = default!;
            }
        }

        [HttpDelete("{id}")]
        public async Task Delete(OrderId id)
        {
            var order = await _db.Set<Order>().FindAsync(id) ?? throw new Exception("����δ�ҵ�");

            _db.Remove(order);

            await _db.SaveChangesAsync();
        }

        [HttpGet("test/{id}")]
        public UserId Test(UserId id)
        {
            return id;
        }
    }
}
