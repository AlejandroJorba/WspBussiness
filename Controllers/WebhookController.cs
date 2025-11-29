using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace WspBussiness.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        // Token que vos definís en Meta
        private const string VERIFY_TOKEN = "chinchulin";

        // GET /webhook -> Verificación
        [HttpGet]
        public IActionResult Get([FromQuery(Name = "hub.mode")] string mode,
                                 [FromQuery(Name = "hub.verify_token")] string token,
                                 [FromQuery(Name = "hub.challenge")] string challenge)
        {
            if (mode == "subscribe" && token == VERIFY_TOKEN)
            {
                return Content(challenge); // Devuelve el challenge a Meta
            }

            return Forbid();
        }

        // POST /webhook -> Recibir mensajes
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Siempre responder 200 OK a Meta
            return Ok(JsonSerializer.Serialize(JsonDocument.Parse(body), new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
