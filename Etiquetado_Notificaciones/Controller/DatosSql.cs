using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Etiquetado_Notificaciones.Modelo;
using System.Configuration;
using Etiquetado_Notificaciones.Connected_Services.SAP;
using Etiquetado_Notificaciones.Logs;
using System.Runtime.Intrinsics.X86;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Etiquetado_Notificaciones.Controller
{
    public class DatosSql
    {
        //Variables de entorno
        private string connectionString = null;
        public string Material_embalaje { get; set; } = null;
        public string Planta { get; set; } = null;
        private double TiempoEntreCiclo { get; set; } = 0;

        ProcesosSAP procesosSAP;

        public DatosSql()
        {

            connectionString = ConfigurationManager.ConnectionStrings["ConexionRemota"].ConnectionString;
            Material_embalaje = ConfigurationManager.AppSettings["Material_embalaje"];
            Planta = ConfigurationManager.AppSettings["Planta"];
            TiempoEntreCiclo = Convert.ToDouble(ConfigurationManager.AppSettings["entreProcesos"]);

        }

        public async Task ObtenerProduccionPendiente()
        {
            try
            {
                procesosSAP = new ProcesosSAP();
                var rateLimiter = new RateLimiter(1, TimeSpan.FromSeconds(TiempoEntreCiclo));

                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(maxRetryCount: 5,
                                                                                                     maxRetryDelay: TimeSpan.FromSeconds(10),
                                                                                                     errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var notificaciones = dbContext.Produccion.Where(p => p.ExpSAP == "");
                    var cantidad = notificaciones.Count();


                    foreach (var notificacion in notificaciones)
                    {
                        await rateLimiter.WaitAsync();
                        Console.WriteLine($"Notificando UMA {notificacion.Uma}");
                        await procesosSAP.ServicioSap_notificacion(notificacion);

                    }

                }
            }
            catch (SqlException ex)
            {
                Program.errorLogger.LogError("Error de SQL: " + ex.Message);
            }
            catch (IOException ex)
            {
                Program.errorLogger.LogError("Error de IO: " + ex.Message);
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error: {ex.Message}\nStackTrace: {ex.StackTrace}\nType: {ex.GetType().FullName}");
            }
        }

        public async Task<bool> ActualizarProduccion(string uma, string documentoGenerado)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(maxRetryCount: 5,
                                                                                                     maxRetryDelay: TimeSpan.FromSeconds(10),
                                                                                                     errorNumbersToAdd: null));
                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var registro = dbContext.Produccion.Where(p => p.Uma == uma).FirstOrDefault();

                    if (registro != null)
                    {
                        registro.NDoc = documentoGenerado;
                        registro.ExpSAP = "X";
                        await dbContext.SaveChangesAsync();
                        Console.WriteLine($"Registro actualizado correctamente uma = {uma}, documento = {documentoGenerado}");
                        return true;
                    }
                    else
                    {
                        Program.errorLogger.LogError($"No se encontró el registro con UMA = {uma} ");
                        return false;
                    }
                }
            }
            catch (SqlException ex)
            {
                Program.errorLogger.LogError($"Error de SQL: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Program.errorLogger.LogError($"Error en la operación: {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                Program.errorLogger.LogError($"Error al actualizar la base de datos: {ex.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
            return false;
        }

        internal async Task<bool> ActualizarFechaCodificado(string uma, DateTime fechaCodificado)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(maxRetryCount: 5,
                                                                                                     maxRetryDelay: TimeSpan.FromSeconds(10),
                                                                                                     errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {
                    var registro = dbContext.Produccion.Where(p => p.Uma == uma).FirstOrDefault();
                    
                    if (registro == null)
                    {
                        Program.errorLogger.LogError($"No se encontró el registro con UMA = {uma} ");
                    }

                    registro.fechaCodificado = fechaCodificado;
                    await dbContext.SaveChangesAsync();
                    return true;

                }
            }
            catch (SqlException ex)
            {
                Program.errorLogger.LogError($"Error de SQL: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Program.errorLogger.LogError($"Error en la operación: {ex.Message}");
            }
            catch (DbUpdateException ex)
            {
                Program.errorLogger.LogError($"Error al actualizar la base de datos: {ex.Message}");
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error inesperado: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> ActualizarLoteInespccionFQ(EtiqProduccion notificacion, string loteFQ)
        {
            try
            {
                var obtionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                obtionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(maxRetryCount: 5,
                                                                                                     maxRetryDelay: TimeSpan.FromSeconds(10),
                                                                                                     errorNumbersToAdd: null));

                using (var dbContext = new ProduccionDBContext(obtionBuilder.Options))
                {
                    var registro = dbContext.Produccion.FirstOrDefault(p => p.Uma == notificacion.Uma);

                    if (registro != null)
                    {
                        registro.LiFq = loteFQ;

                        await dbContext.SaveChangesAsync();
                        return true;
                    }
                    else
                    {
                        Program.errorLogger.LogError($"No se encontró el registro con la UMA = {notificacion.Uma}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error al actualizar el registro FQ: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> ActualizaLoteInspeccionMB(EtiqProduccion notificacion, string loteMB)
        {
            try
            {
                var optionBuilder = new DbContextOptionsBuilder<ProduccionDBContext>();
                optionBuilder.UseSqlServer(connectionString, options => options.EnableRetryOnFailure(maxRetryCount: 5,
                                                                                                   maxRetryDelay: TimeSpan.FromSeconds(10),
                                                                                                   errorNumbersToAdd: null));
                using (var dbContext = new ProduccionDBContext(optionBuilder.Options))
                {

                    var registro = dbContext.Produccion.FirstOrDefault(p => p.Uma == notificacion.Uma);

                    if (registro != null)
                    {
                        registro.LiMb = loteMB;

                        await dbContext.SaveChangesAsync();
                        return true;
                    }
                    else
                    {
                        Program.errorLogger.LogError($"No se encontró el registro con la UMA = {notificacion.Uma}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Program.errorLogger.LogError($"Error al actualizar el registro MB: {ex.Message}");
            }
            return false;
        }
    }
}
