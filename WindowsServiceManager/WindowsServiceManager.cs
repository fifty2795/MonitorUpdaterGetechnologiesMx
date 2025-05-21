using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceProcess;
using log4net;

namespace ExamenAppConsola.WindowsServiceManager
{
    public class WindowsServiceManager
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WindowsServiceManager));
        
        /// <summary>
        /// Inicia un servicio de Windows por su nombre.
        /// </summary>
        public void StartService(string serviceName, TimeSpan timeout)
        {
            try
            {
                using var service = new ServiceController(serviceName);
                if (service.Status != ServiceControllerStatus.Running && service.Status != ServiceControllerStatus.StartPending)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    Log.Info($"Servicio '{serviceName}' iniciado.");                    
                }                
            }
            catch (Exception ex)
            {
                Log.Error($"Error al iniciar el servicio '{serviceName}': {ex.Message}");                                
                throw;
            }
        }

        /// <summary>
        /// Detiene un servicio de Windows por su nombre.
        /// </summary>
        public void StopService(string serviceName, TimeSpan timeout)
        {
            try
            {
                using var service = new ServiceController(serviceName);
                if (service.Status != ServiceControllerStatus.Stopped && service.Status != ServiceControllerStatus.StopPending)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    Log.Info($"Servicio '{serviceName}' detenido.");                    
                }                
            }
            catch (Exception ex)
            {
                Log.Error($"Error al detener el servicio '{serviceName}': {ex.Message}");                
                throw;
            }
        }

        /// <summary>
        /// Obtiene el estado actual del servicio.
        /// </summary>
        public ServiceControllerStatus GetServiceStatus(string serviceName)
        {
            try
            {
                using var service = new ServiceController(serviceName);
                return service.Status;
            }
            catch (Exception ex)
            {
                Log.Error($"Error al obtener el estado del servicio '{serviceName}': {ex.Message}");                
                throw;
            }
        }
    }
}
