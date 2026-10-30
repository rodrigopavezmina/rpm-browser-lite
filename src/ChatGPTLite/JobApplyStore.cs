using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ChatGPTLite
{
    internal sealed class JobApplyStoreException : IOException
    {
        public JobApplyStoreException(
            string message,
            string dataPath,
            string backupPath,
            Exception innerException,
            Exception backupException)
            : base(message, innerException)
        {
            DataPath = dataPath;
            BackupPath = backupPath;
            BackupException = backupException;
        }

        public string DataPath { get; private set; }

        public string BackupPath { get; private set; }

        public Exception BackupException { get; private set; }
    }

    internal static class JobApplyStore
    {
        private const int MaxProfileBytes = 4 * 1024 * 1024;
        private static readonly object Sync = new object();
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
            "ChatGPTLite.JobApplicationProfile.v1");
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static JobApplicationProfile Load(string path)
        {
            string fullPath = NormalizePath(path);

            lock (Sync)
            {
                if (!File.Exists(fullPath))
                    return null;

                FileInfo fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaxProfileBytes)
                {
                    throw new JobApplyStoreException(
                        "El perfil cifrado supera el tamaño seguro permitido y se conservará sin abrirlo.",
                        fullPath,
                        null,
                        new InvalidDataException("Profile size limit exceeded."),
                        null);
                }

                byte[] encrypted = File.ReadAllBytes(fullPath);
                try
                {
                    if (encrypted.Length == 0)
                        throw new InvalidDataException("El archivo cifrado esta vacio.");

                    byte[] jsonBytes = ProtectedData.Unprotect(
                        encrypted,
                        Entropy,
                        DataProtectionScope.CurrentUser);

                    string json = StrictUtf8.GetString(jsonBytes);
                    JavaScriptSerializer serializer = CreateSerializer();
                    JobApplicationProfile profile = serializer.Deserialize<JobApplicationProfile>(json);
                    if (profile == null)
                        throw new InvalidDataException("El perfil guardado no contiene datos validos.");

                    profile.Repair();
                    return profile;
                }
                catch (Exception exception)
                {
                    if (IsFatal(exception))
                        throw;

                    throw CreateCorruptionException(fullPath, encrypted, exception);
                }
            }
        }

        public static JobApplicationProfile LoadOrCreateDefault(string path)
        {
            JobApplicationProfile profile = Load(path);
            return profile ?? CreateDefault();
        }

        public static JobApplicationProfile CreateDefault()
        {
            return JobApplicationProfile.CreateDefault();
        }

        public static void Save(string path, JobApplicationProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException("profile");

            string fullPath = NormalizePath(path);

            lock (Sync)
            {
                string directory = Path.GetDirectoryName(fullPath);
                Directory.CreateDirectory(directory);

                JavaScriptSerializer serializer = CreateSerializer();
                string json = serializer.Serialize(profile);
                byte[] clearBytes = StrictUtf8.GetBytes(json);
                if (clearBytes.Length > MaxProfileBytes)
                    throw new InvalidDataException("El perfil supera el tamaño seguro permitido.");
                byte[] encrypted = ProtectedData.Protect(
                    clearBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser);

                string temporaryPath = Path.Combine(
                    directory,
                    ".job-apply-" + Guid.NewGuid().ToString("N") + ".tmp");

                try
                {
                    WriteNewFile(temporaryPath, encrypted);

                    if (File.Exists(fullPath))
                    {
                        string backupPath = fullPath + ".bak";
                        File.Replace(temporaryPath, fullPath, backupPath, true);
                    }
                    else
                    {
                        File.Move(temporaryPath, fullPath);
                    }
                }
                finally
                {
                    TryDeleteTemporaryFile(temporaryPath);
                }
            }
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            serializer.RecursionLimit = 100;
            return serializer;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Se requiere una ruta para el perfil de postulacion.", "path");

            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("La ruta del perfil no tiene un directorio valido.", "path");

            return fullPath;
        }

        private static void WriteNewFile(string path, byte[] contents)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(contents, 0, contents.Length);
                stream.Flush(true);
            }
        }

        private static JobApplyStoreException CreateCorruptionException(
            string path,
            byte[] encrypted,
            Exception cause)
        {
            string backupPath = null;
            Exception backupException = null;

            try
            {
                backupPath = CreateCorruptBackup(path, encrypted);
            }
            catch (Exception exception)
            {
                if (IsFatal(exception))
                    throw;
                backupException = exception;
            }

            string message;
            if (!string.IsNullOrEmpty(backupPath))
            {
                message = "No se pudo leer el perfil de postulacion. "
                    + "El archivo original se conservo y se creo una copia en '"
                    + backupPath + "'.";
            }
            else
            {
                message = "No se pudo leer el perfil de postulacion. "
                    + "El archivo original se conservo, pero no fue posible crear una copia de respaldo.";
            }

            return new JobApplyStoreException(
                message,
                path,
                backupPath,
                cause,
                backupException);
        }

        private static string CreateCorruptBackup(string path, byte[] contents)
        {
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
            string backupPath = Path.Combine(
                directory,
                fileName + ".corrupt-" + timestamp + "-"
                    + Guid.NewGuid().ToString("N") + ".bak");

            WriteNewFile(backupPath, contents);
            return backupPath;
        }

        private static void TryDeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // La escritura principal ya fallo o termino. Un temporal cifrado
                // remanente no debe ocultar el resultado de esa operacion.
            }
        }

        private static bool IsFatal(Exception exception)
        {
            return exception is OutOfMemoryException
                || exception is StackOverflowException
                || exception is AccessViolationException
                || exception is AppDomainUnloadedException
                || exception is BadImageFormatException
                || exception is CannotUnloadAppDomainException;
        }
    }
}
