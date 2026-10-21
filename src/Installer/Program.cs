using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("RPM Browser Lite Setup")]
[assembly: AssemblyProduct("RPM Browser Lite")]
[assembly: AssemblyVersion("2.8.21.0")]
[assembly: AssemblyFileVersion("2.8.21.0")]

namespace ChatGPTLiteInstaller
{
    internal static class Program
    {
        internal const string ProductName = "RPM Browser Lite";
        private const string ProductVersion = "2.8.21";
        private const string RegistryKeyName = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\RPM Browser Lite";
        private const string LegacyProductName = "ChatGPT Lite";
        private static readonly string[] PayloadFiles = new string[]
        {
            "RPMBrowserLite.exe",
            "RPMBrowserLite.exe.config",
            "Microsoft.Web.WebView2.Core.dll",
            "Microsoft.Web.WebView2.WinForms.dll",
            "WebView2Loader.dll",
            "LEEME.md"
        };
        private static readonly string[] LegacyBinaryFiles = new string[]
        {
            "ChatGPTLite.exe",
            "ChatGPTLite.exe.config"
        };

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && string.Equals(args[0], "/uninstall", StringComparison.OrdinalIgnoreCase))
            {
                Uninstall();
                return;
            }
            if (args.Length > 0 && string.Equals(args[0], "/silent", StringComparison.OrdinalIgnoreCase))
            {
                WaitForApplicationExit();
                // A verified automatic update should leave the user with the
                // updated application ready to use, not silently installed.
                try { Install(true, true); }
                catch { }
                return;
            }
            Application.Run(new InstallerForm());
        }

        internal static string InstallDirectory
        {
            get
            {
                string programs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs");
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyName))
                    {
                        string registered = key == null ? null : key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(registered) &&
                            Directory.Exists(registered) &&
                            registered.StartsWith(programs, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(Path.GetFileName(registered), LegacyProductName, StringComparison.OrdinalIgnoreCase))
                            return registered;
                    }
                }
                catch
                {
                    // Fall through to the standard renamed folder.
                }

                // Never return to the old ChatGPT Lite folder.  It can remain from an
                // older release without taking over current RPM Browser Lite updates.
                return Path.Combine(programs, ProductName);
            }
        }

        internal static void Install(bool desktopShortcut, bool runAfterInstall)
        {
            if (IsApplicationRunning())
                throw new InvalidOperationException("Close " + ProductName + " from its icon near the clock, then try again.");

            string directory = InstallDirectory;
            try
            {
                WritePayloadFiles(directory);
            }
            catch (IOException)
            {
                // Some Windows builds keep an exited WebView process attached to the
                // old executable for a while.  A versioned side-by-side folder keeps
                // the update available immediately instead of failing silently.
                directory = GetFallbackInstallDirectory();
                WritePayloadFiles(directory);
            }
            foreach (string legacyFile in LegacyBinaryFiles)
            {
                string legacyPath = Path.Combine(directory, legacyFile);
                if (File.Exists(legacyPath))
                    File.Delete(legacyPath);
            }

            string uninstaller = Path.Combine(directory, "Uninstall RPM Browser Lite.exe");
            File.Copy(Assembly.GetExecutingAssembly().Location, uninstaller, true);
            string legacyUninstaller = Path.Combine(directory, "Desinstalar ChatGPT Lite.exe");
            if (File.Exists(legacyUninstaller))
                File.Delete(legacyUninstaller);
            CreateShortcuts(directory, desktopShortcut, uninstaller);
            RegisterUninstaller(directory, uninstaller);

            if (runAfterInstall)
                Process.Start(Path.Combine(directory, "RPMBrowserLite.exe"));
        }

        private static string GetFallbackInstallDirectory()
        {
            string programs = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs");
            return Path.Combine(programs, ProductName + " " + ProductVersion);
        }

        private static void WritePayloadFiles(string directory)
        {
            Directory.CreateDirectory(directory);
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (string file in PayloadFiles)
            {
                string resourceName = "payload." + file;
                using (Stream source = assembly.GetManifestResourceStream(resourceName))
                {
                    if (source == null)
                        throw new InvalidDataException("Falta un componente del instalador: " + file);
                    using (FileStream target = File.Create(Path.Combine(directory, file)))
                        source.CopyTo(target);
                }
            }
        }

        private static void Uninstall()
        {
            if (IsApplicationRunning())
            {
                MessageBox.Show(
                    "Cierra ChatGPT Lite desde su icono junto al reloj antes de desinstalar.",
                    ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DialogResult answer = MessageBox.Show(
                "¿Desinstalar ChatGPT Lite?\r\n\r\nTu sesión, caché y lista local se conservarán para que puedas reinstalar sin perderlos.",
                "Desinstalar " + ProductName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;

            string directory = InstallDirectory;
            RemoveShortcuts();
            RemoveLegacyShortcuts();
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegistryKeyName, false);
            }
            catch
            {
            }

            foreach (string file in PayloadFiles)
            {
                try { File.Delete(Path.Combine(directory, file)); }
                catch { }
            }
            foreach (string file in LegacyBinaryFiles)
            {
                try { File.Delete(Path.Combine(directory, file)); }
                catch { }
            }

            string self = Assembly.GetExecutingAssembly().Location;
            MoveFileEx(self, null, MoveFileFlags.DelayUntilReboot);
            MoveFileEx(directory, null, MoveFileFlags.DelayUntilReboot);

            MessageBox.Show(
                "RPM Browser Lite fue desinstalado.\r\n\r\nLos datos locales se conservaron en:\r\n" +
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPT Lite"),
                ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static void CreateShortcuts(string directory, bool desktopShortcut, string uninstaller)
        {
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string group = Path.Combine(programs, ProductName);
            Directory.CreateDirectory(group);
            string app = Path.Combine(directory, "RPMBrowserLite.exe");
            CreateShortcut(Path.Combine(group, ProductName + ".lnk"), app, directory, "Lightweight browser with hibernatable windows");
            CreateShortcut(Path.Combine(group, "Uninstall " + ProductName + ".lnk"), uninstaller, directory, "Uninstall " + ProductName, "/uninstall");

            string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk");
            if (desktopShortcut || !File.Exists(desktop))
                CreateShortcut(desktop, app, directory, "Lightweight browser with hibernatable windows");
            RemoveLegacyShortcuts();
        }

        private static void RemoveShortcuts()
        {
            string group = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ProductName);
            try
            {
                string appShortcut = Path.Combine(group, ProductName + ".lnk");
                string uninstallShortcut = Path.Combine(group, "Uninstall " + ProductName + ".lnk");
                if (File.Exists(appShortcut)) File.Delete(appShortcut);
                if (File.Exists(uninstallShortcut)) File.Delete(uninstallShortcut);
                if (Directory.Exists(group) && Directory.GetFileSystemEntries(group).Length == 0) Directory.Delete(group);
            }
            catch
            {
            }
            try
            {
                string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ProductName + ".lnk");
                if (File.Exists(desktop)) File.Delete(desktop);
            }
            catch
            {
            }
        }

        private static void RemoveLegacyShortcuts()
        {
            try
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string legacyGroup = Path.Combine(programs, LegacyProductName);
                string legacyApp = Path.Combine(legacyGroup, LegacyProductName + ".lnk");
                string legacyUninstall = Path.Combine(legacyGroup, "Desinstalar ChatGPT Lite.lnk");
                if (File.Exists(legacyApp)) File.Delete(legacyApp);
                if (File.Exists(legacyUninstall)) File.Delete(legacyUninstall);
                if (Directory.Exists(legacyGroup) && Directory.GetFileSystemEntries(legacyGroup).Length == 0) Directory.Delete(legacyGroup);
                string legacyDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), LegacyProductName + ".lnk");
                if (File.Exists(legacyDesktop)) File.Delete(legacyDesktop);
            }
            catch
            {
            }
        }

        private static bool IsApplicationRunning()
        {
            return HasLiveApplicationProcess("RPMBrowserLite");
        }

        private static bool HasLiveApplicationProcess(string processName)
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    // A real instance either owns a visible/hidden WinForms window or
                    // exposes the executable path. Ignore orphaned process-table entries
                    // left without a window and without a readable image path; they cannot
                    // hold the installed binary and should not block an update forever.
                    if (process.MainWindowHandle != IntPtr.Zero)
                        return true;

                    string imagePath = process.MainModule == null ? string.Empty : process.MainModule.FileName;
                    if (!string.IsNullOrWhiteSpace(imagePath) &&
                        string.Equals(
                            Path.GetFullPath(imagePath),
                            Path.GetFullPath(Path.Combine(InstallDirectory, "RPMBrowserLite.exe")),
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // If the process is disappearing, it is safe to let the installer
                    // perform its normal copy attempt rather than hanging the update.
                }
                finally
                {
                    process.Dispose();
                }
            }
            return false;
        }

        private static void WaitForApplicationExit()
        {
            for (int attempt = 0; attempt < 100 && IsApplicationRunning(); attempt++)
                System.Threading.Thread.Sleep(300);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            CreateShortcut(shortcutPath, targetPath, workingDirectory, description, string.Empty);
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description, string arguments)
        {
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(type);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Description = description;
            shortcut.Arguments = arguments;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Save();
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }

        private static void RegisterUninstaller(string directory, string uninstaller)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyName))
            {
                key.SetValue("DisplayName", ProductName, RegistryValueKind.String);
                key.SetValue("DisplayVersion", ProductVersion, RegistryValueKind.String);
                key.SetValue("Publisher", ProductName, RegistryValueKind.String);
                key.SetValue("InstallLocation", directory, RegistryValueKind.String);
                key.SetValue("DisplayIcon", Path.Combine(directory, "RPMBrowserLite.exe") + ",0", RegistryValueKind.String);
                key.SetValue("UninstallString", "\"" + uninstaller + "\" /uninstall", RegistryValueKind.String);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 1600, RegistryValueKind.DWord);
            }
        }

        [Flags]
        private enum MoveFileFlags
        {
            DelayUntilReboot = 0x4
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string existingFileName, string newFileName, MoveFileFlags flags);
    }

    internal sealed class InstallerForm : Form
    {
        private readonly CheckBox _desktop;
        private readonly CheckBox _run;
        private readonly Button _install;
        private readonly Label _status;

        public InstallerForm()
        {
            Text = "Install " + Program.ProductName;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 360);
            Font = new Font("Segoe UI", 9f);

            Label title = new Label();
            title.Text = Program.ProductName;
            title.Font = new Font("Segoe UI Semibold", 24f, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(30, 28);

            Label subtitle = new Label();
            subtitle.Text = "Cliente Windows liviano con ventanas suspendibles e hibernables";
            subtitle.AutoSize = true;
            subtitle.ForeColor = Color.FromArgb(85, 85, 85);
            subtitle.Location = new Point(34, 80);

            Label locationTitle = new Label();
            locationTitle.Text = "Se instalará solo para tu usuario en:";
            locationTitle.AutoSize = true;
            locationTitle.Location = new Point(34, 127);

            TextBox location = new TextBox();
            location.ReadOnly = true;
            location.Text = Program.InstallDirectory;
            location.Location = new Point(37, 151);
            location.Width = 486;

            _desktop = new CheckBox();
            _desktop.Text = "Crear acceso directo en el escritorio";
            _desktop.AutoSize = true;
            _desktop.Location = new Point(38, 202);

            _run = new CheckBox();
            _run.Text = "Open " + Program.ProductName + " when finished";
            _run.Checked = true;
            _run.AutoSize = true;
            _run.Location = new Point(38, 231);

            _status = new Label();
            _status.Text = "No requiere permisos de administrador.";
            _status.AutoSize = false;
            _status.Width = 340;
            _status.Height = 34;
            _status.ForeColor = Color.FromArgb(85, 85, 85);
            _status.Location = new Point(37, 286);

            _install = new Button();
            _install.Text = "Instalar";
            _install.Width = 110;
            _install.Height = 36;
            _install.Location = new Point(413, 285);
            _install.Click += InstallClicked;

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(locationTitle);
            Controls.Add(location);
            Controls.Add(_desktop);
            Controls.Add(_run);
            Controls.Add(_status);
            Controls.Add(_install);
            AcceptButton = _install;
        }

        private void InstallClicked(object sender, EventArgs e)
        {
            _install.Enabled = false;
            _status.Text = "Instalando…";
            Cursor = Cursors.WaitCursor;
            try
            {
                Program.Install(_desktop.Checked, _run.Checked);
                _status.Text = "Instalación completada.";
                MessageBox.Show(Program.ProductName + " was installed successfully.", Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _status.Text = "No se pudo completar la instalación.";
                MessageBox.Show(ex.Message, Program.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                _install.Enabled = true;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
