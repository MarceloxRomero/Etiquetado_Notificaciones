using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Etiquetado_Notificaciones.Modelo;
using System.Configuration;
using Etiquetado_Notificaciones.Connected_Services.SAP;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Etiquetado_Notificaciones.Controller
{
    public class DatosSql
    {
        // Variables de entorno
        private readonly string connectionString;
        public string Material_embalaje { get; set; }
        public string Planta { get; set; }
        private readonly double TiempoEntreCiclo;
        private readonly ILogger<DatosSql> _logger;

        private readonly ProcesosSAP procesosSAP;

        public DatosSql(ILoggerFactory loggerFactory, ILogger<DatosSql> logger = null)
        {
            _logger = logger ?? loggerFactory?.CreateLogger<DatosSql>() ?? NullLogger<DatosSql>.Instance;
            connectionString = ConfigurationManager.ConnectionStrings["ConexionRemota"]?.ConnectionString
                ?? throw new ConfigurationErrorsException("Connection string 'ConexionRemota' no configurada.");
            Material_embalaje = ConfigurationManager.AppSettings["Material_embalaje"]
                ?? throw new ConfigurationErrorsException("AppSetting 'Material_embalaje' no configurado.");
            Planta = ConfigurationManager.AppSettings["Planta"]
                ?? throw new ConfigurationErrorsException("AppSetting 'Planta' no configurado.");
            if (!double.TryParse(ConfigurationManager.AppSettings["entreProcesos"], out var tiempo) || tiempo < 0)
            {
                _logger.LogWarning("AppSetting 'entreProcesos' no válido. Usando valor predeterminado: 0");
                tiempo = 0;
            }
            TiempoEntreCiclo = tiempo;

            procesosSAP = new ProcesosSAP(this);
        }

        public async Task ObtenerProduccionPendiente(ILogger logger)
        {
            var methodLogger = logger ?? _logger;
            try
            {
                var rateLimiter = new RateLimiter(1, TimeSpan.FromSeconds(TiempoEntreCiclo));

                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var notificaciones = dbContext.Produccion.Where(p => p.ExpSAP == "");
                    var cantidad = notificaciones.Count();
                    methodLogger.LogInformation("Encontradas {Cantidad} notificaciones pendientes", cantidad);

                    foreach (var notificacion in notificaciones)
                    {
                        await rateLimiter.WaitAsync();
                        methodLogger.LogInformation("Notificando UMA {Uma}", notificacion.Uma);
                        await procesosSAP.ServicioSap_notificacion(notificacion);
                    }
                }
            }
            catch (SqlException ex)
            {
                methodLogger.LogError(ex, "Error de SQL al obtener producción pendiente: {Message}", ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                methodLogger.LogError(ex, "Error de IO al obtener producción pendiente: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                methodLogger.LogError(ex, "Error inesperado al obtener producción pendiente: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<bool> ActualizarProduccion(string uma, string documentoGenerado)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var registro = dbContext.Produccion.FirstOrDefault(p => p.Uma == uma);

                    if (registro != null)
                    {
                        registro.NDoc = documentoGenerado;
                        registro.ExpSAP = "X";
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Registro actualizado correctamente: UMA = {Uma}, Documento = {Documento}", uma, documentoGenerado);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró el registro con UMA = {Uma}", uma);
                        return false;
                    }
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error de SQL al actualizar producción para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error en la operación al actualizar producción para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error al actualizar la base de datos para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar producción para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
        }

        public async Task<bool> ActualizarFechaCodificado(string uma, DateTime fechaCodificado)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var registro = dbContext.Produccion.FirstOrDefault(p => p.Uma == uma);

                    if (registro == null)
                    {
                        _logger.LogWarning("No se encontró el registro con UMA = {Uma}", uma);
                        return false;
                    }

                    registro.fechaCodificado = fechaCodificado;
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("Fecha codificado actualizada para UMA {Uma}: {Fecha}", uma, fechaCodificado);
                    return true;
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "Error de SQL al actualizar fecha codificado para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Error en la operación al actualizar fecha codificado para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error al actualizar la base de datos para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar fecha codificado para UMA {Uma}: {Message}", uma, ex.Message);
                return false;
            }
        }

        public async Task<bool> ActualizarLoteInspeccionFQ(EtiqProduccion notificacion, string loteFQ)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var registro = dbContext.Produccion.FirstOrDefault(p => p.Uma == notificacion.Uma);

                    if (registro != null)
                    {
                        registro.LiFq = loteFQ;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Lote FQ actualizado para UMA {Uma}: {LoteFQ}", notificacion.Uma, loteFQ);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró el registro con UMA = {Uma}", notificacion.Uma);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar lote FQ para UMA {Uma}: {Message}", notificacion.Uma, ex.Message);
                return false;
            }
        }

        public async Task<bool> ActualizaLoteInspeccionMB(EtiqProduccion notificacion, string loteMB)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var registro = dbContext.Produccion.FirstOrDefault(p => p.Uma == notificacion.Uma);

                    if (registro != null)
                    {
                        registro.LiMb = loteMB;
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation("Lote MB actualizado para UMA {Uma}: {LoteMB}", notificacion.Uma, loteMB);
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("No se encontró el registro con UMA = {Uma}", notificacion.Uma);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar lote MB para UMA {Uma}: {Message}", notificacion.Uma, ex.Message);
                return false;
            }
        }
    }
}