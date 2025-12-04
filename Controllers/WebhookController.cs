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
        private const string phoneNumberId = "901966789667192";
        private const string token = "";
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

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var webhook = JsonSerializer.Deserialize<WhatsappResponse>(body, options);

                var usuario = webhook?.Entry?[0]?.Changes?[0]?.Value?.Contacts?[0];
                var mensaje = webhook?.Entry?[0]?.Changes?[0]?.Value?.Messages?[0];

                if (mensaje == null)
                    return Ok(); // Meta siempre pide 200

                string nombre = usuario?.Profile?.Name ?? "";
                string from = mensaje.From;

                // ============================================
                // 🟦 1️⃣ SI TOCÓ UN BOTÓN (Botón de plantilla)
                // ============================================
                if (mensaje.Button != null)
                {
                    string opcion = mensaje.Button.Text ?? mensaje.Button.Payload;
                    _logger.LogInformation($"➡️ BOTÓN SELECCIONADO: {opcion}");

                    if (opcion == "Realizar un pedido")
                    {
                        await EnviarPlantillaAsync(from, nombre, "plantilla_pedir_datos");
                        return Ok();
                    }

                    if (opcion == "Ver estado de pedido")
                    {
                        await EnviarTextoAsync(from, "Por favor enviá el número del pedido 🧾");
                        return Ok();
                    }
                }

                // ============================================
                // 🟩 2️⃣ MENSAJE DE TEXTO NORMAL
                //    (si contiene la palabra "pedido")
                // ============================================
                string texto = mensaje.Text?.Body?.ToLower() ?? "";

                if (texto.Contains("pedido"))
                {
                    _logger.LogInformation("📌 El usuario mencionó 'pedido', se envía la plantilla inicial.");

                    await EnviarPlantillaAsync(
                        from,
                        nombre,
                        "opciones_iniciales" // ESTA ES TU PLANTILLA DE INICIO
                    );

                    return Ok();
                }

                // Si no contiene "pedido", no hacer nada
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                _logger.LogError(ex.StackTrace);
                return Ok();
            }
        }

        private async Task EnviarPlantillaAsync(string numero, string nombrePersona, string nombrePlantilla)
        {
            try
            {
                var url = $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";
                _logger.LogInformation("📤 La plantilla se va a enviar al numero: " + numero);

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
            var url = $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";

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