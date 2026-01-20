using System.Text;
using System.Text.Json;

namespace Consumer.Workers
{
    public class HeartbeatWorker : BackgroundService
    {
        private readonly ILogger<HeartbeatWorker> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly Guid ConsumerId = Guid.NewGuid();

        public HeartbeatWorker(IHttpClientFactory factory, ILogger<HeartbeatWorker> logger)
        {
            _httpFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = _httpFactory.CreateClient("ApiClient");

                var payload = JsonSerializer.Serialize(ConsumerId);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                try
                {
                    await client.PostAsync("producer/consumer/register", content, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // cierre ordenado, salir del bucle
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error enviando heartbeat de registro");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        // Finalizador asíncrono que se ejecuta al detener la aplicación
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("HeartbeatWorker stopping. Enviando unregister para {ConsumerId}", ConsumerId);

            // Limitar el tiempo que dedicamos al "finalize" para no bloquear el shutdown indefinidamente
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                var client = _httpFactory.CreateClient("ApiClient");

                // Serializar el GUID como JSON válido para el unregister también
                var payload = JsonSerializer.Serialize(ConsumerId);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                // Enviar petición de "unregister" durante el apagado
                await client.PostAsync("producer/consumer/unregister", content, cts.Token);

                _logger.LogInformation("Unregister enviado correctamente para {ConsumerId}", ConsumerId);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogWarning("El unregister fue cancelado por timeout o por cierre.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar unregister durante el apagado.");
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
