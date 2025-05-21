using System.Diagnostics;
using log4net;
using Microsoft.Web.XmlTransform;

namespace ExamenAppConsola.FileUpdater
{
    public class FileUpdater
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileUpdater));

        #region Constants

        private const string DeleteCommandExtension = ".del";
        private const string AddCommandExtension = ".add";
        private const string UpdateCommandExtension = ".upd";
        private const string XdtMergeCommandExtension = ".xmrg";
        private const string ExecuteCommandExtension = ".exc";
        private const string ExecuteCommandExtensionInitial = ".eini";
        private const string ExecuteCommandExtensionEnd = ".eend";
        private const string ExecuteCommandParamsExtension = ".params";
        private const string CannotTransformXdtMessage = "No se puede realizar la transformación del archivo .";

        #endregion

        #region Methods

        public string UpdateFiles(string sourceFolder, string targetFolder, string backupDir = null)
        {
            var createBackup = !string.IsNullOrEmpty(backupDir);

            if (createBackup && !Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            try
            {
                Directory.EnumerateFiles(sourceFolder, "*" + ExecuteCommandExtensionInitial, SearchOption.AllDirectories).ToList().ForEach(file =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var targetRelativeDirectory = Path.GetDirectoryName(GetRelativePath(file, sourceFolder));                    
                    this.ExecuteBats(file, fileName, targetRelativeDirectory, createBackup, backupDir, ExecuteCommandExtensionInitial, sourceFolder);
                });

                foreach (var file in Directory.EnumerateFiles(sourceFolder, "*.*", SearchOption.AllDirectories).Where(s => Path.GetExtension(s) != ExecuteCommandExtensionInitial && Path.GetExtension(s) != ExecuteCommandExtensionEnd).ToList())
                {
                    var fileExtension = Path.GetExtension(file);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var targetRelativeDirectory = Path.GetDirectoryName(GetRelativePath(file, sourceFolder));                    
                    var targetFileName = Path.Combine(targetFolder, targetRelativeDirectory ?? string.Empty, fileName ?? string.Empty);

                    switch (fileExtension.ToLower())
                    {
                        case AddCommandExtension:
                            if (createBackup)
                            {
                                BackupFile(Path.Combine(backupDir, targetRelativeDirectory ?? string.Empty), targetFolder, targetFileName, DeleteCommandExtension);
                            }
                            this.CopyFile(file, targetFileName);
                            break;
                        case XdtMergeCommandExtension:
                            if (createBackup && File.Exists(targetFileName))
                            {
                                BackupFile(Path.Combine(backupDir, targetRelativeDirectory ?? string.Empty), targetFolder, targetFileName, UpdateCommandExtension);
                            }
                            this.MergeXDT(file, targetFileName);
                            break;
                        case UpdateCommandExtension:
                            if (createBackup)
                            {
                                BackupFile(Path.Combine(backupDir, targetRelativeDirectory ?? string.Empty), targetFolder, targetFileName, UpdateCommandExtension);
                            }
                            this.CopyFile(file, targetFileName);
                            break;
                        case DeleteCommandExtension:
                            if (createBackup)
                            {
                                BackupFile(Path.Combine(backupDir, targetRelativeDirectory ?? string.Empty), targetFolder, targetFileName, AddCommandExtension);
                            }
                            this.RemoveFile(targetFileName);
                            break;
                        case ExecuteCommandExtension:
                            this.ExecuteBats(file, fileName, targetRelativeDirectory, createBackup, Path.Combine(backupDir ?? string.Empty, targetRelativeDirectory ?? string.Empty), ExecuteCommandExtension, sourceFolder);
                            break;
                    }
                }

                Directory.EnumerateFiles(sourceFolder, "*" + ExecuteCommandExtensionEnd, SearchOption.AllDirectories).ToList().ForEach(file =>
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var targetRelativeDirectory = Path.GetDirectoryName(GetRelativePath(file, sourceFolder));
                    this.ExecuteBats(file, fileName, targetRelativeDirectory, createBackup, Path.Combine(backupDir ?? string.Empty, targetRelativeDirectory ?? string.Empty), ExecuteCommandExtensionEnd, sourceFolder);
                });
            }
            catch (Exception ex)
            {
                if (createBackup)
                {
                    var errorMessage = ex.ToString();

                    var rollbackError = this.UpdateFiles(backupDir, targetFolder);

                    return errorMessage + (string.IsNullOrEmpty(rollbackError) ? string.Empty : Environment.NewLine + "Rollback Error =>" + Environment.NewLine + rollbackError);
                }

                return ex.ToString();
            }

            return string.Empty;
        }

        private static void BackupFile(string backupDir, string targetFolder, string originalFileName, string command)
        {
            var backupFileName = originalFileName;
            var targetRelativeDirectory = string.Empty;

            if (File.Exists(originalFileName))
            {
                if (command == DeleteCommandExtension)
                {
                    command = UpdateCommandExtension;
                    targetRelativeDirectory = Path.GetDirectoryName(GetRelativePath(originalFileName, targetFolder));
                }

                if (command == ExecuteCommandExtensionInitial || command == ExecuteCommandExtension || command == ExecuteCommandExtensionEnd || command == ExecuteCommandParamsExtension)
                {
                    backupFileName = Path.Combine(Path.GetDirectoryName(originalFileName), Path.GetFileNameWithoutExtension(originalFileName));
                    targetRelativeDirectory = targetFolder;
                }
            }
            else if (command == DeleteCommandExtension)
            {
                if (!Directory.Exists(Path.GetDirectoryName(originalFileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(originalFileName));
                }
                
                File.WriteAllText(originalFileName, string.Empty);

                targetRelativeDirectory = Path.GetDirectoryName(GetRelativePath(originalFileName, targetFolder));
            }
            else if (command == UpdateCommandExtension)
            {
                command = DeleteCommandExtension;
                if (!Directory.Exists(Path.GetDirectoryName(originalFileName)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(originalFileName));
                }

                File.WriteAllText(originalFileName, string.Empty);

                targetRelativeDirectory = Path.GetDirectoryName(GetRelativePath(originalFileName, targetFolder));
            }
            else
            {
                return;
            }

            var folderToBackupFile = Path.Combine(backupDir, targetRelativeDirectory);

            if (!Directory.Exists(folderToBackupFile))
            {
                Directory.CreateDirectory(folderToBackupFile);
            }

            File.Copy(originalFileName, Path.Combine(folderToBackupFile, Path.GetFileName(backupFileName)) + command, true);
        }

        private static string GetRelativePath(string filespec, string folder)
        {
            filespec = Path.GetFullPath(filespec);
            folder = Path.GetFullPath(folder);

            return Path.GetRelativePath(folder, filespec);            
        }

        private void ExecuteBats(string file, string fileName, string targetRelativeDirectory, bool createBackup, string backupDir, string extension, string sourceFolder)
        {
            try
            {
                Log.Info($"Ejecutando script {file} con extensión {extension}");

                if (!File.Exists(file))
                {
                    Log.Warn($"Archivo de script no encontrado: {file}");
                    return;
                }
                
                if (createBackup && !string.IsNullOrEmpty(backupDir))
                {
                    var relativePath = targetRelativeDirectory;
                    string pathBackup = Path.Combine(backupDir, relativePath);
                    string pathfileBackup = Path.Combine(pathBackup, fileName + extension);

                    Directory.CreateDirectory(pathBackup);
                    File.Copy(file, pathfileBackup, true);                    

                    Log.Info($"Backup creado para {fileName} en {pathBackup}");
                }

                if (extension == ".eini" || extension == ".eend" || extension == ".exc")
                {
                    var tempBatPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(file) + ".bat");
                    File.Copy(file, tempBatPath, overwrite: true);

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c \"{tempBatPath}\"",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = Path.GetDirectoryName(file) ?? Environment.CurrentDirectory
                        }
                    };

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Log.Info($"[BATCH OUT] {e.Data}");
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            Log.Error($"[BATCH ERR] {e.Data}");
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    File.Delete(tempBatPath);

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"El script {fileName} terminó con código de salida {process.ExitCode}");
                    }
                }

                Log.Info($"Script {fileName} ejecutado correctamente.");
            }
            catch (Exception ex)
            {
                Log.Error($"Error ejecutando script {fileName} con extensión {extension}", ex);
                throw;
            }
        }

        private void CopyFile(string sourceFile, string targetFile)
        {
            try
            {
                var targetDir = Path.GetDirectoryName(targetFile);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                File.Copy(sourceFile, targetFile, true);
                Log.Info($"Archivo copiado de {sourceFile} a {targetFile}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error copiando archivo de {sourceFile} a {targetFile}", ex);
                throw;
            }
        }

        private void RemoveFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Log.Info($"Archivo eliminado: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error eliminando archivo: {filePath}", ex);
                throw;
            }
        }

        private void MergeXDT(string sourceFile, string targetFile)
        {
            try
            {
                if (!File.Exists(targetFile))
                {
                    File.Copy(sourceFile, targetFile);
                    return;
                }

                using var document = new XmlTransformableDocument();
                document.PreserveWhitespace = true;
                document.Load(targetFile);

                using var transform = new XmlTransformation(sourceFile);

                if (!transform.Apply(document))
                {
                    throw new Exception(CannotTransformXdtMessage + sourceFile);
                }

                document.Save(targetFile);

                Log.Info($"Merge realizado XDT para {sourceFile} sobre {targetFile}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error realizando merge XDT para {sourceFile} sobre {targetFile}", ex);
                throw;
            }
        }

        #endregion Methods
    }
}
