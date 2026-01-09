using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

namespace LearningAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IDistributedCache _cache;

        public TestController(IDistributedCache cache)
        {
            _cache = cache;
        }

        [HttpGet("test-redis")]
        public async Task<IActionResult> Test()
        {
            await _cache.SetStringAsync("check_connection", "Hello from Redis! " + DateTime.Now);

            var value = await _cache.GetStringAsync("check_connection");

            return Ok($"Redis works! Value: {value}");
        }
    }
}
