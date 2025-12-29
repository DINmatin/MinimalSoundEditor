namespace MinimalSoundEditor
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btn_test = new Button();
            chk_test = new CheckBox();
            SuspendLayout();
            // 
            // btn_test
            // 
            btn_test.BackColor = Color.Transparent;
            btn_test.BackgroundImage = Resource1.icon_loop;
            btn_test.BackgroundImageLayout = ImageLayout.Stretch;
            btn_test.FlatAppearance.BorderSize = 0;
            btn_test.FlatStyle = FlatStyle.Flat;
            btn_test.Location = new Point(490, 12);
            btn_test.Name = "btn_test";
            btn_test.Size = new Size(44, 46);
            btn_test.TabIndex = 0;
            btn_test.UseVisualStyleBackColor = false;
            // 
            // chk_test
            // 
            chk_test.AutoSize = true;
            chk_test.FlatAppearance.BorderSize = 0;
            chk_test.FlatStyle = FlatStyle.Flat;
            chk_test.Image = Resource1.icon_loop;
            chk_test.Location = new Point(510, 159);
            chk_test.Name = "chk_test";
            chk_test.Size = new Size(292, 260);
            chk_test.TabIndex = 1;
            chk_test.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(chk_test);
            Controls.Add(btn_test);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btn_test;
        private CheckBox chk_test;
    }
}
