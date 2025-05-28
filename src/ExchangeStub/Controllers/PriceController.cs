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

            // generate prices directly as double (no decimal cast)
            double bid = rnd.NextDouble() * 1_000 + 30_000;
            double ask = rnd.NextDouble() * 1_000 + 30_000;

            var tick = new RawTick(
                "BTCUSDT",
                bid,
                ask,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            );

            return Ok(tick);
        }
    }
}
