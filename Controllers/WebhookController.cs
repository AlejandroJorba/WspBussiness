using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using WspBussiness.Models;

namespace WspBussiness.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private const string VERIFY_TOKEN = "chinchulin";
        private const string phoneNumberId = "1533339991147357";
        private const string token = "EAAjd2zosSf4BQL0SoRTGmp7zELiL1ZBQZB2iTVV6NDt7EEZAdhMMISDZBy9b38UnOxt07L9Y4tWfZCjHbafNIPoWwxAZAz6wWFKf2KdPfDr5ZBtSZAZCkvF9jVOe0ZAWaHKMVByABmpLyJJLovFw4D0qoBmhG2sJfeX9eJcrdXO60cRLzK2RHotnCgZBMHOfK9fJUBxnGTIHq48XpL9a7f0dI9LaMMx6NXNewae";
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
                    var nombre = usuario?.Profile?.Name ?? "amigo";
                    var numero = mensaje.From;

                    // 👉 Enviar plantilla genérica
                    await EnviarPlantillaAsync(
                        numero,
                        nombre,
                        "opciones_iniciales" // nombre de tu plantilla
                    );

                    var data = new WebhookResponse
                    {
                        Nombre = nombre,
                        Mensaje = mensaje.Text?.Body,
                        Horario = DateTimeOffset.FromUnixTimeSeconds(long.Parse(mensaje.Timestamp)).DateTime,
                        Numero = numero
                    };

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


        private async Task EnviarPlantillaAsync(string numero, string nombrePersona, string nombrePlantilla)
        {
            try
            {
                var url = $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";

                var payload = new
                {
                    messaging_product = "whatsapp",
                    to = numero,
                    type = "template",
                    template = new
                    {
                        name = nombrePlantilla,
                        language = new { code = "es_AR" },
                        components = new[]
                        {
                    new {
                        type = "body",
                        parameters = new[]
                        {
                            new { type = "text", text = nombrePersona }
                        }
                    }
                }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await http.PostAsync(url, content);

                var resultado = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("📤 Respuesta de WhatsApp: " + resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError("❌ Error enviando plantilla: " + ex.Message);
            }
        }


        private async Task EnviarTextoAsync(string numero, string texto)
        {
            var url = $"https://graph.facebook.com/v20.0/{phoneId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                to = numero,
                type = "text",
                text = new { body = texto }
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }

    }
}