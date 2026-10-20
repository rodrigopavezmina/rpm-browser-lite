using System;
using System.IO;

namespace ChatGPTLite
{
    internal sealed class AppPaths
    {
        public string ExecutableDirectory { get; private set; }
        public bool IsPortable { get; private set; }
        public string DataDirectory { get; private set; }
        public string WebViewDirectory { get; private set; }
        public string NotesDirectory { get; private set; }
        public string PreviewsDirectory { get; private set; }
        public string CapturesDirectory { get; private set; }
        public string IconsDirectory { get; private set; }
        public string StateFile { get; private set; }
        public string LogFile { get; private set; }

        public static AppPaths Create()
        {
            string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
            bool portable = File.Exists(Path.Combine(executableDirectory, "portable.flag"));
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string legacyDataDirectory = Path.Combine(localData, "ChatGPT Lite");
            string dataDirectory = portable
                ? Path.Combine(executableDirectory, "Data")
                : (Directory.Exists(legacyDataDirectory)
                    ? legacyDataDirectory
                    : Path.Combine(localData, "RPM Browser Lite"));

            Directory.CreateDirectory(dataDirectory);

            AppPaths paths = new AppPaths();
            paths.ExecutableDirectory = executableDirectory;
            paths.IsPortable = portable;
            paths.DataDirectory = dataDirectory;
            paths.WebViewDirectory = Path.Combine(dataDirectory, "Perfil WebView2");
            paths.NotesDirectory = Path.Combine(dataDirectory, "Notas");
            paths.PreviewsDirectory = Path.Combine(dataDirectory, "Vistas hibernadas");
            paths.CapturesDirectory = Path.Combine(dataDirectory, "Capturas");
            paths.IconsDirectory = Path.Combine(dataDirectory, "Iconos");
            paths.StateFile = Path.Combine(dataDirectory, "estado.json");
            paths.LogFile = Path.Combine(dataDirectory, "ChatGPTLite.log");
            return paths;
        }
    }
}
