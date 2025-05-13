using Etiquetado_Notificaciones.Controller;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace Etiquetado_Notificaciones
{
    public class Program
    {
        private static readonly ILogger<Program> _logger;
        private static readonly ILoggerFactory _loggerFactory;
        private static Timer? _timer;

        // Constructor estático para inicializar _logger
        static Program()
        {
            Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Information()
                    .WriteTo.Console()
                    .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

            _loggerFactory = LoggerFactory.Create(builder => builder.AddSerilog());
            _logger = _loggerFactory.CreateLogger<Program>();
        }

        static async Task Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                _logger.LogInformation("Recibida señal de cancelación (Ctrl+C)");
                cts.Cancel();
                e.Cancel = true;
            };
            await RunAsync(cts.Token);
        }

        private static async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                int minutos;
                if (!int.TryParse(ConfigurationManager.AppSettings["Minutos"], out minutos) || minutos <= 0)
                {
                    _logger.LogWarning("Clave 'Minutos' no válida en App.config. Usando valor predeterminado: 1 minuto");
                    minutos = 1;
                }

                _logger.LogInformation("Iniciando timer con intervalo de {Minutos} minutos", minutos);
                _timer = new Timer(async (_) => await obtienePendientes(), null, TimeSpan.Zero, TimeSpan.FromMinutes(minutos));

                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Aplicación cancelada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en la inicialización del programa");
            }
            finally
            {
                _timer?.Dispose();
                _logger.LogInformation("Timer detenido");
            }
        }

        private static async Task obtienePendientes()
        {
            try
            {
                _logger.LogInformation("Rescatando producción pendiente a las {Fecha}", DateTime.Now);
                DatosSql dataSql = new DatosSql(_loggerFactory);
                await dataSql.ObtenerProduccionPendiente(_logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener producción pendiente");
                throw;
            }
        }
    }
}