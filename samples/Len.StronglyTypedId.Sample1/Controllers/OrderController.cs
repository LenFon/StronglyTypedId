using Len.StronglyTypedId.Sample1.Models;
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

        [HttpPost]
        public async Task Post([FromBody] string value)
        {
            await _db.AddAsync(new Order
            {
                Id = new OrderId(Guid.NewGuid()),
                Buyer = "buyer 1",
                Items = new List<Product>
                {
                    new Product{ Key=new ProductId(1),Name="product 1" },
                    new Product{ Key=new ProductId(2),Name="product 1" },
                }
            });

            await _db.SaveChangesAsync();
        }

        [HttpDelete("{id}")]
        public async Task Delete(OrderId id)
        {
            var order = await _db.Set<Order>().FindAsync(id) ?? throw new Exception("����δ�ҵ�");

            _db.Remove(order);

            await _db.SaveChangesAsync();
        }
    }
}