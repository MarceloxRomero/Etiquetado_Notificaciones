using Etiquetado_Notificaciones.Controller;
using Etiquetado_Notificaciones.Modelo;
using ServiceActualizaFechaCodificado;
using ServiceNotificacion;
using ServiceReferenceFQ;
using ServiceReferenceMB;
using ServiceReferenceMoverUmas;
using ServiceReferenceOT;
using System.Configuration;
using System.ServiceModel;

namespace Etiquetado_Notificaciones.Connected_Services.SAP
{
    public class ProcesosSAP
    {
        //Variables Servicio SAP
        private zpp_notificacionesClient _NotificacionesClient;
        private ZWS_ETIQUETADO0006Client _fechaCodificadoClient;
        private ZPP_HU_LI_FQClient _LoteInspeccionFQClient;
        private ZPP_HU_LI_MBClient _LoteInspeccionMBClient;
        private zmm_fnc_mover_umasClient _MoverUmasClient;
        private ZWS_ETIQUETADO0008Client _MovimientoUbicacionClient;


        //Variables SAP
        private string UsuarioSap;
        private string PasswordSap;
        private string loteOrigen;
        private string wMod;
        private string wAlmacen;
        private string wBwlvs;
        private string wNltyp;
        private string wNlpla;
        private string wSquit;

        //Variables Lotes FQ
        private string GeneraQM;

        //Variable Base de datos
        DatosSql data = new DatosSql();


        public ProcesosSAP()
        {
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.Count == 0)
            {
                throw new ConfigurationErrorsException("No se encontraron configuraciones en el archivo de configuración.");
            }
            UsuarioSap = ConfigurationManager.AppSettings["UsuarioSap"];
            PasswordSap = ConfigurationManager.AppSettings["PasswordSap"];
            loteOrigen = ConfigurationManager.AppSettings["LoteOrigen"];
            wMod = ConfigurationManager.AppSettings["WMod"];
            wAlmacen = ConfigurationManager.AppSettings["Almacen"];
            wBwlvs = ConfigurationManager.AppSettings["Movimiento"];
            wNltyp = ConfigurationManager.AppSettings["TipoAlm"];
            wNlpla = ConfigurationManager.AppSettings["Ubicacion"];
            wSquit = ConfigurationManager.AppSettings["Confirmacion"];
            GeneraQM = ConfigurationManager.AppSettings["GeneraFQ"];

        }

