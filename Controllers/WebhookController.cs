using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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

                _logger.LogInformation($"Mensaje recibido: {body}");

                // Procesar el mensaje aquí
                var json = JsonDocument.Parse(body);

                return Ok(); // Meta solo necesita un 200 OK
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error procesando webhook: {ex.Message}");
                return Ok(); // Igual devolver 200 para no reintentos
            }
        }
    }
}