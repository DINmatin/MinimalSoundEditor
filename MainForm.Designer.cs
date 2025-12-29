namespace MinimalSoundEditor
{
    partial class MainForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            _menuStrip = new MenuStrip();
            toolStripMenuItem1 = new ToolStripMenuItem();
            dateiToolStripMenuItem = new ToolStripMenuItem();
            miFileOpen = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            miFileSave = new ToolStripMenuItem();
            miFileSaveAs = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            miFileExportSel = new ToolStripMenuItem();
            toolStripSeparator3 = new ToolStripSeparator();
            miFileExit = new ToolStripMenuItem();
            ansichtToolStripMenuItem = new ToolStripMenuItem();
            miViewZoomAll = new ToolStripMenuItem();
            themeToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem3 = new ToolStripMenuItem();
            darkToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            einstellungenToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            presetsToolStripMenuItem = new ToolStripMenuItem();
            neonToolStripMenuItem = new ToolStripMenuItem();
            consoleToolStripMenuItem = new ToolStripMenuItem();
            sunsetToolStripMenuItem = new ToolStripMenuItem();
            _topPanel = new Panel();
            _btnNormalize = new Button();
            _btnFadeOut = new Button();
            _btnFadeIn = new Button();
            _btnTrim = new Button();
            _btnCompress = new Button();
            _btnExport = new Button();
            _btnSaveAs = new Button();
            _btnSave = new Button();
            _chkLoop = new CheckBox();
            _lblInfo = new Label();
            _btnTheme = new Button();
            _btnStop = new Button();
            _btnPlay = new Button();
            _btnUndo = new Button();
            _btnDeleteSelection = new Button();
            _btnOpen = new Button();
            _overviewPanel = new Panel();
            _overviewView = new WaveformView();
            _detailView = new WaveformView();
            _menuStrip.SuspendLayout();
            _topPanel.SuspendLayout();
            _overviewPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _menuStrip
            // 
            _menuStrip.Items.AddRange(new ToolStripItem[] { toolStripMenuItem1, dateiToolStripMenuItem, ansichtToolStripMenuItem, themeToolStripMenuItem });
            _menuStrip.Location = new Point(0, 0);
            _menuStrip.Name = "_menuStrip";
            _menuStrip.Size = new Size(1184, 24);
            _menuStrip.TabIndex = 1;
            _menuStrip.Text = "menuStrip1";
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(12, 20);
            // 
            // dateiToolStripMenuItem
            // 
            dateiToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { miFileOpen, toolStripSeparator1, miFileSave, miFileSaveAs, toolStripSeparator2, miFileExportSel, toolStripSeparator3, miFileExit });
            dateiToolStripMenuItem.Name = "dateiToolStripMenuItem";
            dateiToolStripMenuItem.Size = new Size(46, 20);
            dateiToolStripMenuItem.Text = "Datei";
            // 
            // miFileOpen
            // 
            miFileOpen.Name = "miFileOpen";
            miFileOpen.ShortcutKeys = Keys.Control | Keys.O;
            miFileOpen.Size = new Size(253, 22);
            miFileOpen.Text = "Öffnen";
            miFileOpen.Click += BtnOpen_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(250, 6);
            // 
            // miFileSave
            // 
            miFileSave.Name = "miFileSave";
            miFileSave.ShortcutKeys = Keys.Control | Keys.S;
            miFileSave.Size = new Size(253, 22);
            miFileSave.Text = "Speichern";
            miFileSave.Click += SaveWithPrompt;
            // 
            // miFileSaveAs
            // 
            miFileSaveAs.Name = "miFileSaveAs";
            miFileSaveAs.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
            miFileSaveAs.Size = new Size(253, 22);
            miFileSaveAs.Text = "Speichern unter";
            miFileSaveAs.Click += SaveAsWithFormat;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(250, 6);
            // 
            // miFileExportSel
            // 
            miFileExportSel.Name = "miFileExportSel";
            miFileExportSel.ShortcutKeys = Keys.Control | Keys.Shift | Keys.E;
            miFileExportSel.Size = new Size(253, 22);
            miFileExportSel.Text = "Auswahl exportieren";
            miFileExportSel.Click += ExportSelectionAs;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(250, 6);
            // 
            // miFileExit
            // 
            miFileExit.Name = "miFileExit";
            miFileExit.ShortcutKeys = Keys.Control | Keys.Q;
            miFileExit.Size = new Size(253, 22);
            miFileExit.Text = "Beenden";
            miFileExit.Click += miFileExit_Click;
            // 
            // ansichtToolStripMenuItem
            // 
            ansichtToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { miViewZoomAll });
            ansichtToolStripMenuItem.Name = "ansichtToolStripMenuItem";
            ansichtToolStripMenuItem.Size = new Size(59, 20);
            ansichtToolStripMenuItem.Text = "Ansicht";
            // 
            // miViewZoomAll
            // 
            miViewZoomAll.Name = "miViewZoomAll";
            miViewZoomAll.ShortcutKeys = Keys.Control | Keys.NumPad0;
            miViewZoomAll.Size = new Size(236, 22);
            miViewZoomAll.Text = "Alles anzeigen";
            miViewZoomAll.Click += miViewZoomAll_Click;
            // 
            // themeToolStripMenuItem
            // 
            themeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem3, darkToolStripMenuItem, toolStripSeparator4, einstellungenToolStripMenuItem, toolStripSeparator5, presetsToolStripMenuItem });
            themeToolStripMenuItem.Name = "themeToolStripMenuItem";
            themeToolStripMenuItem.Size = new Size(56, 20);
            themeToolStripMenuItem.Text = "Theme";
            // 
            // toolStripMenuItem3
            // 
            toolStripMenuItem3.Name = "toolStripMenuItem3";
            toolStripMenuItem3.Size = new Size(145, 22);
            toolStripMenuItem3.Text = "Light";
            // 
            // darkToolStripMenuItem
            // 
            darkToolStripMenuItem.Name = "darkToolStripMenuItem";
            darkToolStripMenuItem.Size = new Size(145, 22);
            darkToolStripMenuItem.Text = "Dark";
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(142, 6);
            // 
            // einstellungenToolStripMenuItem
            // 
            einstellungenToolStripMenuItem.Name = "einstellungenToolStripMenuItem";
            einstellungenToolStripMenuItem.Size = new Size(145, 22);
            einstellungenToolStripMenuItem.Text = "Einstellungen";
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(142, 6);
            // 
            // presetsToolStripMenuItem
            // 
            presetsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { neonToolStripMenuItem, consoleToolStripMenuItem, sunsetToolStripMenuItem });
            presetsToolStripMenuItem.Name = "presetsToolStripMenuItem";
            presetsToolStripMenuItem.Size = new Size(145, 22);
            presetsToolStripMenuItem.Text = "Presets";
            // 
            // neonToolStripMenuItem
            // 
            neonToolStripMenuItem.Name = "neonToolStripMenuItem";
            neonToolStripMenuItem.Size = new Size(151, 22);
            neonToolStripMenuItem.Text = "Neon";
            // 
            // consoleToolStripMenuItem
            // 
            consoleToolStripMenuItem.Name = "consoleToolStripMenuItem";
            consoleToolStripMenuItem.Size = new Size(151, 22);
            consoleToolStripMenuItem.Text = "Console Green";
            // 
            // sunsetToolStripMenuItem
            // 
            sunsetToolStripMenuItem.Name = "sunsetToolStripMenuItem";
            sunsetToolStripMenuItem.Size = new Size(151, 22);
            sunsetToolStripMenuItem.Text = "Warm Sunset";
            // 
            // _topPanel
            // 
            _topPanel.Controls.Add(_btnNormalize);
            _topPanel.Controls.Add(_btnFadeOut);
            _topPanel.Controls.Add(_btnFadeIn);
            _topPanel.Controls.Add(_btnTrim);
            _topPanel.Controls.Add(_btnCompress);
            _topPanel.Controls.Add(_btnExport);
            _topPanel.Controls.Add(_btnSaveAs);
            _topPanel.Controls.Add(_btnSave);
            _topPanel.Controls.Add(_chkLoop);
            _topPanel.Controls.Add(_lblInfo);
            _topPanel.Controls.Add(_btnTheme);
            _topPanel.Controls.Add(_btnStop);
            _topPanel.Controls.Add(_btnPlay);
            _topPanel.Controls.Add(_btnUndo);
            _topPanel.Controls.Add(_btnDeleteSelection);
            _topPanel.Controls.Add(_btnOpen);
            _topPanel.Dock = DockStyle.Top;
            _topPanel.Location = new Point(0, 24);
            _topPanel.Name = "_topPanel";
            _topPanel.Size = new Size(1184, 46);
            _topPanel.TabIndex = 2;
            // 
            // _btnNormalize
            // 
            _btnNormalize.BackgroundImage = Resource1.icon_normalize;
            _btnNormalize.BackgroundImageLayout = ImageLayout.Stretch;
            _btnNormalize.FlatAppearance.BorderSize = 0;
            _btnNormalize.FlatStyle = FlatStyle.Flat;
            _btnNormalize.Location = new Point(569, 6);
            _btnNormalize.Name = "_btnNormalize";
            _btnNormalize.Size = new Size(32, 32);
            _btnNormalize.TabIndex = 15;
            _btnNormalize.UseVisualStyleBackColor = true;
            _btnNormalize.Click += _btnNormalize_Click;
            // 
            // _btnFadeOut
            // 
            _btnFadeOut.BackgroundImage = Resource1.icon_fadeOut;
            _btnFadeOut.BackgroundImageLayout = ImageLayout.Stretch;
            _btnFadeOut.FlatAppearance.BorderSize = 0;
            _btnFadeOut.FlatStyle = FlatStyle.Flat;
            _btnFadeOut.Location = new Point(683, 6);
            _btnFadeOut.Name = "_btnFadeOut";
            _btnFadeOut.Size = new Size(32, 32);
            _btnFadeOut.TabIndex = 14;
            _btnFadeOut.UseVisualStyleBackColor = true;
            _btnFadeOut.Click += _btnFadeOut_Click;
            // 
            // _btnFadeIn
            // 
            _btnFadeIn.BackgroundImage = Resource1.icon_fadeIn;
            _btnFadeIn.BackgroundImageLayout = ImageLayout.Stretch;
            _btnFadeIn.FlatAppearance.BorderSize = 0;
            _btnFadeIn.FlatStyle = FlatStyle.Flat;
            _btnFadeIn.Location = new Point(645, 6);
            _btnFadeIn.Name = "_btnFadeIn";
            _btnFadeIn.Size = new Size(32, 32);
            _btnFadeIn.TabIndex = 13;
            _btnFadeIn.UseVisualStyleBackColor = true;
            _btnFadeIn.Click += _btnFadeIn_Click;
            // 
            // _btnTrim
            // 
            _btnTrim.BackgroundImage = Resource1.icon_trim;
            _btnTrim.BackgroundImageLayout = ImageLayout.Stretch;
            _btnTrim.FlatAppearance.BorderSize = 0;
            _btnTrim.FlatStyle = FlatStyle.Flat;
            _btnTrim.Location = new Point(607, 6);
            _btnTrim.Name = "_btnTrim";
            _btnTrim.Size = new Size(32, 32);
            _btnTrim.TabIndex = 12;
            _btnTrim.UseVisualStyleBackColor = true;
            _btnTrim.Click += _btnTrim_Click;
            // 
            // _btnCompress
            // 
            _btnCompress.BackgroundImage = Resource1.icon_compress;
            _btnCompress.BackgroundImageLayout = ImageLayout.Stretch;
            _btnCompress.FlatAppearance.BorderSize = 0;
            _btnCompress.FlatStyle = FlatStyle.Flat;
            _btnCompress.Location = new Point(531, 6);
            _btnCompress.Name = "_btnCompress";
            _btnCompress.Size = new Size(32, 32);
            _btnCompress.TabIndex = 11;
            _btnCompress.UseVisualStyleBackColor = true;
            _btnCompress.Click += _btnCompress_Click;
            // 
            // _btnExport
            // 
            _btnExport.BackgroundImage = Resource1.icon_export;
            _btnExport.BackgroundImageLayout = ImageLayout.Stretch;
            _btnExport.FlatAppearance.BorderSize = 0;
            _btnExport.FlatStyle = FlatStyle.Flat;
            _btnExport.Location = new Point(124, 6);
            _btnExport.Name = "_btnExport";
            _btnExport.Size = new Size(32, 32);
            _btnExport.TabIndex = 10;
            _btnExport.UseVisualStyleBackColor = true;
            _btnExport.Click += _btnExport_Click;
            // 
            // _btnSaveAs
            // 
            _btnSaveAs.BackgroundImage = Resource1.icon_saveAs;
            _btnSaveAs.BackgroundImageLayout = ImageLayout.Stretch;
            _btnSaveAs.FlatAppearance.BorderSize = 0;
            _btnSaveAs.FlatStyle = FlatStyle.Flat;
            _btnSaveAs.Location = new Point(86, 6);
            _btnSaveAs.Name = "_btnSaveAs";
            _btnSaveAs.Size = new Size(32, 32);
            _btnSaveAs.TabIndex = 9;
            _btnSaveAs.UseVisualStyleBackColor = true;
            _btnSaveAs.Click += _btnSaveAs_Click;
            // 
            // _btnSave
            // 
            _btnSave.BackgroundImage = Resource1.icon_save;
            _btnSave.BackgroundImageLayout = ImageLayout.Stretch;
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.Location = new Point(48, 6);
            _btnSave.Name = "_btnSave";
            _btnSave.Size = new Size(32, 32);
            _btnSave.TabIndex = 8;
            _btnSave.UseVisualStyleBackColor = true;
            _btnSave.Click += _btnSave_Click;
            // 
            // _chkLoop
            // 
            _chkLoop.Appearance = Appearance.Button;
            _chkLoop.BackgroundImage = Resource1.icon_loop;
            _chkLoop.BackgroundImageLayout = ImageLayout.Stretch;
            _chkLoop.FlatAppearance.BorderSize = 0;
            _chkLoop.FlatAppearance.CheckedBackColor = Color.MediumPurple;
            _chkLoop.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 192, 192);
            _chkLoop.FlatStyle = FlatStyle.Flat;
            _chkLoop.Location = new Point(477, 6);
            _chkLoop.Name = "_chkLoop";
            _chkLoop.Size = new Size(32, 32);
            _chkLoop.TabIndex = 7;
            _chkLoop.UseVisualStyleBackColor = true;
            // 
            // _lblInfo
            // 
            _lblInfo.AutoSize = true;
            _lblInfo.Location = new Point(736, 15);
            _lblInfo.Name = "_lblInfo";
            _lblInfo.Size = new Size(127, 15);
            _lblInfo.TabIndex = 6;
            _lblInfo.Text = "start by opening a clip!";
            // 
            // _btnTheme
            // 
            _btnTheme.BackgroundImage = Resource1.icon_themes;
            _btnTheme.BackgroundImageLayout = ImageLayout.Stretch;
            _btnTheme.FlatAppearance.BorderSize = 0;
            _btnTheme.FlatStyle = FlatStyle.Flat;
            _btnTheme.Location = new Point(1136, 3);
            _btnTheme.Name = "_btnTheme";
            _btnTheme.Size = new Size(32, 32);
            _btnTheme.TabIndex = 5;
            _btnTheme.UseVisualStyleBackColor = true;
            _btnTheme.Click += BtnTheme_Click;
            // 
            // _btnStop
            // 
            _btnStop.BackgroundImage = Resource1.icon_stop;
            _btnStop.BackgroundImageLayout = ImageLayout.Stretch;
            _btnStop.FlatAppearance.BorderSize = 0;
            _btnStop.FlatStyle = FlatStyle.Flat;
            _btnStop.Location = new Point(435, 6);
            _btnStop.Name = "_btnStop";
            _btnStop.Size = new Size(32, 32);
            _btnStop.TabIndex = 4;
            _btnStop.UseVisualStyleBackColor = true;
            _btnStop.Click += BtnStop_Click;
            // 
            // _btnPlay
            // 
            _btnPlay.BackgroundImage = Resource1.icon_play;
            _btnPlay.BackgroundImageLayout = ImageLayout.Stretch;
            _btnPlay.FlatAppearance.BorderSize = 0;
            _btnPlay.FlatStyle = FlatStyle.Flat;
            _btnPlay.Location = new Point(393, 6);
            _btnPlay.Name = "_btnPlay";
            _btnPlay.Size = new Size(32, 32);
            _btnPlay.TabIndex = 3;
            _btnPlay.UseVisualStyleBackColor = true;
            _btnPlay.Click += BtnPlay_Click;
            // 
            // _btnUndo
            // 
            _btnUndo.BackgroundImage = Resource1.icon_undo;
            _btnUndo.BackgroundImageLayout = ImageLayout.Stretch;
            _btnUndo.FlatAppearance.BorderSize = 0;
            _btnUndo.FlatStyle = FlatStyle.Flat;
            _btnUndo.Location = new Point(278, 6);
            _btnUndo.Name = "_btnUndo";
            _btnUndo.Size = new Size(32, 32);
            _btnUndo.TabIndex = 2;
            _btnUndo.UseVisualStyleBackColor = true;
            _btnUndo.Click += BtnUndo_Click;
            // 
            // _btnDeleteSelection
            // 
            _btnDeleteSelection.BackgroundImage = Resource1.icon_del;
            _btnDeleteSelection.BackgroundImageLayout = ImageLayout.Stretch;
            _btnDeleteSelection.FlatAppearance.BorderSize = 0;
            _btnDeleteSelection.FlatStyle = FlatStyle.Flat;
            _btnDeleteSelection.Location = new Point(236, 6);
            _btnDeleteSelection.Name = "_btnDeleteSelection";
            _btnDeleteSelection.Size = new Size(32, 32);
            _btnDeleteSelection.TabIndex = 1;
            _btnDeleteSelection.UseVisualStyleBackColor = true;
            _btnDeleteSelection.Click += BtnDeleteSelection_Click;
            // 
            // _btnOpen
            // 
            _btnOpen.BackgroundImage = Resource1.icon_openFile;
            _btnOpen.BackgroundImageLayout = ImageLayout.Stretch;
            _btnOpen.FlatAppearance.BorderSize = 0;
            _btnOpen.FlatStyle = FlatStyle.Flat;
            _btnOpen.Location = new Point(10, 6);
            _btnOpen.Name = "_btnOpen";
            _btnOpen.Size = new Size(32, 32);
            _btnOpen.TabIndex = 0;
            _btnOpen.UseVisualStyleBackColor = true;
            _btnOpen.Click += BtnOpen_Click;
            // 
            // _overviewPanel
            // 
            _overviewPanel.Controls.Add(_overviewView);
            _overviewPanel.Dock = DockStyle.Top;
            _overviewPanel.Location = new Point(0, 70);
            _overviewPanel.Name = "_overviewPanel";
            _overviewPanel.Size = new Size(1184, 80);
            _overviewPanel.TabIndex = 3;
            // 
            // _overviewView
            // 
            _overviewView.AllowHorizontalScroll = true;
            _overviewView.BackColor = Color.Black;
            _overviewView.Dock = DockStyle.Fill;
            _overviewView.ExtraScrollSamples = 0;
            _overviewView.Location = new Point(0, 0);
            _overviewView.Name = "_overviewView";
            _overviewView.PlaybackSample = 0;
            _overviewView.SampleRate = 44100;
            _overviewView.Size = new Size(1184, 80);
            _overviewView.TabIndex = 0;
            _overviewView.Text = "waveformView1";
            _overviewView.VisibleSampleCount = 0;
            _overviewView.VisibleStartSample = 0;
            _overviewView.Zoom = 0.5F;
            // 
            // _detailView
            // 
            _detailView.AllowHorizontalScroll = true;
            _detailView.BackColor = Color.Black;
            _detailView.Dock = DockStyle.Fill;
            _detailView.ExtraScrollSamples = 0;
            _detailView.Location = new Point(0, 150);
            _detailView.Name = "_detailView";
            _detailView.PlaybackSample = 0;
            _detailView.SampleRate = 44100;
            _detailView.Size = new Size(1184, 411);
            _detailView.TabIndex = 4;
            _detailView.Text = "waveformView1";
            _detailView.VisibleSampleCount = 0;
            _detailView.VisibleStartSample = 0;
            _detailView.Zoom = 0.5F;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1184, 561);
            Controls.Add(_detailView);
            Controls.Add(_overviewPanel);
            Controls.Add(_topPanel);
            Controls.Add(_menuStrip);
            DoubleBuffered = true;
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Name = "MainForm";
            Text = "Minimal Sound Editor";
            _menuStrip.ResumeLayout(false);
            _menuStrip.PerformLayout();
            _topPanel.ResumeLayout(false);
            _topPanel.PerformLayout();
            _overviewPanel.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip _menuStrip;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem dateiToolStripMenuItem;
        private ToolStripMenuItem miFileOpen;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem miFileSave;
        private ToolStripMenuItem miFileSaveAs;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem miFileExportSel;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripMenuItem miFileExit;
        private ToolStripMenuItem ansichtToolStripMenuItem;
        private ToolStripMenuItem miViewZoomAll;
        private ToolStripMenuItem themeToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem3;
        private ToolStripMenuItem darkToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator4;
        private ToolStripMenuItem einstellungenToolStripMenuItem;
        private ToolStripSeparator toolStripSeparator5;
        private ToolStripMenuItem presetsToolStripMenuItem;
        private ToolStripMenuItem neonToolStripMenuItem;
        private ToolStripMenuItem consoleToolStripMenuItem;
        private ToolStripMenuItem sunsetToolStripMenuItem;
        private Panel _topPanel;
        private Panel _overviewPanel;
        private WaveformView _overviewView;
        private WaveformView _detailView;
        private Button _btnOpen;
        private Button _btnDeleteSelection;
        private Button _btnStop;
        private Button _btnPlay;
        private Button _btnUndo;
        private Button _btnTheme;
        private Label _lblInfo;
        private CheckBox _chkLoop;
        private Button _btnExport;
        private Button _btnSaveAs;
        private Button _btnSave;
        private Button _btnFadeOut;
        private Button _btnFadeIn;
        private Button _btnTrim;
        private Button _btnCompress;
        private Button _btnNormalize;
    }
}