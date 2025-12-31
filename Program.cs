using System;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    internal static class Program
    {
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