        public async Task ServicioSap_notificacion(EtiqProduccion notificacion)
        {
            try
            {
                if (notificacion == null)
                {
                    throw new ArgumentNullException(nameof(notificacion), "El objeto produccion no puede ser nulo.");
                }

                _NotificacionesClient = new zpp_notificacionesClient();
                _NotificacionesClient.ClientCredentials.UserName.UserName = UsuarioSap;
                _NotificacionesClient.ClientCredentials.UserName.Password = PasswordSap;


                var envase = notificacion.Material + "/" + notificacion.Paletizadora + "/" + notificacion.CorreLinea;

                string wFechaCodificado = notificacion.fechaSemi.Date == new DateTime(1900, 1, 1) ? notificacion.fechaCodificado.ToString("yyyy-MM-dd") : notificacion.fechaSemi.ToString("yyyy-MM-dd");
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

                var response = await _NotificacionesClient.ZppHuNotifBapiEAsync(request);

                if (response == null)
                {
                    Program.errorLogger.LogError("El servicio devolvió una respuesta nula.");
                }

                string[] errores = {
                                    response.ZppHuNotifBapiEResponse.Err.ToString(),
                                    response.ZppHuNotifBapiEResponse.Err2.ToString(),
                                    response.ZppHuNotifBapiEResponse.Err3.ToString(),
                                    response.ZppHuNotifBapiEResponse.Err4.ToString(),
                                    response.ZppHuNotifBapiEResponse.Err5.ToString(),
                                    response.ZppHuNotifBapiEResponse.Err6.ToString(),
                                    response.ZppHuNotifBapiEResponse.Err7.ToString(),
                                    response.ZppHuNotifBapiEResponse.ErrNotif.ToString(),
                                    };

                string documentoGenerado = response.ZppHuNotifBapiEResponse.DocV;


                if (!string.IsNullOrEmpty(documentoGenerado))
                {
                    Console.WriteLine($"Actualiza registro UMA : {notificacion.Uma} con documento : {documentoGenerado}");
                    bool okProduccion = await data.ActualizarProduccion(notificacion.Uma, documentoGenerado);

                    if (GeneraQM == "X")
                    {
                        //Genera Lotes FQ
                        bool okFQ = await GeneraLotesFQ(notificacion, documentoGenerado);
                        await Task.Delay(2000);
                        //Genera Lotes MB
                        bool okMB = await GeneraLotesMB(notificacion, documentoGenerado);
                        await Task.Delay(2000);
                    }

                    //Actualiza Fecha Codificado
                    bool fechaCod = await ServicioActualizaFechaCodificado(notificacion);
                    await Task.Delay(5000);
                    //Realiza Movimiento BW05
                    bool mov = await RealizaMovimiento(notificacion);
                    await Task.Delay(6000);
                    //Genera OT de Almacenamiento
                    await GeneraOTAlmacenamiento(notificacion);

                }
                else
                {
                    foreach (string error in errores)
                    {
                        if (!string.IsNullOrEmpty(error.Trim()) && error != "0")
                        {
                            Program.errorLogger.LogError($"Error al notificar número error: {error}");
                        }
                    }
                }

            }
            catch (CommunicationException commEx)
            {
                Program.errorLogger.LogError($"Error de comunicación en ServicioSap_notificacion: {commEx.Message}");
            }
            catch (TimeoutException timeoutEx)
            {
                Program.errorLogger.LogError($"Error de tiempo de espera: {timeoutEx.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
        }

        private async Task GeneraOTAlmacenamiento(EtiqProduccion notificacion)
        {
            try
            {
                _MovimientoUbicacionClient = new ZWS_ETIQUETADO0008Client();
                _MovimientoUbicacionClient.ClientCredentials.UserName.UserName = UsuarioSap;
                _MovimientoUbicacionClient.ClientCredentials.UserName.Password = PasswordSap;

                var uma = notificacion.Uma.PadLeft(20, '0');

                var request = new ZwsPpEtiquetado0008
                {
                    WUma = uma, //Unidad de manipulación
                    WBwlvs = wBwlvs,         //Cl.movim.gestión almacenes   999
                    WNltyp = wNltyp,         //Tipo almacén destino         D15
                    WNlpla = wNlpla,         //Ubicación de destino         D-32
                    WSquit = wSquit,         //Indicador: Confirmación de una posición de OT X
                    WTipo = "OT"             //Genera OT de almacenamiento
                };

                var response = await _MovimientoUbicacionClient.ZwsPpEtiquetado0008Async(request);

                if (response == null)
                {
                    Program.errorLogger.LogError("El servicio devolvió una respuesta nula.");
                }

                if (!string.IsNullOrEmpty(response.ZwsPpEtiquetado0008Response.WTanum))
                {
                    Console.WriteLine($"OT generada con número : {response.ZwsPpEtiquetado0008Response.WTanum}");
                }
            }
            catch (CommunicationException commEx)
            {
                Program.errorLogger.LogError($"Error de comunicación en GeneraOTAlmacenamiento: {commEx.Message}");
            }
            catch (TimeoutException timeoutEx)
            {
                Program.errorLogger.LogError($"Error de tiempo de espera: {timeoutEx.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
        }

        private async Task<bool> RealizaMovimiento(EtiqProduccion notificacion)
        {
            try
            {
                _MoverUmasClient = new zmm_fnc_mover_umasClient();
                _MoverUmasClient.ClientCredentials.UserName.UserName = UsuarioSap;
                _MoverUmasClient.ClientCredentials.UserName.Password = PasswordSap;

                var uma = notificacion.Uma.PadLeft(20,'0');

                var request = new ZmmWbsMovimientos
                {
                    WUma = uma,
                    WLgort = wAlmacen,
                };
                var response = await _MoverUmasClient.ZmmWbsMovimientosAsync(request);

                if (response == null)
                {
                    Program.errorLogger.LogError("El servicio devolvió una respuesta nula.");
                }

                if (string.IsNullOrEmpty(response.ZmmWbsMovimientosResponse.WDoc))
                {
                    Program.errorLogger.LogError($"No fue posible generar el movimiento a uma : {notificacion.Uma}");
                }
                else
                {
                    Console.WriteLine($"Movimiento generado con documento : {response.ZmmWbsMovimientosResponse.WDoc}");
                    return true;
                }

            }
            catch (CommunicationException commEx)
            {
                Program.errorLogger.LogError($"Error de comunicación en RealizaMovimiento: {commEx.Message}");
            }
            catch (TimeoutException timeoutEx)
            {
                Program.errorLogger.LogError($"Error de tiempo de espera: {timeoutEx.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> GeneraLotesMB(EtiqProduccion notificacion, string documentoGenerado)
        {
            try
            {
                if (notificacion == null)
                {
                    throw new ArgumentNullException(nameof(notificacion), "El objeto xmlRequest no puede ser nulo.");
                }

                _LoteInspeccionMBClient = new ZPP_HU_LI_MBClient();
                _LoteInspeccionMBClient.ClientCredentials.UserName.UserName = UsuarioSap;
                _LoteInspeccionMBClient.ClientCredentials.UserName.Password = PasswordSap;

                var request = new ZppHuLiMb
                {
                    WOri = loteOrigen,
                    WNdoc = documentoGenerado,
                    WExidv = notificacion.Uma,
                    WFechai = Convert.ToDateTime(notificacion.Fecha).ToString("yyyy-MM-dd"),
                    WHora = notificacion.Hora,
                    WHoraSpecified = true,
                    WPalet = notificacion.Paletizadora.ToString(),
                    WOp = notificacion.NOrdPrev,
                };

                var retorno = await _LoteInspeccionMBClient.ZppHuLiMbAsync(request);

                if (retorno == null)
                {
                    Program.errorLogger.LogError("El servicio devolvió una respuesta nula.");
                }


                // Obtener el primer valor no vacío de message_v2
                var loteMB = retorno.ZppHuLiMbResponse.Ret.FirstOrDefault(item => !string.IsNullOrEmpty(item.MessageV2))?.MessageV2;

                if (string.IsNullOrEmpty(loteMB) || loteMB.All(c => c == '0'))
                {
                    loteMB = retorno.ZppHuLiMbResponse.WLi;
                }

                if (loteMB == null)
                {
                    Program.errorLogger.LogError("El servicio FQ devolvió una respuesta nula.");
                }
                else
                {
                    DatosSql sqlData = new DatosSql();
                    await sqlData.ActualizaLoteInspeccionMB(notificacion, loteMB);
                    Console.WriteLine($"Acatulizando Registro MB de {notificacion.Uma} con lote FQ {loteMB}");
                    return true;
                }


            }
            catch (CommunicationException commEx)
            {
                Program.errorLogger.LogError($"Error de comunicación en GeneraLotesMB: {commEx.Message}");
            }
            catch (TimeoutException timeoutEx)
            {
                Program.errorLogger.LogError($"Error de tiempo de espera: {timeoutEx.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
            return false;
        }

        private async Task<bool> GeneraLotesFQ(EtiqProduccion notificacion, string documentoGenerado)
        {
            try
            {
                if (notificacion == null)
                {
                    throw new ArgumentNullException(nameof(notificacion), "El objeto produccion no puede ser nulo.");
                }

                _LoteInspeccionFQClient = new ZPP_HU_LI_FQClient();
                _LoteInspeccionFQClient.ClientCredentials.UserName.UserName = UsuarioSap;
                _LoteInspeccionFQClient.ClientCredentials.UserName.Password = PasswordSap;

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
                    WModSpecified = true,
                };

                var retorno = await _LoteInspeccionFQClient.ZppHuLiFqAsync(request);
                

                if (retorno == null)
                {
                    Program.errorLogger.LogError("El servicio devolvió una respuesta nula.");
                }

                // Recorrer e imprimir todos los elementos de ret
                var ret = retorno.ZppHuLiFqResponse.Ret;

                // Obtener el primer valor no vacío de message_v2
                var loteFQ = ret.FirstOrDefault(item => !string.IsNullOrEmpty(item.MessageV2))?.MessageV2;

                if (string.IsNullOrEmpty(loteFQ) || loteFQ.All(c => c == '0'))
                {
                    loteFQ = retorno.ZppHuLiFqResponse.WLi;
                }

                if (loteFQ == null)
                {
                    Program.errorLogger.LogError("El servicio FQ devolvió una respuesta nula.");
                    return false;
                }
                else
                {
                    DatosSql sqlData = new DatosSql();
                    await sqlData.ActualizarLoteInespccionFQ(notificacion, loteFQ);
                    Console.WriteLine($"Acatulizando Registro FQ de {notificacion} con lote FQ {loteFQ}");
                    return true;
                }

            }
            catch (CommunicationException commEx)
            {
                Program.errorLogger.LogError($"Error de comunicación en GeneraLotesFQ: {commEx.Message}");
            }
            catch (TimeoutException timeoutEx)
            {
                Program.errorLogger.LogError($"Error de tiempo de espera: {timeoutEx.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> ServicioActualizaFechaCodificado(EtiqProduccion notificacion)
        {
            try
            {

                _fechaCodificadoClient = new ZWS_ETIQUETADO0006Client();
                _fechaCodificadoClient.ClientCredentials.UserName.UserName = UsuarioSap;
                _fechaCodificadoClient.ClientCredentials.UserName.Password = PasswordSap;

                var request = new ZwsPpEtiquetado0006
                {
                    WExidv = notificacion.Uma,
                    WFechaSemi = notificacion.fechaCodificado.ToString("yyyy-MM-dd"),
                    WTipo = "AN"
                };

                var response = await _fechaCodificadoClient.ZwsPpEtiquetado0006Async(request);

                if (response == null)
                {
                    Program.errorLogger.LogError($"El servicio devolvió una respuesta nula en la fecha codificado en uma {notificacion.Uma}");
                }

                await data.ActualizarFechaCodificado(notificacion.Uma, notificacion.fechaCodificado);
                return true;
            }
            catch (CommunicationException commEx)
            {
                Program.errorLogger.LogError($"Error de comunicación en ServicioActualizaFechaCodificado: {commEx.Message}");
            }
            catch (TimeoutException timeoutEx)
            {
                Program.errorLogger.LogError($"Error de tiempo de espera: {timeoutEx.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
            return false;
        }
    }
}
