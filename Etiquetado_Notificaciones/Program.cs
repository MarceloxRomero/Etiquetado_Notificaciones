using Etiquetado_Notificaciones.Controller;
using Etiquetado_Notificaciones.Logs;
using System.Configuration;

namespace Etiquetado_Notificaciones
{

    public class Program
    {
        public static ErrorLogger errorLogger = new ErrorLogger();
        private static Timer? _timer;


        static async Task Main(string[] args)
        {
            try
            {
                //Iniciamos los minutos del timer
                int minutos = ConfigurationManager.AppSettings["Minutos"] != null ? Convert.ToInt32(ConfigurationManager.AppSettings["Minutos"]) : 1;

                //Iniciamos el timer
                _timer = new Timer(async (_) => await obtienePendientes(), null, TimeSpan.Zero, TimeSpan.FromMinutes(minutos));

                await Task.Delay(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                errorLogger.LogError(ex.Message);
            }
        }

        private static async Task obtienePendientes()
        {
            try
            {
                DatosSql dataSql = new DatosSql();
                Console.WriteLine($"Rescatando producción pendiente del " + DateTime.Now);
                //Obtenemos la producción pendiente
                await dataSql.ObtenerProduccionPendiente();

            }
            catch (Exception ex)
            {

                throw;
            }
        }
    }
}