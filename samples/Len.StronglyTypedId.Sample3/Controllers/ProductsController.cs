using Len.StronglyTypedId.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Len.StronglyTypedId.Sample3.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    /// <summary>
    /// 路由段经生成的模型绑定器绑定；{id:ProductId} 再由生成的路由约束校验格式。
    /// </summary>
    [HttpGet("{id:ProductId}")]
    public ActionResult<ProductId> Get([FromRoute] ProductId id) => id;

    /// <summary>
    /// 查询参数同样走 IParsable&lt;ProductId&gt;。
    /// </summary>
    [HttpGet("by-seller")]
    public ActionResult<SellerId> BySeller([FromQuery] SellerId seller) => seller;

    /// <summary>
    /// 请求体里的 Id 由 JSON 转换器处理。
    /// </summary>
    [HttpPost]
    public ActionResult<Product> Create([FromBody] Product product) => product;
}
