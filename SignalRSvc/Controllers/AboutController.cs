using Microsoft.AspNetCore.Mvc;

namespace SignalRSvc.Controllers
{
    [Route("api/[controller]")]
    public class AboutController : Controller
    {
        [HttpGet]
        public string Get() =>
            "This is \"SignalRSvc\" service - a sample of SignalR usage";
    }
}
