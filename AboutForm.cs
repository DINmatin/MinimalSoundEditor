using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace MinimalSoundEditor
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            var asm = Assembly.GetExecutingAssembly();
            var version = asm.GetName().Version?.ToString() ?? "unknown";

            Text = $"About Minimal Sound Editor  —  v{version}";

            label1.Text =
        $@"Minimal Sound Editor
Version: {version}

A lightweight waveform editor
built with ❤️ in C# / .NET WinForms.

Audio powered by:
• NAudio

Supports:
• WAV / MP3 / FLAC / AIFF / etc.";
        }


    }
}
