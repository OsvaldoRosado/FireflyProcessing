using System.Web.Http;

namespace FireflyProcessing.Controllers
{
    public class DefaultController : ApiController
    {
        public string Get()
        {
            return "ack";
        }
    }
}
