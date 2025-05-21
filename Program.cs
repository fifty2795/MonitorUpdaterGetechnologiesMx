using ExamenAppConsola.MonitorUpdaterManager;
using log4net.Config;

XmlConfigurator.Configure();

string monitorSource = @"C:\Users\aleja\OneDrive\Escritorio\Examen\TestUpdatePackage";
string installFolder = @"C:\Users\aleja\OneDrive\Escritorio\Examen\Backup";
string version = "1.2.3";

MonitorUpdaterManager.UpdateMonitor(monitorSource, installFolder, version);