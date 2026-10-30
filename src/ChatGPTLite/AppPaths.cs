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
        public string JobApplyProfileFile { get; private set; }
        public string LogFile { get; private set; }

        public static AppPaths Create()
        {
            string executableDirectory = AppDomain.CurrentDomain.BaseDirectory;
            bool portable = File.Exists(Path.Combine(executableDirectory, "portable.flag"));
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dataDirectory = portable
                ? Path.Combine(executableDirectory, "Data")
                : Path.Combine(localData, "RPM IA Job Apply Browser Lite Turbo");

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
            // The application profile contains personal and employment data, so it
            // is kept outside estado.json and protected separately with Windows
            // DPAPI by JobApplyStore.
            paths.JobApplyProfileFile = Path.Combine(dataDirectory, "perfil-postulacion.dat");
            paths.LogFile = Path.Combine(dataDirectory, "RPMIAJobApplyBrowserLiteTurbo.log");
            return paths;
        }
    }
}
