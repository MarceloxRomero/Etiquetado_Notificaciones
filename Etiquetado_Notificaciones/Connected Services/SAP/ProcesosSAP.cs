using Polly;
using Polly.Retry;
using Etiquetado_Notificaciones.Controller;
using Etiquetado_Notificaciones.Modelo;
using ServiceActualizaFechaCodificado;
using ServiceNotificacion;
using ServiceReferenceFQ;
using ServiceReferenceMB;
using ServiceReferenceMoverUmas;
using ServiceReferenceOT;
using System;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Etiquetado_Notificaciones.Connected_Services.SAP
{
    public class ProcesosSAP : IDisposable
    {
        private bool disposed = false;

        private readonly ILogger<ProcesosSAP> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly DatosSql data;
        private readonly zpp_notificacionesClient _NotificacionesClient;
        private readonly ZWS_ETIQUETADO0006Client _fechaCodificadoClient;
        private readonly ZPP_HU_LI_FQClient _LoteInspeccionFQClient;
        private readonly ZPP_HU_LI_MBClient _LoteInspeccionMBClient;
        private readonly zmm_fnc_mover_umasClient _MoverUmasClient;
        private readonly ZWS_ETIQUETADO0008Client _MovimientoUbicacionClient;

        private string UsuarioSap;
        private string PasswordSap;
        private string loteOrigen;
        private string wMod;
        private string wAlmacen;
        private string wBwlvs;
        private string wNltyp;
        private string wNlpla;
        private string wSquit;
        private string GeneraQM;

        public ProcesosSAP(ILoggerFactory loggerFactory = null,ILogger < ProcesosSAP> logger = null)
        {
            _logger = logger ?? new NullLogger<ProcesosSAP>();
            data = new DatosSql(loggerFactory);

            _NotificacionesClient = new zpp_notificacionesClient();
            _fechaCodificadoClient = new ZWS_ETIQUETADO0006Client();
            _LoteInspeccionFQClient = new ZPP_HU_LI_FQClient();
            _LoteInspeccionMBClient = new ZPP_HU_LI_MBClient();
            _MoverUmasClient = new zmm_fnc_mover_umasClient();
            _MovimientoUbicacionClient = new ZWS_ETIQUETADO0008Client();

            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.Count == 0)
            {
                throw new ConfigurationErrorsException("No se encontraron configuraciones en el archivo de configuración.");
            }
            UsuarioSap = appSettings["UsuarioSap"];
            PasswordSap = appSettings["PasswordSap"];
            loteOrigen = appSettings["LoteOrigen"];
            wMod = appSettings["WMod"];
            wAlmacen = appSettings["Almacen"];
            wBwlvs = appSettings["Movimiento"];
            wNltyp = appSettings["TipoAlm"];
            wNlpla = appSettings["Ubicacion"];
            wSquit = appSettings["Confirmacion"];
            GeneraQM = appSettings["GeneraFQ"];

            ConfigureClientCredentials<zpp_notificacionesClient, zpp_notificaciones>(_NotificacionesClient, UsuarioSap, PasswordSap);
            ConfigureClientCredentials<ZWS_ETIQUETADO0006Client, ZWS_ETIQUETADO0006>(_fechaCodificadoClient, UsuarioSap, PasswordSap);
            ConfigureClientCredentials<ZPP_HU_LI_FQClient, ZPP_HU_LI_FQ>(_LoteInspeccionFQClient, UsuarioSap, PasswordSap);
            ConfigureClientCredentials<ZPP_HU_LI_MBClient, ZPP_HU_LI_MB>(_LoteInspeccionMBClient, UsuarioSap, PasswordSap);
            ConfigureClientCredentials<zmm_fnc_mover_umasClient, zmm_fnc_mover_umas>(_MoverUmasClient, UsuarioSap, PasswordSap);
            ConfigureClientCredentials<ZWS_ETIQUETADO0008Client, ZWS_ETIQUETADO0008>(_MovimientoUbicacionClient, UsuarioSap, PasswordSap);

            _retryPolicy = Policy
                .Handle<CommunicationException>()
                .Or<TimeoutException>()
                .Or<InvalidOperationException>(ex => ex.Message.Contains("respuesta nula"))
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (exception, timeSpan, attempt, context) =>
                    {
                        _logger.LogWarning($"Intento {attempt} fallido: {exception.Message}. Reintentando en {timeSpan.TotalSeconds}s.");
                    });
        }

        private void ConfigureClientCredentials<TClient, TChannel>(TClient client, string usuario, string password)
            where TClient : ClientBase<TChannel>
            where TChannel : class
        {
            client.ClientCredentials.UserName.UserName = usuario;
            client.ClientCredentials.UserName.Password = password;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, string operationName, string uma = null)
        {
            try
            {
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var result = await action();
                    if (result == null)
                    {
                        throw new InvalidOperationException("El servicio devolvió una respuesta nula.");
                    }
                    return result;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en {operationName} para UMA {uma ?? "desconocida"}: {ex.Message}");
                throw;
            }
        }

        public async Task ServicioSap_notificacion(EtiqProduccion notificacion)
        {
            if (notificacion == null)
            {
                throw new ArgumentNullException(nameof(notificacion), "El objeto notificación no puede ser nulo.");
            }

            var envase = $"{notificacion.Material}/{notificacion.Paletizadora}/{notificacion.CorreLinea}";
            string wFechaCodificado = notificacion.fechaSemi.Date == new DateTime(1900, 1, 1)
                ? notificacion.fechaCodificado.ToString("yyyy-MM-dd")
                : notificacion.fechaSemi.ToString("yyyy-MM-dd");

            var request = new ZppHuNotifBapiE
            {
                W1PackMat = data.Material_embalaje,
                W1HuExid = notificacion.Uma,
                W1Plant = notificacion.Centro,
                W3PackQty = notificacion.Cantidad.ToString(),
                W3PackQtySpecified = true,
                W3Material = notificacion.Material,
                W3Batch = notificacion.Lote,
                W3Plant2 = data.Planta,
                W3StgeLoc2 = notificacion.Almacen,
                W2Verid = notificacion.VersionF,
                W2Budat = notificacion.Fecha.ToString("yyyy-MM-dd"),
                W2Plnum = notificacion.NOrdPrev,
                W2Plwerk = data.Planta,
                W2Hora = notificacion.Hora.Replace(":", ""),
                WEnvase = envase,
                WPalet = notificacion.Paletizadora.ToString(),
                WFechaSemi = wFechaCodificado
            };

            try
            {
                var response = await ExecuteWithRetryAsync(
                    () => _NotificacionesClient.ZppHuNotifBapiEAsync(request),
                    "ServicioSap_notificacion",
                    notificacion.Uma);

                string documentoGenerado = response.ZppHuNotifBapiEResponse.DocV;

                if (!string.IsNullOrEmpty(documentoGenerado))
                {
                    _logger.LogInformation("Actualiza registro UMA: {Uma} con documento: {Documento}", notificacion.Uma, documentoGenerado);
                    bool okProduccion = await data.ActualizarProduccion(notificacion.Uma, documentoGenerado);

                    if (GeneraQM == "X")
                    {
                        bool okFQ = await GeneraLotesFQ(notificacion, documentoGenerado);
                        await Task.Delay(2000);
                        bool okMB = await GeneraLotesMB(notificacion, documentoGenerado);
                        await Task.Delay(2000);
                    }

                    bool fechaCod = await ServicioActualizaFechaCodificado(notificacion);
                    await Task.Delay(5000);
                    bool mov = await RealizaMovimiento(notificacion);
                    await Task.Delay(6000);
                    await GeneraOTAlmacenamiento(notificacion);
                }
                else
                {
                    string[] errores = {
                        response.ZppHuNotifBapiEResponse.Err.ToString(),
                        response.ZppHuNotifBapiEResponse.Err2.ToString(),
                        response.ZppHuNotifBapiEResponse.Err3.ToString(),
                        response.ZppHuNotifBapiEResponse.Err4.ToString(),
                        response.ZppHuNotifBapiEResponse.Err5.ToString(),
                        response.ZppHuNotifBapiEResponse.Err6.ToString(),
                        response.ZppHuNotifBapiEResponse.Err7.ToString(),
                        response.ZppHuNotifBapiEResponse.ErrNotif.ToString()
                    };

                    foreach (var error in errores)
                    {
                        if (!string.IsNullOrEmpty(error.Trim()) && error != "0")
                        {
                            _logger.LogError("Error al notificar UMA {Uma}: {Error}", notificacion.Uma, error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en ServicioSap_notificacion para UMA {Uma}", notificacion.Uma);
            }
        }

        private async Task GeneraOTAlmacenamiento(EtiqProduccion notificacion)
        {
            var uma = notificacion.Uma.PadLeft(20, '0');
            var request = new ZwsPpEtiquetado0008
            {
                WUma = uma,
                WBwlvs = wBwlvs,
                WNltyp = wNltyp,
                WNlpla = wNlpla,
                WSquit = wSquit,
                WTipo = "OT"
            };

            try
            {
                var response = await ExecuteWithRetryAsync(
                    () => _MovimientoUbicacionClient.ZwsPpEtiquetado0008Async(request),
                    "GeneraOTAlmacenamiento",
                    uma);

                if (!string.IsNullOrEmpty(response.ZwsPpEtiquetado0008Response.WTanum))
                {
                    _logger.LogInformation("OT generada con número: {Tanum} para UMA {Uma}", response.ZwsPpEtiquetado0008Response.WTanum, uma);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en GeneraOTAlmacenamiento para UMA {Uma}", uma);
            }
        }

        private async Task<bool> RealizaMovimiento(EtiqProduccion notificacion)
        {
            var uma = notificacion.Uma.PadLeft(20, '0');
            var request = new ZmmWbsMovimientos
            {
                WUma = uma,
                WLgort = wAlmacen
            };

            try
            {
                var response = await ExecuteWithRetryAsync(
                    () => _MoverUmasClient.ZmmWbsMovimientosAsync(request),
                    "RealizaMovimiento",
                    uma);

                if (string.IsNullOrEmpty(response.ZmmWbsMovimientosResponse.WDoc))
                {
                    _logger.LogError("No fue posible generar el movimiento para UMA {Uma}", uma);
                    return false;
                }

                _logger.LogInformation("Movimiento generado con documento: {Documento} para UMA {Uma}", response.ZmmWbsMovimientosResponse.WDoc, uma);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en RealizaMovimiento para UMA {Uma}", uma);
                return false;
            }
        }

        private async Task<bool> GeneraLotesMB(EtiqProduccion notificacion, string documentoGenerado)
        {
            if (notificacion == null)
            {
                throw new ArgumentNullException(nameof(notificacion), "El objeto notificación no puede ser nulo.");
            }

            var request = new ZppHuLiMb
            {
                WOri = loteOrigen,
                WNdoc = documentoGenerado,
                WExidv = notificacion.Uma,
                WFechai = Convert.ToDateTime(notificacion.Fecha).ToString("yyyy-MM-dd"),
                WHora = notificacion.Hora,
                WHoraSpecified = true,
                WPalet = notificacion.Paletizadora.ToString(),
                WOp = notificacion.NOrdPrev
            };

            try
            {
                var response = await ExecuteWithRetryAsync(
                    () => _LoteInspeccionMBClient.ZppHuLiMbAsync(request),
                    "GeneraLotesMB",
                    notificacion.Uma);

                var loteMB = response.ZppHuLiMbResponse.Ret
                    .FirstOrDefault(item => !string.IsNullOrEmpty(item.MessageV2))?.MessageV2;

                if (string.IsNullOrEmpty(loteMB) || loteMB.All(c => c == '0'))
                {
                    loteMB = response.ZppHuLiMbResponse.WLi;
                }

                if (string.IsNullOrEmpty(loteMB))
                {
                    _logger.LogError("No se generó un lote MB válido para UMA {Uma}", notificacion.Uma);
                    return false;
                }

                await data.ActualizaLoteInspeccionMB(notificacion, loteMB);
                _logger.LogInformation("Actualizando registro MB de {Uma} con lote MB {LoteMB}", notificacion.Uma, loteMB);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en GeneraLotesMB para UMA {Uma}", notificacion.Uma);
                return false;
            }
        }

        private async Task<bool> GeneraLotesFQ(EtiqProduccion notificacion, string documentoGenerado)
        {
            if (notificacion == null)
            {
                throw new ArgumentNullException(nameof(notificacion), "El objeto notificación no puede ser nulo.");
            }

            var request = new ZppHuLiFq
            {
                WOri = loteOrigen,
                WNdoc = documentoGenerado,
                WExidv = notificacion.Uma,
                WFechai = Convert.ToDateTime(notificacion.Fecha).ToString("yyyy-MM-dd"),
                WHora = notificacion.Hora,
                WHoraSpecified = true,
                WPalet = notificacion.Paletizadora.ToString(),
                WOp = notificacion.NOrdPrev,
                WMod = Convert.ToInt32(wMod),
                WModSpecified = true
            };

            try
            {
                var response = await ExecuteWithRetryAsync(
                    () => _LoteInspeccionFQClient.ZppHuLiFqAsync(request),
                    "GeneraLotesFQ",
                    notificacion.Uma);

                var loteFQ = response.ZppHuLiFqResponse.Ret
                    .FirstOrDefault(item => !string.IsNullOrEmpty(item.MessageV2))?.MessageV2;

                if (string.IsNullOrEmpty(loteFQ) || loteFQ.All(c => c == '0'))
                {
                    loteFQ = response.ZppHuLiFqResponse.WLi;
                }

                if (string.IsNullOrEmpty(loteFQ))
                {
                    _logger.LogError("No se generó un lote FQ válido para UMA {Uma}", notificacion.Uma);
                    return false;
                }

                await data.ActualizarLoteInspeccionFQ(notificacion, loteFQ);
                _logger.LogInformation("Actualizando registro FQ de {Uma} con lote FQ {LoteFQ}", notificacion.Uma, loteFQ);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en GeneraLotesFQ para UMA {Uma}", notificacion.Uma);
                return false;
            }
        }

        private async Task<bool> ServicioActualizaFechaCodificado(EtiqProduccion notificacion)
        {
            if (notificacion == null)
            {
                throw new ArgumentNullException(nameof(notificacion), "El objeto notificación no puede ser nulo.");
            }

            var request = new ZwsPpEtiquetado0006
            {
                WExidv = notificacion.Uma,
                WFechaSemi = notificacion.fechaCodificado.ToString("yyyy-MM-dd"),
                WTipo = "AN"
            };

            try
            {
                var response = await ExecuteWithRetryAsync(
                    () => _fechaCodificadoClient.ZwsPpEtiquetado0006Async(request),
                    "ServicioActualizaFechaCodificado",
                    notificacion.Uma);

                await data.ActualizarFechaCodificado(notificacion.Uma, notificacion.fechaCodificado);
                _logger.LogInformation("Fecha codificado actualizada para UMA {Uma}", notificacion.Uma);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallo en ServicioActualizaFechaCodificado para UMA {Uma}", notificacion.Uma);
                return false;
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                _NotificacionesClient?.Close();
                _fechaCodificadoClient?.Close();
                _LoteInspeccionFQClient?.Close();
                _LoteInspeccionMBClient?.Close();
                _MoverUmasClient?.Close();
                _MovimientoUbicacionClient?.Close();
                disposed = true;
            }
        }
    }
}