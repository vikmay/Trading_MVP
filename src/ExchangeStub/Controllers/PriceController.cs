using Microsoft.AspNetCore.Mvc;
using Common;

namespace ExchangeStub.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PriceController : ControllerBase
    {
        [HttpGet]
        public ActionResult<RawTick> Get()
        {
            var rnd = new Random();
            var tick = new RawTick(
                "BTCUSDT",
                (decimal)(rnd.NextDouble() * 1000 + 30000),
                (decimal)(rnd.NextDouble() * 1000 + 30000),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );
            return Ok(tick);
        }
    }
}