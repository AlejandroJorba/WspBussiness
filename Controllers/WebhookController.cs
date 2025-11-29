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
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();

                _logger.LogInformation($"📨 JSON recibido: {body}");

                // Parsear con System.Text.Json
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var webhook = JsonSerializer.Deserialize<WhatsappResponse>(body, options);

                _logger.LogInformation($"✅ JSON deserializado correctamente");

                // Obtener el mensaje parseado como objeto
                var usuario = webhook?.Entry?[0]?.Changes?[0]?.Value?.Contacts?[0];
                var mensaje = webhook?.Entry?[0]?.Changes?[0]?.Value?.Messages?[0];

                _logger.LogInformation($"Usuario: {usuario?.Profile?.Name ?? "NULL"}");
                _logger.LogInformation($"Mensaje: {mensaje?.Text?.Body ?? "NULL"}");
                _logger.LogInformation($"From: {mensaje?.From ?? "NULL"}");
                _logger.LogInformation($"Timestamp: {mensaje?.Timestamp ?? "NULL"}");

                if (mensaje != null)
                {
                    var data = new WebhookResponse
                    {
                        Nombre = usuario?.Profile?.Name,
                        Mensaje = mensaje.Text?.Body,
                        Horario = DateTimeOffset.FromUnixTimeSeconds(long.Parse(mensaje.Timestamp)).DateTime,
                        Numero = mensaje.From
                    };

                    _logger.LogInformation($"✅ Respuesta creada: {JsonSerializer.Serialize(data)}");

                    // Retornar solo el objeto Message
                    return Ok(data);
                }

                _logger.LogWarning("⚠️ mensaje es NULL");
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                _logger.LogError($"❌ StackTrace: {ex.StackTrace}");
                return Ok(); // Siempre 200 para Meta
            }
        }

    }
}