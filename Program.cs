using System;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    /// <summary>Application entry point and optional command-line file opener.</summary>
    internal static class Program
    {
        /// <summary>Initializes WinForms, forwards the first command-line argument, and starts the main message loop.</summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new MainForm();
            if (args != null && args.Length > 0)
            {
                try
                {
                    string file = args[0];
                    form.LoadAudioFileFromExternal(file);
                }
                catch
                {
                    // ignore — app still opens normally
                }
            }
            Application.Run(form);
        }
    }
}
