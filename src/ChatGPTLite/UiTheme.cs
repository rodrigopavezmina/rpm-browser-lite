using System.Drawing;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ChatGPTLite
{
    internal static class UiTheme
    {
        public static readonly Color DarkBackground = Color.FromArgb(20, 24, 29);
        public static readonly Color DarkSurface = Color.FromArgb(31, 37, 44);
        public static readonly Color DarkInput = Color.FromArgb(41, 48, 57);
        public static readonly Color DarkText = Color.FromArgb(236, 240, 245);
        public static readonly Color DarkMutedText = Color.FromArgb(176, 187, 199);
        public static readonly Color LightBackground = Color.FromArgb(245, 245, 242);
        public static readonly Color LightText = Color.FromArgb(40, 40, 40);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        public static void ApplyWindowChrome(Form form, bool dark)
        {
            if (form == null || form.IsDisposed)
                return;
            try
            {
                int enabled = dark ? 1 : 0;
                // Windows 10 uses attribute 19; Windows 11 uses 20. Applying both is
                // harmless and keeps the title bar in sync with the in-app dark mode.
                DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
                DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int));
            }
            catch
            {
                // Older Windows builds may not expose the DWM attribute.
            }
        }

        public static void ApplyNativeTree(Control root, bool dark)
        {
            if (root == null)
                return;

            ApplyOne(root, dark);
            foreach (Control child in root.Controls)
                ApplyNativeTree(child, dark);
        }

        private static void ApplyOne(Control control, bool dark)
        {
            if (!dark)
            {
                if (control is RichTextBox || control is TextBox || control is NumericUpDown || control is ComboBox || control is ListView)
                {
                    control.BackColor = Color.White;
                    control.ForeColor = UiTheme.LightText;
                    return;
                }
                if (control is Button)
                {
                    Button button = (Button)control;
                    button.FlatStyle = FlatStyle.Flat;
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = Color.White;
                    button.ForeColor = UiTheme.LightText;
                    button.FlatAppearance.BorderColor = Color.FromArgb(170, 180, 194);
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 240, 250);
                    return;
                }
                if (control is TabPage)
                {
                    control.BackColor = Color.White;
                    control.ForeColor = UiTheme.LightText;
                    return;
                }
                if (control is TabControl)
                {
                    control.BackColor = UiTheme.LightBackground;
                    control.ForeColor = UiTheme.LightText;
                    return;
                }
                control.BackColor = UiTheme.LightBackground;
                control.ForeColor = UiTheme.LightText;
                return;
            }

            if (control is RichTextBox || control is TextBox || control is NumericUpDown || control is ComboBox)
            {
                control.BackColor = DarkInput;
                control.ForeColor = DarkText;
                return;
            }
            if (control is ListView)
            {
                control.BackColor = DarkSurface;
                control.ForeColor = DarkText;
                return;
            }
            if (control is Button)
            {
                Button button = (Button)control;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(75, 86, 100);
                button.BackColor = DarkInput;
                button.ForeColor = DarkText;
                return;
            }
            if (control is TabPage)
            {
                control.BackColor = DarkBackground;
                control.ForeColor = DarkText;
                return;
            }
            if (control is TabControl)
            {
                control.BackColor = DarkSurface;
                control.ForeColor = DarkText;
                return;
            }

            control.BackColor = control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel
                ? DarkSurface
                : DarkBackground;
            // Native CheckBox, RadioButton and similar controls do not follow the
            // Label branch; set the foreground for every remaining control so
            // dark mode never leaves black text on the dark surface.
            control.ForeColor = DarkText;
        }
    }
}
