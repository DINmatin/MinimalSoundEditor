namespace MinimalSoundEditor
{
    partial class AboutForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            btnOk = new Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.Location = new Point(16, 16);
            label1.Name = "label1";
            label1.Size = new Size(300, 180);
            label1.TabIndex = 0;
            label1.Text = "Minimal Sound Editor\r\n\r\nA lightweight waveform editor\r\nbuilt with ❤️ in C# / .NET WinForms.\r\n\r\nAudio powered by:\r\n• NAudio\r\n\r\nSupports:\r\n• WAV / MP3 / FLAC / AIFF / etc.";
            // 
            // btnOk
            // 
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Location = new Point(114, 210);
            btnOk.Name = "btnOk";
            btnOk.Size = new Size(75, 23);
            btnOk.TabIndex = 1;
            btnOk.Text = "OK";
            btnOk.UseVisualStyleBackColor = true;
            // 
            // AboutForm
            // 
            AcceptButton = btnOk;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(315, 242);
            Controls.Add(btnOk);
            Controls.Add(label1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "About Minimal Sound Editor";
            ResumeLayout(false);
        }

        #endregion

        private Label label1;
        private Button btnOk;
    }
}