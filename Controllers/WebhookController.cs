using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using WspBussiness.Models;

namespace WspBussiness.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private const string VERIFY_TOKEN = "chinchulin";
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(ILogger<WebhookController> logger)
        {
            _logger = logger;
        }

        // GET /api/webhook -> Verificación
        [HttpGet]
        public IActionResult Get(
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string token,
            [FromQuery(Name = "hub.challenge")] string challenge)
        {
            _logger.LogInformation($"Verificación recibida - Mode: {mode}, Token: {token}, Challenge: {challenge}");

            if (mode == "subscribe" && token == VERIFY_TOKEN)
            {
                _logger.LogInformation("✅ Verificación exitosa");
                return Content(challenge);
            }

            _logger.LogWarning("❌ Verificación fallida");
            return Unauthorized("Token inválido");
        }

        // POST /api/webhook -> Recibir mensajes
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            // Parsear con System.Text.Json
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var webhook = JsonSerializer.Deserialize<WhatsappResponse>(body, options);

            // Obtener el mensaje parseado como objeto
            var usuario = webhook?.Entry?[0]?.Changes?[0]?.Value?.Contacts?[0];
            var mensaje = webhook?.Entry?[0]?.Changes?[0]?.Value?.Messages?[0];

            if (mensaje != null)
            {
                var data = new WebhookResponse
                {
                    Nombre = usuario?.Profile?.Name,
                    Mensaje = mensaje.Text?.Body,
                    Horario = DateTimeOffset.FromUnixTimeSeconds(long.Parse(mensaje.Timestamp)).DateTime,
                    Numero = mensaje.From
                };

                // Retornar solo el objeto Message
                return Ok(data);
            }

            return Ok();
        }


    }
}