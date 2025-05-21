using ExamenAppConsola.FileManagerSample;
using System.Diagnostics;
using log4net;

namespace ExamenAppConsola.MonitorUpdaterManager
{
    public class MonitorUpdaterManager
    {
        #region Constantes

        //private const string MonitorServiceNameKey = "monitorsk";
        private const string MonitorServiceNameKey = "wuauserv";
        public const string UpdaterMonitorInstallationFolder = "monSelfUpdater";
        const string MonitorUpdatesPath = "/tmp";
        public const string UpdaterMonitorFolder = "actualizaciones";
        public static string InstalledRollbackFilesPath = "/tmp";

        private static readonly ILog Log = LogManager.GetLogger(typeof(MonitorUpdaterManager));

        #endregion
        
        #region Metodos

        public static void UpdateMonitor(string monitorFilesLocation, string installationFolder, string version)
        {
            try
            {                
                var winServiceManager = new WindowsServiceManager.WindowsServiceManager();
                TimeSpan timeout = TimeSpan.FromSeconds(30);                
                winServiceManager.StopService(MonitorServiceNameKey, timeout);
                Log.Info("Iniciando las actualizaciones al monitor...");

                try
                {
                    Process[] processes = Process.GetProcessesByName("psample");
                    if (processes.Any())
                    {
                        Log.Info("Cerrando el monitor de actualizaciones...");
                        foreach (var proc in processes)
                        {
                            proc.Kill();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Ocurrió un error al intentar terminar el proceso del monitor de actualizaciones.", ex);
                }

                var backupPath = Path.Combine(MonitorUpdatesPath, "Backup", Guid.NewGuid().ToString().Replace("-", "").Substring(0, 6));
                if (!Directory.Exists(backupPath))
                {
                    try
                    {
                        Directory.CreateDirectory(backupPath);
                    }
                    catch
                    {
                        backupPath = Path.Combine(Path.GetTempPath(), UpdaterMonitorFolder, "Backup", Guid.NewGuid().ToString().Replace("-", "").Substring(0, 6));
                        if (!Directory.Exists(backupPath))
                        {
                            Directory.CreateDirectory(backupPath);
                        }
                    }
                }

                var fileManager = new FileManager();
                var result = fileManager.UpdateFiles(monitorFilesLocation.Trim('"'), installationFolder.Trim('"'), backupPath);                
                bool updateError = false;

                if (!string.IsNullOrEmpty(result))
                {
                    updateError = true;
                    Log.Error(result);
                    result = null;
                }

                if (updateError)
                {
                    Log.Info("Realizando rollback de las actualizaciones al monitor...");
                    result = fileManager.UpdateFiles(backupPath, installationFolder);
                    fileManager.RemoveDirectoryContents(backupPath);

                    if (!string.IsNullOrEmpty(result))
                    {
                        Log.Info("MonitorUpdater: " + result);
                    }
                    else
                    {
                        Log.Info("Terminado rollback de las actualizaciones al monitor...");
                    }

                    winServiceManager.StartService(MonitorServiceNameKey, timeout);

                    return;
                }

                fileManager.RemoveDirectoryContents(backupPath);
                fileManager.RemoveDirectoryContents(monitorFilesLocation.Trim('"'));
                Directory.Delete(backupPath, true);
                Directory.Delete(monitorFilesLocation.Trim('"'), true);

                ReleaseUpdateMonitorTask();
                Log.Info("Actualizaciones al monitor terminadas...");

                winServiceManager.StartService(MonitorServiceNameKey, timeout);
            }
            catch (Exception ex)
            {
                Log.Error("Ocurrió un error durante el proceso de actualización del Monitor.", ex);
            }
        }

        private static void ReleaseUpdateMonitorTask()
        {            
            Log.Info("ReleaseUpdateMonitorTask ejecutado.");
        }
        #endregion
    }
}
