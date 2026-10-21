using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // GitHub and most modern update endpoints require TLS 1.2. Explicitly
            // selecting it keeps the updater reliable on older Windows defaults.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            string pipeName = "RPMBrowserLite-" + Environment.UserName;
            string command = args.FirstOrDefault(delegate(string value) { return value.StartsWith("--", StringComparison.Ordinal); });
            string initialUrl = args.FirstOrDefault(delegate(string value) { return !value.StartsWith("--", StringComparison.Ordinal); });
            if (string.Equals(command, "--show-manager", StringComparison.OrdinalIgnoreCase))
                initialUrl = "@show-manager";
            else if (string.Equals(command, "--exit", StringComparison.OrdinalIgnoreCase))
                initialUrl = "@exit";
            else if (string.Equals(command, "--hibernate-all", StringComparison.OrdinalIgnoreCase))
                initialUrl = "@hibernate-all";

            bool ownsMutex;
            using (Mutex mutex = new Mutex(true, "Local\\RPMBrowserLite", out ownsMutex))
            {
                if (ownsMutex && string.Equals(initialUrl, "@exit", StringComparison.Ordinal))
                    return;
                if (!ownsMutex)
                {
                    SendToExisting(pipeName, initialUrl ?? "@show-manager");
                    return;
                }

                try
                {
                    Application.Run(new AppController(AppPaths.Create(), pipeName, initialUrl));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        AppController.ProductName + " no pudo iniciarse.\r\n\r\n" + ex.Message,
                        AppController.ProductName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs args)
        {
            WriteFatalLog(args == null ? null : args.Exception);
            MessageBox.Show(
                "RPM Browser Lite encontró un problema recuperable. La sesión guardada se conserva y puedes continuar desde el administrador.",
                AppController.ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            WriteFatalLog(args == null ? null : args.ExceptionObject as Exception);
        }

        private static void WriteFatalLog(Exception exception)
        {
            try
            {
                AppPaths paths = AppPaths.Create();
                File.AppendAllText(
                    paths.LogFile,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " Unhandled UI exception\r\n" +
                    (exception == null ? string.Empty : exception.ToString()) + "\r\n\r\n",
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static void SendToExisting(string pipeName, string message)
        {
            try
            {
                using (NamedPipeClientStream client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out))
                {
                    client.Connect(1500);
                    using (StreamWriter writer = new StreamWriter(client, Encoding.UTF8))
                    {
                        writer.WriteLine(message ?? string.Empty);
                        writer.Flush();
                    }
                }
            }
            catch
            {
            }
        }
    }
}
