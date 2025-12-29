using MinimalSoundEditor;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using static MinimalSoundEditor.MainForm;

namespace MinimalSoundEditor
{
    public partial class MainForm : Form
    {
        // ✅ Singleton-Referenz für statische Helper (themes.json usw.)
        private static MainForm _instance;

        // ===== Hover Zoom =====
        private readonly Dictionary<Control, (Size size, Point loc)> _hoverBackup
            = new Dictionary<Control, (Size, Point)>();

        // private WaveformView _overviewView;
        // private WaveformView _detailView;
        //private Button _btnOpen;
        //private Button _btnDeleteSelection;
        //private Button _btnUndo;
        //private Button _btnPlay;
        //private Button _btnStop;
        //  private Button _btnTheme;
        //  private Label _lblInfo;

        //  private Panel _topPanel;
        // private Panel _overviewPanel;

        // private MenuStrip _menuStrip;

        private ToolStripMenuItem _miThemeLight;
        private ToolStripMenuItem _miThemeDark;

        private const int SelectionFillAlpha = 110; // oder 80, wie du magst

        public enum ThemeMode
        {
            Light,
            Dark
        }

        public class ThemeDefinition
        {
            public ThemeMode Mode { get; set; }
            public Color FormBack { get; set; }
            public WaveformViewTheme Waveform { get; set; }
            public Color ButtonBack { get; set; }
            public Color ButtonFore { get; set; }
            public Color ButtonBorder { get; set; }
        }

        // Theme
        private ThemeDefinition _lightTheme;
        private ThemeDefinition _darkTheme;
        private ThemeMode _currentThemeMode = ThemeMode.Dark;

        private ThemeDefinition CurrentTheme =>
            _currentThemeMode == ThemeMode.Light ? _lightTheme : _darkTheme;


        // Audio-Daten
        private float[] _currentSamples = Array.Empty<float>();
        private int _currentSampleRate = 44100;

        // Playback
        private WaveOutEvent _waveOut;
        private IPositionedSampleProvider _currentProvider;
        private System.Windows.Forms.Timer _playbackTimer;
        private int _playbackSamplePosition; // aktueller Sampleindex
        private double _trackDurationSeconds; // Gesamtdauer
        private bool _isClosing;

        // Undo
        private Stack<float[]> _undoStack = new Stack<float[]>();

        // Loop
       // private CheckBox _chkLoop;
        private bool _loopEnabled = false;

        private string _currentFilePath;   // aktuell geladene Datei
        private bool _isDirty;             // wurde editiert?

        private enum AudioExportFormat
        {
            Wav,
            Mp3,
            Aac // AAC in .m4a/.mp4 Container
        }

        //public MainForm()
        //{
        //    _instance = this;

        //    InitializeComponent();

        //    // 1. Code-Defaults setzen
        //    InitThemes();

        //    // 2. Themes aus themes.json (HEX) laden
        //    LoadThemeDefaultsFromFile();

        //    // 3. User-Mode (Light/Dark) aus settings.json lesen
        //    LoadThemeSettings();

        //    // 4. Anwenden
        //    ApplyTheme();
        //    UpdateThemeMenuChecks();
        //}



        private void InitializeCustomUi()
        {
            Text = "Minimal Sound Editor";
            Width = 1000;
            Height = 600;
            KeyPreview = true;

            const int toolbarTop = 6;
            const int toolbarHeight = 36;

            // === OVERVIEW (oben, klein) ===
            _overviewView = new WaveformView
            {
                Dock = DockStyle.Fill,
                Zoom = 0.5f
            };
            _overviewView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            _overviewView.SelectionChanged += OverviewView_SelectionChanged;
            _overviewView.MouseDoubleClick += OverviewView_MouseDoubleClick;

            _overviewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80
            };
            _overviewPanel.Controls.Add(_overviewView);

            // === DETAIL (unten, Editing) ===
            _detailView = new WaveformView
            {
                Dock = DockStyle.Fill,
                Zoom = 1.0f
            };
            _detailView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            _detailView.SelectionChanged += DetailView_SelectionChanged;

            // === TOP BUTTON BAR ===
            _btnOpen = new Button
            {
                Text = "",
                Width = toolbarHeight,
                Left = 10,
                Top = toolbarTop,
                Height = toolbarHeight
            };
            _btnOpen.Click += BtnOpen_Click;

           

            _btnDeleteSelection = new Button
            {
                Text = "",
                Width = toolbarHeight,
                Left = 290,
                Top = toolbarTop,
                Height = toolbarHeight
            };
            _btnDeleteSelection.Click += BtnDeleteSelection_Click;


            //_btnUndo = new Button
            //{
            //    Text = "",
            //    Width = toolbarHeight,
            //    Left = 290 + 42,
            //    Top = toolbarTop,
            //    Height = toolbarHeight
            //};
            //_btnUndo.Click += BtnUndo_Click;            // ✅ richtig


            //_btnPlay = new Button
            //{
            //    Text = "",
            //    Width = toolbarHeight,
            //    Top = toolbarTop,
            //    Height = toolbarHeight,
            //    BackColor = Color.Transparent,
            //    Left = 420
            //};
            //_btnPlay.Click += BtnPlay_Click;

            //_btnStop = new Button
            //{
            //    Text = "",
            //    Width = toolbarHeight,
            //    Left = 420+42,
            //    Top = toolbarTop,
            //    Height = toolbarHeight
            //};
            //_btnStop.Click += BtnStop_Click;

            _chkLoop = new CheckBox
            {
                Appearance = Appearance.Button,
                Text = "",
                Width = toolbarHeight,
                Left = 420+42+42,
                Top = toolbarTop,
                Height = toolbarHeight,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _chkLoop.CheckedChanged += (s, e) =>
            {
                _loopEnabled = _chkLoop.Checked;
            };

            //_btnTheme = new Button
            //{
            //    Text = "",
            //    Width = toolbarHeight,
            //    Left = 660,
            //    Top = toolbarTop,
            //    Height = toolbarHeight
            //};
            //_btnTheme.Click += (s, e) => OpenThemeSettings();
            // OPEN (kannst du später auch ein schönes „ordner“-Icon spendieren)
            StyleToolbarButton(_btnOpen, Resource1.icon_openFile, "");

            // DELETE SELECTION
            StyleToolbarButton(_btnDeleteSelection, Resource1.icon_del, "");

            // UNDO
            StyleToolbarButton(_btnUndo, Resource1.icon_undo, "");

            // PLAY
            StyleToolbarButton(_btnPlay, Resource1.icon_play, "");

            // STOP
            StyleToolbarButton(_btnStop, Resource1.icon_stop, "");

            // LOOP – CheckBox als Button mit Icon
            {
                Image icon = resizeIconImage( Resource1.icon_loop);

                _chkLoop.Image = icon;
                _chkLoop.ImageAlign = ContentAlignment.MiddleLeft;
                _chkLoop.Text = "";
                _chkLoop.TextImageRelation = TextImageRelation.ImageAboveText;
                _chkLoop.Padding = new Padding(1, 0, 1, 0);
                _chkLoop.FlatStyle = FlatStyle.Flat;
                _chkLoop.FlatAppearance.BorderSize = 0;
                _chkLoop.BackColor = Color.Transparent;
                _chkLoop.FlatAppearance.MouseOverBackColor = Color.Pink;
                _chkLoop.FlatAppearance.CheckedBackColor = Color.MediumPurple;
            }

            // THEME
            StyleToolbarButton(_btnTheme, Resource1.icon_themes, "");

            //_lblInfo = new Label
            //{
            //    Text = "Keine Datei geladen",
            //    AutoSize = true,
            //    Left = 760,
            //    Top = 15
            //};

            _topPanel = new Panel
            {
                Height = toolbarTop + toolbarHeight + 4, // z.B. 6 + 36 + 4 = 46
                Dock = DockStyle.Top
            };
            _topPanel.Controls.AddRange(new Control[]
            {
        _btnOpen,
        _btnDeleteSelection,
        _btnUndo,
        _btnPlay,
        _btnStop,
        _chkLoop,
        _btnTheme,
        _lblInfo
            });

            // === MENÜLEISTE ===
            _menuStrip = new MenuStrip
            {
                Dock = DockStyle.Top
            };

            // --- Datei ---
            var miFile = new ToolStripMenuItem("&Datei");

            //var miFileOpen = new ToolStripMenuItem("Öffnen...", null, (s, e) => BtnOpen_Click(s, e))
            //{
            //    ShortcutKeys = Keys.Control | Keys.O
            //};

            //var miFileSave = new ToolStripMenuItem("Speichern", null, (s, e) => SaveWithPrompt())
            //{
            //    ShortcutKeys = Keys.Control | Keys.S
            //};

            //var miFileSaveAs = new ToolStripMenuItem("Speichern unter...", null, (s, e) => SaveAsWithFormat())
            //{
            //    ShortcutKeys = Keys.Control | Keys.Shift | Keys.S
            //};

            //var miFileExportSel = new ToolStripMenuItem("Auswahl exportieren...", null, (s, e) => ExportSelectionAs())
            //{
            //    ShortcutKeys = Keys.Control | Keys.Shift | Keys.E
            //};

            //var miFileExit = new ToolStripMenuItem("Beenden", null, (s, e) => Close());

            miFile.DropDownItems.AddRange(new ToolStripItem[]
            {
        miFileOpen,
        new ToolStripSeparator(),
        miFileSave,
        miFileSaveAs,
        new ToolStripSeparator(),
        miFileExportSel,
        new ToolStripSeparator(),
        miFileExit
            });

            // --- Ansicht ---
            var miView = new ToolStripMenuItem("&Ansicht");

            

            //var miViewZoomAll = new ToolStripMenuItem("Alles anzeigen", null, (s, e) => ZoomAll())
            //{
            //    ShortcutKeys = Keys.Control | Keys.NumPad0
            //};

            miView.DropDownItems.AddRange(new ToolStripItem[]
            {
        miViewZoomAll
            });

            // --- Theme ---
            var miTheme = new ToolStripMenuItem("&Theme");

            _miThemeLight = new ToolStripMenuItem("Light", null, (s, e) =>
            {
                _currentThemeMode = ThemeMode.Light;
                ApplyTheme();
                SaveThemeSettings();
                UpdateThemeMenuChecks();
            })
            {
                CheckOnClick = false
            };

            _miThemeDark = new ToolStripMenuItem("Dark", null, (s, e) =>
            {
                _currentThemeMode = ThemeMode.Dark;
                ApplyTheme();
                SaveThemeSettings();
                UpdateThemeMenuChecks();
            })
            {
                CheckOnClick = false
            };

            var miThemeSettings = new ToolStripMenuItem("Einstellungen...", null, (s, e) => OpenThemeSettings())
            {
                ShortcutKeys = Keys.Control | Keys.T
            };

            // Presets
            var miPresets = new ToolStripMenuItem("Presets");
            var miPresetNeon = new ToolStripMenuItem("Neon", null, (s, e) => ApplyPresetToCurrentTheme(ApplyPresetNeon));
            var miPresetConsole = new ToolStripMenuItem("Console Green", null, (s, e) => ApplyPresetToCurrentTheme(ApplyPresetConsoleGreen));
            var miPresetSunset = new ToolStripMenuItem("Warm Sunset", null, (s, e) => ApplyPresetToCurrentTheme(ApplyPresetWarmSunset));

            miPresets.DropDownItems.AddRange(new ToolStripItem[]
            {
        miPresetNeon,
        miPresetConsole,
        miPresetSunset
            });

            miTheme.DropDownItems.AddRange(new ToolStripItem[]
            {
        _miThemeLight,
        _miThemeDark,
        new ToolStripSeparator(),
        miThemeSettings,
        new ToolStripSeparator(),
        miPresets
            });

            _menuStrip.Items.AddRange(new ToolStripItem[]
            {
        miFile,
        miView,
        miTheme
            });

            MainMenuStrip = _menuStrip;

            // === Controls anordnen (Reihenfolge für DockStyle.Top wichtig!) ===
            Controls.Add(_detailView);    // Fill
            Controls.Add(_overviewPanel); // Top (unterhalb TopPanel)
            Controls.Add(_topPanel);      // Top (unterhalb Menü)
            Controls.Add(_menuStrip);     // Top (ganz oben)

            //// === Playback-Timer (UI-Thread) ===
            //_playbackTimer = new System.Windows.Forms.Timer
            //{
            //    Interval = 16
            //};
            //_playbackTimer.Tick += PlaybackTimer_Tick;

            //FormClosing += MainForm_FormClosing;
            //Resize += MainForm_Resize;
        }
        private Image resizeIconImage(Image icon)
        {
            Image img = icon;
            if (icon.Width > 32 || icon.Height > 32)
            {
                img = new Bitmap(icon, new Size(24, 24));
            }

            return img;
        }
        private void EnableHoverZoom(Control c, int grow = 6)
        {
            c.MouseEnter += (s, e) =>
            {
                if (!_hoverBackup.ContainsKey(c))
                    _hoverBackup[c] = (c.Size, c.Location);

                var old = _hoverBackup[c];

                c.Size = new Size(old.size.Width + grow, old.size.Height + grow);

                // zentrieren (damit der Button nicht springt)
                c.Location = new Point(
                    old.loc.X - grow / 2,
                    old.loc.Y - grow / 2
                );
            };

            c.MouseLeave += (s, e) =>
            {
                if (_hoverBackup.ContainsKey(c))
                {
                    var old = _hoverBackup[c];
                    c.Size = old.size;
                    c.Location = old.loc;
                }
            };
        }

        private void StyleToolbarButton(Button btn, Image icon, string text = null)
        {
            // Icon ggf. auf 24x24 runterskalieren – deine PNGs sind ja relativ groß
            Image img = resizeIconImage(icon);

            //wir haben das icon als button.background gesetzt
            //btn.Image = img;  
            //btn.ImageAlign = ContentAlignment.MiddleLeft;

            if (!string.IsNullOrEmpty(text))
            {
                btn.Text = text;
                btn.TextImageRelation = TextImageRelation.ImageAboveText;
                btn.Padding = new Padding(1, 0, 1, 0);
            }
            else
            {
                // Nur Icon
                btn.Text = "";
            }

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.CheckedBackColor = Color.Black;
            btn.BackColor = Color.Transparent;
        }

        private void InitThemes()
        {
            _lightTheme = new ThemeDefinition { Waveform = new WaveformViewTheme() };
            _darkTheme = new ThemeDefinition { Waveform = new WaveformViewTheme() };

            SetDefaultLightTheme(_lightTheme);
            SetDefaultDarkTheme(_darkTheme);

            _currentThemeMode = ThemeMode.Dark; // Start im Dark-Mode
        }
        private void UpdateThemeMenuChecks()
        {
            if (_miThemeLight == null || _miThemeDark == null)
                return;

            _miThemeLight.Checked = _currentThemeMode == ThemeMode.Light;
            _miThemeDark.Checked = _currentThemeMode == ThemeMode.Dark;
        }

        private void ApplyTheme()
        {
            var t = CurrentTheme;

            BackColor = t.FormBack;

            if (_overviewPanel != null)
                _overviewPanel.BackColor = t.FormBack;

            if (_topPanel != null)
                _topPanel.BackColor = t.FormBack;

            if (_lblInfo != null)
            {
                _lblInfo.ForeColor = t.Waveform.TextColor;
                _lblInfo.BackColor = Color.Transparent;
            }

            StyleButtons(_topPanel, t);
            UpdatePlayButtonVisualState();

            if (_overviewView != null)
                _overviewView.ApplyTheme(t.Waveform);

            if (_detailView != null)
                _detailView.ApplyTheme(t.Waveform);
        }
        private void StyleButtons(Control parent, ThemeDefinition theme)
        {
            if (parent == null)
                return;

            foreach (Control c in parent.Controls)
            {
                if (c is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.ForeColor = theme.ButtonFore;

                    // Grundfarbe
                    btn.BackColor = theme.ButtonBack;

                    // Toolbar-Buttons ohne Border, andere mit 1px
                    if (btn.Parent == _topPanel)
                    {
                        btn.FlatAppearance.BorderSize = 0;
                    }
                    else
                    {
                        btn.FlatAppearance.BorderSize = 1;
                        btn.FlatAppearance.BorderColor = theme.ButtonBorder;
                    }

                    // Deutlichere Hover-/Down-Farben
                    var hover = AdjustBrightness(theme.ButtonBack, 0.35f); // 35% heller
                    var down = AdjustBrightness(theme.ButtonBack, 0.55f); // 55% heller

                    btn.FlatAppearance.MouseOverBackColor = hover;
                    btn.FlatAppearance.MouseDownBackColor = down;
                }
                else if (c is CheckBox cb && cb.Appearance == Appearance.Button)
                {
                    // ⏺ Spezialfall: Loop-Button
                    if (cb == _chkLoop)
                    {
                        cb.FlatStyle = FlatStyle.Flat;

                        // dezente Grundfläche
                        cb.BackColor = theme.ButtonBack;

                        // zarter Ring
                        cb.FlatAppearance.BorderSize = 1;
                        cb.FlatAppearance.BorderColor = AdjustBrightness(theme.ButtonBorder, 0.40f);

                        // sanftes Glow beim Hover
                        cb.FlatAppearance.MouseOverBackColor =
                            AdjustBrightness(theme.ButtonBack, 0.25f);

                        // etwas stärker beim Klick
                        cb.FlatAppearance.MouseDownBackColor =
                            AdjustBrightness(theme.ButtonBack, 0.45f);

                        // Checked leicht hervorgehoben
                        cb.FlatAppearance.CheckedBackColor =
                            AdjustBrightness(theme.ButtonBack, 0.30f);

                        cb.ForeColor = theme.ButtonFore;
                    }
                    else
                    {
                        // generische Button-Checkboxen
                        cb.FlatStyle = FlatStyle.Flat;
                        cb.BackColor = theme.ButtonBack;
                        cb.ForeColor = theme.ButtonFore;
                        cb.FlatAppearance.BorderSize = 0;

                        var hover = AdjustBrightness(theme.ButtonBack, 0.35f);
                        var down = AdjustBrightness(theme.ButtonBack, 0.55f);
                        cb.FlatAppearance.MouseOverBackColor = hover;
                        cb.FlatAppearance.MouseDownBackColor = down;
                        cb.FlatAppearance.CheckedBackColor =
                            AdjustBrightness(theme.ButtonBack, 0.60f);
                    }
                }

                if (c.HasChildren)
                    StyleButtons(c, theme);


            }
        }

        // Hellt eine Farbe um 'amount' auf (0 = unverändert, 0.2 = 20% Richtung Weiß)
        private static Color AdjustBrightness(Color c, float amount)
        {
            amount = Math.Max(-1f, Math.Min(1f, amount));
            float r = c.R, g = c.G, b = c.B;

            if (amount >= 0)
            {
                r += (255 - r) * amount;
                g += (255 - g) * amount;
                b += (255 - b) * amount;
            }
            else
            {
                float f = -amount;
                r -= r * f;
                g -= g * f;
                b -= b * f;
            }

            return Color.FromArgb(c.A, (int)r, (int)g, (int)b);
        }

        // Mischt zwei Farben (0 = nur a, 1 = nur b)
        private static Color Blend(Color a, Color b, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            int r = (int)(a.R + (b.R - a.R) * amount);
            int g = (int)(a.G + (b.G - a.G) * amount);
            int bb = (int)(a.B + (b.B - a.B) * amount);
            return Color.FromArgb(a.A, r, g, bb);
        }


        internal static void SetDefaultLightTheme(ThemeDefinition t)
        {
            if (t.Waveform == null) t.Waveform = new WaveformViewTheme();
            t.Mode = ThemeMode.Light;
            t.FormBack = Color.WhiteSmoke;
            t.ButtonBack = Color.WhiteSmoke;
            t.ButtonFore = Color.Black;
            t.ButtonBorder = Color.DarkGray;

            t.Waveform.Background = Color.White;
            t.Waveform.WaveColor = Color.ForestGreen;
            t.Waveform.ZeroLineColor = Color.DarkGray;
            t.Waveform.SelectionFillColor = Color.FromArgb(80, Color.Gold);
            t.Waveform.SelectionEdgeColor = Color.Gold;
            t.Waveform.PlayheadColor = Color.Red;
            t.Waveform.TextColor = Color.Black;
        }

        internal static void SetDefaultDarkTheme(ThemeDefinition t)
        {
            if (t.Waveform == null) t.Waveform = new WaveformViewTheme();
            t.Mode = ThemeMode.Dark;

            // Fensterhintergrund etwas heller als ganz schwarz
            t.FormBack = Color.FromArgb(32, 32, 32);
            t.ButtonBack = Color.FromArgb(55, 55, 55);
            t.ButtonFore = Color.WhiteSmoke;
            t.ButtonBorder = Color.DimGray;

            t.Waveform.Background = Color.Black;

            // Wellenfarbe ruhig knallig lassen
            t.Waveform.WaveColor = Color.Lime;

            // 0-Linie gut sichtbar
            t.Waveform.ZeroLineColor = Color.Gray;

            // Auswahl etwas kräftiger gelb
            t.Waveform.SelectionFillColor = Color.FromArgb(110, Color.Yellow);
            t.Waveform.SelectionEdgeColor = Color.Gold;

            t.Waveform.PlayheadColor = Color.Red;

            // 👇 Text richtig hell machen
            t.Waveform.TextColor = Color.WhiteSmoke;
        }


        private void OverviewView_SelectionChanged(int startSample, int endSample)
        {
            if (endSample <= startSample)
                return;

            int length = endSample - startSample;

            // Zoom im Detail: wie vorher
            _detailView.VisibleStartSample = startSample;
            _detailView.VisibleSampleCount = length;

            // Loop-Bereich merken wir NICHT mehr hier,
            // sondern fragen später direkt bei den Views nach.
        }
        private void OpenThemeSettings()
        {
            // themes.json neu lesen (falls im Editor geändert)
            LoadThemeDefaultsFromFile();

            using (var dlg = new ThemeSettingsForm(_lightTheme, _darkTheme, _currentThemeMode))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _currentThemeMode = dlg.SelectedMode;

                    ApplyTheme();

                    // Mode merken (settings.json)
                    SaveThemeSettings();

                    // NEU: aktuelle Farben als neue Defaults in themes.json schreiben
                    SaveThemeDefaults(force: true);
                }
                else
                {
                    // falls du im Dialog rumgespielt hast, aber abbrichst:
                    ApplyTheme();
                }

                UpdateThemeMenuChecks();
            }
        }
        public static void SaveThemeDefaults(bool force = false)
        {
            string path = GetThemeDefaultsFilePath();

            if (!force && File.Exists(path))
                return;

            var defaults = new ThemeDefaults
            {
                Light = ThemeToColorSet(_instance._lightTheme),
                Dark = ThemeToColorSet(_instance._darkTheme)
            };

            var json = JsonSerializer.Serialize(
                defaults,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(path, json);
        }

        private void ApplyPresetToCurrentTheme(Action<ThemeDefinition> presetApplier)
        {
            if (presetApplier == null) return;

            var t = CurrentTheme;
            presetApplier(t);

            ApplyTheme();
            SaveThemeSettings();
        }
        private static void ApplyPresetNeon(ThemeDefinition t)
        {
            // Hintergrund sehr dunkel, Wave knallgrün / magenta
            t.FormBack = Color.FromArgb(16, 16, 28);
            t.ButtonBack = Color.FromArgb(40, 40, 70);
            t.ButtonFore = Color.WhiteSmoke;
            t.ButtonBorder = Color.DeepSkyBlue;

            t.Waveform.Background = Color.Black;
            t.Waveform.WaveColor = Color.Lime;
            t.Waveform.ZeroLineColor = Color.MediumPurple;
            t.Waveform.SelectionFillColor = Color.FromArgb(120, Color.DeepPink);
            t.Waveform.SelectionEdgeColor = Color.HotPink;
            t.Waveform.PlayheadColor = Color.Cyan;
            t.Waveform.TextColor = Color.WhiteSmoke;
        }

        private static void ApplyPresetConsoleGreen(ThemeDefinition t)
        {
            t.FormBack = Color.Black;
            t.ButtonBack = Color.FromArgb(10, 40, 10);
            t.ButtonFore = Color.LawnGreen;
            t.ButtonBorder = Color.LimeGreen;

            t.Waveform.Background = Color.Black;
            t.Waveform.WaveColor = Color.Lime;
            t.Waveform.ZeroLineColor = Color.FromArgb(0, 80, 0);
            t.Waveform.SelectionFillColor = Color.FromArgb(120, 0, 100, 0);
            t.Waveform.SelectionEdgeColor = Color.LimeGreen;
            t.Waveform.PlayheadColor = Color.Chartreuse;
            t.Waveform.TextColor = Color.LawnGreen;
        }

        private static void ApplyPresetWarmSunset(ThemeDefinition t)
        {
            t.FormBack = Color.FromArgb(35, 20, 20);
            t.ButtonBack = Color.FromArgb(70, 40, 30);
            t.ButtonFore = Color.Moccasin;
            t.ButtonBorder = Color.SandyBrown;

            t.Waveform.Background = Color.FromArgb(20, 8, 8);
            t.Waveform.WaveColor = Color.Orange;
            t.Waveform.ZeroLineColor = Color.SaddleBrown;
            t.Waveform.SelectionFillColor = Color.FromArgb(120, Color.OrangeRed);
            t.Waveform.SelectionEdgeColor = Color.Gold;
            t.Waveform.PlayheadColor = Color.LightGoldenrodYellow;
            t.Waveform.TextColor = Color.Bisque;
        }


        private void DetailView_SelectionChanged(int startSample, int endSample)
        {
            // Hier musst du eigentlich nichts mehr tun,
            // außer evtl. später UI-Info updaten.
            // Loop-Bereich wird direkt aus der aktuellen Selektion gelesen.
        }


        // Tastatur-Shortcuts
        //protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        //{
        //    // CTRL+O -> Öffnen
        //    if (keyData == (Keys.Control | Keys.O))
        //    {
        //        BtnOpen_Click(null, EventArgs.Empty);
        //        return true;
        //    }

        //    // CTRL+T -> Theme-Settings
        //    if (keyData == (Keys.Control | Keys.T))
        //    {
        //        OpenThemeSettings();
        //        return true;
        //    }

        //    // SPACE -> Play/Stop
        //    if (keyData == Keys.Space)
        //    {
        //        TogglePlayStop();
        //        return true;
        //    }

        //    // CTRL+Z -> Undo
        //    if (keyData == (Keys.Control | Keys.Z))
        //    {
        //        Undo();
        //        return true;
        //    }

        //    // L -> Loop Toggle
        //    if (keyData == Keys.L)
        //    {
        //        _chkLoop.Checked = !_chkLoop.Checked;
        //        return true;
        //    }

        //    // DEL -> Bereich löschen
        //    if (keyData == Keys.Delete)
        //    {
        //        BtnDeleteSelection_Click(null, EventArgs.Empty);
        //        return true;
        //    }

        //    // NUM 1 -> an den Anfang der Selektion springen
        //    if (keyData == Keys.NumPad1)
        //    {
        //        JumpToSelectionEdge(toStart: true);
        //        return true;
        //    }

        //    // NUM 2 -> ans Ende der Selektion springen
        //    if (keyData == Keys.NumPad2)
        //    {
        //        JumpToSelectionEdge(toStart: false);
        //        return true;
        //    }

        //    // NUM 0 -> an den Anfang des ganzen Clips springen
        //    if (keyData == Keys.NumPad0)
        //    {
        //        JumpToStartOfFile();
        //        return true;
        //    }

        //    // CTRL+NUM 0 -> View All (alles auszoomen)
        //    if (keyData == (Keys.Control | Keys.NumPad0))
        //    {
        //        ZoomAll();
        //        return true;
        //    }

        //    // CTRL+N -> Bereich normalisieren
        //    if (keyData == (Keys.Control | Keys.N))
        //    {
        //        NormalizeSelection();
        //        return true;
        //    }

        //    // CTRL+E -> Bereich exportieren
        //    if (keyData == (Keys.Control | Keys.E))
        //    {
        //        ExportSelection();
        //        return true;
        //    }

        //    // CTRL+S -> Datei speichern (Overwrite/Rename)
        //    if (keyData == (Keys.Control | Keys.S))
        //    {
        //        SaveWithPrompt(this, null);
        //        return true;
        //    }

        //    // CTRL+A -> Alles selektieren
        //    if (keyData == (Keys.Control | Keys.A))
        //    {
        //        SelectAll();
        //        return true;
        //    }

        //    // CTRL+SHIFT+S -> Save As (Format wählen)
        //    if (keyData == (Keys.Control | Keys.Shift | Keys.S))
        //    {
        //        SaveAsWithFormat();
        //        return true;
        //    }

        //    // CTRL+SHIFT+E -> Export Selection As (Format wählen)
        //    if (keyData == (Keys.Control | Keys.Shift | Keys.E))
        //    {
        //        ExportSelectionAs();
        //        return true;
        //    }

        //    return base.ProcessCmdKey(ref msg, keyData);
        //}
        private void JumpToStartOfFile()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // nutzt deine bestehende Logik (inkl. Playhead+Restart)
            JumpToSample(0, restartIfPlaying: true);
        }


        public interface IPositionedSampleProvider : ISampleProvider
        {
            int PositionSamples { get; }
        }

        private void OverviewView_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // Ganze Datei markieren
            _overviewView.SetSelection(0, _currentSamples.Length, raiseEvent: true);
            // SelectionChanged-Event kümmert sich darum,
            // dass der Detail-View entsprechend gezoomt wird.
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            UpdatePlaybackTimerInterval();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _isClosing = true;

            if (_playbackTimer != null)
            {
                _playbackTimer.Stop();
                _playbackTimer.Tick -= PlaybackTimer_Tick;
                _playbackTimer.Dispose();
                _playbackTimer = null;
            }

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                try { _waveOut.Stop(); } catch { }
                _waveOut.Dispose();
                _waveOut = null;
            }

            _currentProvider = null;
        }

        //private void BtnOpen_Click(object sender, EventArgs e)
        //{
        //    using var ofd = new OpenFileDialog
        //    {
        //        Filter = "Audio-Dateien|*.wav;*.mp3;*.flac;*.aiff;*.wma;*.m4a|Alle Dateien|*.*",
        //        Title = "Audiodatei öffnen"
        //    };

        //    if (ofd.ShowDialog(this) != DialogResult.OK)
        //        return;

        //    try
        //    {
        //        LoadAudioFile(ofd.FileName);
        //        _lblInfo.Text = ofd.FileName;
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show(this,
        //            "Fehler beim Laden der Datei:\n" + ex.Message,
        //            "Fehler",
        //            MessageBoxButtons.OK,
        //            MessageBoxIcon.Error);
        //    }
        //}

        private void LoadAudioFile(string filePath)
        {
            using var reader = new NAudio.Wave.AudioFileReader(filePath);

            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;

            _currentSampleRate = sampleRate;
            var monoSamples = new List<float>();

            // WaveViews über Samplerate informieren
            _overviewView.SampleRate = _currentSampleRate;
            _detailView.SampleRate = _currentSampleRate;

            _currentSamples = monoSamples.ToArray();

            _overviewView.Samples = _currentSamples;
            _overviewView.VisibleStartSample = 0;
            _overviewView.VisibleSampleCount = 0;

            _detailView.Samples = _currentSamples;
            _detailView.VisibleStartSample = 0;
            _detailView.VisibleSampleCount = 0;



            // 1 Sekunde Puffer
            float[] buffer = new float[sampleRate * channels];
            int read;

            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                int frameCount = read / channels;
                for (int i = 0; i < frameCount; i++)
                {
                    float sum = 0f;
                    for (int ch = 0; ch < channels; ch++)
                        sum += buffer[i * channels + ch];

                    float mono = sum / channels;
                    if (mono > 1f) mono = 1f;
                    if (mono < -1f) mono = -1f;

                    monoSamples.Add(mono);
                }
            }

            _currentSamples = monoSamples.ToArray();

            _overviewView.Samples = _currentSamples;
            _overviewView.VisibleStartSample = 0;
            _overviewView.VisibleSampleCount = 0; // ganzer Track

            _detailView.Samples = _currentSamples;
            _detailView.VisibleStartSample = 0;
            _detailView.VisibleSampleCount = 0; // zunächst auch ganzer Track

            _playbackSamplePosition = 0;
            _overviewView.PlaybackSample = 0;
            _detailView.PlaybackSample = 0;

            _undoStack.Clear();

            UpdateInfo(sampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _currentFilePath = filePath;
            _isDirty = false;
            UpdateWindowTitle();
        }
        private void UpdateWindowTitle()
        {
            string filePart = string.IsNullOrEmpty(_currentFilePath)
                ? "Kein File"
                : Path.GetFileName(_currentFilePath);

            Text = $"Minimal Sound Editor - {filePart}" + (_isDirty ? " *" : "");
        }

        private void UpdateInfo(int sampleRate = 0, int sampleCount = 0)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
            {
                _lblInfo.Text = "Keine Datei geladen";
                return;
            }

            if (sampleRate == 0 || sampleCount == 0)
            {
                _lblInfo.Text = $"Samples: {_currentSamples.Length}";
                return;
            }

            _trackDurationSeconds = sampleCount / (double)sampleRate;
            _lblInfo.Text = $"Samples: {sampleCount} | Dauer: {_trackDurationSeconds:0.00} s";
        }

        //private void UpdatePlaybackTimerInterval()
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0 || _currentSampleRate <= 0)
        //        return;

        //    int width = _detailView.Width;
        //    if (width <= 0)
        //        width = 1000;

        //    _trackDurationSeconds = _currentSamples.Length / (double)_currentSampleRate;

        //    double idealMs = (_trackDurationSeconds * 1000.0) / width;

        //    int intervalMs = (int)Math.Max(2, Math.Min(idealMs, 15));

        //    if (_playbackTimer != null)
        //        _playbackTimer.Interval = intervalMs;
        //}

        private void UpdatePlaybackTimerInterval()
        {
            // Einfach konstant lassen – Windows-Timer kann eh nicht besser als ~15 ms
            if (_playbackTimer != null)
                _playbackTimer.Interval = 16;   // ~60 FPS
        }

        //private void BtnUndo_Click(object sender, EventArgs e)
        //{
        //    Undo();
        //}
        //private void BtnDeleteSelection_Click(object sender, EventArgs e)
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0)
        //        return;

        //    // Undo sichern
        //    _undoStack.Push(CloneSamples(_currentSamples));

        //    // Nur im Detail-Track löschen
        //    _detailView.DeleteSelection();

        //    // Loop-Bereich ungültig, weil sich die Samples verschoben haben
        //    _chkLoop.Checked = false;

        //    // Samples aus Detail-View übernehmen
        //    _currentSamples = _detailView.Samples ?? Array.Empty<float>();
        //    _overviewView.Samples = _currentSamples;

        //    if (_currentSamples.Length == 0)
        //    {
        //        _playbackSamplePosition = 0;
        //        _overviewView.PlaybackSample = 0;
        //        _detailView.PlaybackSample = 0;
        //    }
        //    else if (_playbackSamplePosition >= _currentSamples.Length)
        //    {
        //        _playbackSamplePosition = _currentSamples.Length - 1;
        //        _overviewView.PlaybackSample = _playbackSamplePosition;
        //        _detailView.PlaybackSample = _playbackSamplePosition;
        //    }

        //    UpdateInfo(_currentSampleRate, _currentSamples.Length);
        //    UpdatePlaybackTimerInterval();

        //    _isDirty = true;
        //    UpdateWindowTitle();
        //}

        private float[] CloneSamples(float[] src)
        {
            if (src == null) return Array.Empty<float>();
            var copy = new float[src.Length];
            Array.Copy(src, copy, src.Length);
            return copy;
        }

        private void Undo()
        {
            if (_undoStack.Count == 0)
                return;

            _currentSamples = _undoStack.Pop();

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            _overviewView.VisibleStartSample = 0;
            _overviewView.VisibleSampleCount = 0;

            _detailView.VisibleStartSample = 0;
            _detailView.VisibleSampleCount = 0;

            _playbackSamplePosition = 0;
            _overviewView.PlaybackSample = 0;
            _detailView.PlaybackSample = 0;

            UpdateInfo(_currentSampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _chkLoop.Checked = false;
        }

        // PLAY
        //private void BtnPlay_Click(object sender, EventArgs e)
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0)
        //    {
        //        MessageBox.Show(this, "Keine Audiodatei geladen.", "Hinweis",
        //            MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return;
        //    }

        //    if (_waveOut != null)
        //    {
        //        _waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
        //        try { _waveOut.Stop(); } catch { }
        //        _waveOut.Dispose();
        //        _waveOut = null;
        //    }

        //    //_waveOut = new WaveOutEvent
        //    //{
        //    //    DesiredLatency = 50,   // ca. 50 ms Gesamtlatenz
        //    //    NumberOfBuffers = 2    // 2 kleine Buffer
        //    //};

        //    // etwas konservativere Standard-Einstellungen verwenden
        //    _waveOut = new WaveOutEvent();

        //    // Prüfen, ob wir loop-fähig sind
        //    bool hasLoopSelection = false;
        //    int loopStart = 0;
        //    int loopEnd = 0;

        //    if (_loopEnabled)
        //    {
        //        // 1) bevorzugt: Selektion im Detail-Track
        //        if (_detailView.TryGetSelection(out var ds, out var de) && de > ds)
        //        {
        //            hasLoopSelection = true;
        //            loopStart = ds;
        //            loopEnd = de;
        //        }
        //        // 2) falls dort nichts: Selektion im Overview-Track
        //        else if (_overviewView.TryGetSelection(out var os, out var oe) && oe > os)
        //        {
        //            hasLoopSelection = true;
        //            loopStart = os;
        //            loopEnd = oe;
        //        }
        //    }

        //    if (hasLoopSelection)
        //    {
        //        // Loop von aktueller Selektion
        //        _currentProvider = new LoopingArraySampleProvider(
        //            _currentSamples,
        //            _currentSampleRate,
        //            1,
        //            loopStart,
        //            loopEnd);

        //        // Playhead am Loop-Anfang
        //        _playbackSamplePosition = loopStart;
        //        _overviewView.PlaybackSample = _playbackSamplePosition;
        //        _detailView.PlaybackSample = _playbackSamplePosition;
        //    }
        //    else
        //    {
        //        // normales Playback ab aktuellem Playhead
        //        _currentProvider = new SimpleArraySampleProvider(
        //            _currentSamples,
        //            _currentSampleRate,
        //            1,
        //            _playbackSamplePosition);
        //    }


        //    _waveOut.Init(_currentProvider);
        //    _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
        //    _waveOut.Play();

        //    _playbackTimer?.Start();

        //}

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (_isClosing)
                return;

            try
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (_playbackTimer != null)
                            _playbackTimer.Stop();
                        UpdatePlayButtonVisualState();
                    }));
                }
            }
            catch { }
        }

        // STOP
        //private void BtnStop_Click(object sender, EventArgs e)
        //{
        //    if (_waveOut == null)
        //        return;

        //    try { _waveOut.Stop(); _playbackTimer?.Stop(); } catch { }
        //    _playbackTimer?.Stop();
        //}

        private void TogglePlayStop()
        {
            if (_waveOut == null || _waveOut.PlaybackState != PlaybackState.Playing)
            {
                BtnPlay_Click(null, EventArgs.Empty);
            }
            else
            {
                BtnStop_Click(null, EventArgs.Empty);
            }
        }

        // Timer aktualisiert Playhead
        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (_isClosing)
                return;

            if (_currentProvider == null || _currentSamples == null || _currentSamples.Length == 0)
            {
                _playbackTimer?.Stop();
                return;
            }

            if (_waveOut == null || _waveOut.PlaybackState != PlaybackState.Playing)
                return;

            int pos = _currentProvider.PositionSamples;
            if (pos < 0) pos = 0;
            if (pos >= _currentSamples.Length)
                pos = _currentSamples.Length - 1;

            _playbackSamplePosition = pos;
            _overviewView.PlaybackSample = pos;
            _detailView.PlaybackSample = pos;
        }

        // Klick in Overview/Detail -> Playhead setzen
        private void Waveform_PlaybackPositionChangedByClick(int sampleIndex)
        {
            // nutzt jetzt denselben Weg wie Tastatur-Sprünge
            JumpToSample(sampleIndex, restartIfPlaying: true);
        }
        //private void ZoomAll()
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0)
        //        return;

        //    _detailView.VisibleStartSample = 0;
        //    _detailView.VisibleSampleCount = 0; // 0 = „ganzer Track“
        //}
        private void NormalizeSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Normalisieren",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryGetNormalizeTarget(out float targetPeak))
                return; // User abgebrochen

            // Maximalen Absolutwert im Bereich finden
            float maxAbs = 0f;
            for (int i = start; i < end; i++)
            {
                float v = Math.Abs(_currentSamples[i]);
                if (v > maxAbs) maxAbs = v;
            }

            if (maxAbs <= 0f)
            {
                MessageBox.Show(this, "Der Bereich ist komplett stumm – kann nicht normalisiert werden.",
                    "Normalisieren", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            float factor = targetPeak / maxAbs;

            for (int i = start; i < end; i++)
            {
                float v = _currentSamples[i] * factor;
                if (v > 1f) v = 1f;
                if (v < -1f) v = -1f;
                _currentSamples[i] = v;
            }

            // Views neu füttern + Zoom & Selektion wiederherstellen
            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            _detailView.VisibleStartSample = oldVisibleStart;
            _detailView.VisibleSampleCount = oldVisibleCount;

            _overviewView.SetSelection(start, end, raiseEvent: false);
            _detailView.SetSelection(start, end, raiseEvent: false);

            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            UpdateInfo(_currentSampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _isDirty = true;
            UpdateWindowTitle();
        }
        private bool TryGetNormalizeTarget(out float targetLinear)
        {
            // Standard: -2 dBFS
            targetLinear = 1.0f;

            using (var form = new Form())
            using (var nudDb = new NumericUpDown())
            using (var lbl = new Label())
            using (var btn0dB = new Button())
            using (var btnMinus2dB = new Button())
            using (var btnMinus6dB = new Button())
            using (var btnOk = new Button())
            using (var btnCancel = new Button())
            {
                form.Text = "Bereich normalisieren (Peak)";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.ClientSize = new System.Drawing.Size(320, 160);
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;

                // Label
                lbl.Text = "Ziel-Pegel (dBFS, 0 bis -60 dB):";
                lbl.AutoSize = true;
                lbl.Left = 10;
                lbl.Top = 10;

                // NumericUpDown für dB
                nudDb.Left = 10;
                nudDb.Top = 35;
                nudDb.Width = 80;
                nudDb.DecimalPlaces = 1;
                nudDb.Minimum = -60.0M;
                nudDb.Maximum = 0.0M;
                nudDb.Increment = 0.5M;
                nudDb.Value = -2.0M; // üblicher Standard

                // Preset-Buttons
                btn0dB.Text = "0 dB";
                btn0dB.Left = 110;
                btn0dB.Top = 30;
                btn0dB.Width = 60;
                btn0dB.Click += (s, e) => nudDb.Value = 0.0M;

                btnMinus2dB.Text = "-2 dB";
                btnMinus2dB.Left = 180;
                btnMinus2dB.Top = 30;
                btnMinus2dB.Width = 60;
                btnMinus2dB.Click += (s, e) => nudDb.Value = -2.0M;

                btnMinus6dB.Text = "-6 dB";
                btnMinus6dB.Left = 250;
                btnMinus6dB.Top = 30;
                btnMinus6dB.Width = 60;
                btnMinus6dB.Click += (s, e) => nudDb.Value = -6.0M;

                // OK / Cancel
                btnOk.Text = "Normalize";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Left = 70;
                btnOk.Top = 100;
                btnOk.Width = 90;

                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Left = 170;
                btnCancel.Top = 100;
                btnCancel.Width = 90;

                form.Controls.Add(lbl);
                form.Controls.Add(nudDb);
                form.Controls.Add(btn0dB);
                form.Controls.Add(btnMinus2dB);
                form.Controls.Add(btnMinus6dB);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);

                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                var result = form.ShowDialog(this);
                if (result != DialogResult.OK)
                    return false;

                // dB -> linearer Peak (0 dB = 1.0, -6 dB ~ 0.501)
                decimal db = nudDb.Value;
                double dbDouble = (double)db;
                double linear = Math.Pow(10.0, dbDouble / 20.0);

                if (linear > 1.0) linear = 1.0;      // Sicherheit
                if (linear < 0.0) linear = 0.0;

                targetLinear = (float)linear;
                return true;
            }
        }

        private void ExportSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Exportieren",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 0)
                return;

            string defaultName = "selection.wav";
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(_currentFilePath);
                defaultName = baseName + "_selection.wav";
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "WAV-Datei (*.wav)|*.wav";
                sfd.FileName = defaultName;

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                using var writer = new WaveFileWriter(
                    sfd.FileName,
                    WaveFormat.CreateIeeeFloatWaveFormat(_currentSampleRate, 1));

                writer.WriteSamples(_currentSamples, start, length);
            }
        }
        private void SaveCurrentFile(bool saveAs)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            string path = _currentFilePath;

            if (saveAs || string.IsNullOrEmpty(path) ||
                !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "WAV-Datei (*.wav)|*.wav",
                    FileName = string.IsNullOrEmpty(_currentFilePath)
                        ? "audio.wav"
                        : Path.GetFileNameWithoutExtension(_currentFilePath) + "_edited.wav"
                };

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return;

                path = sfd.FileName;
                _currentFilePath = path;
            }

            using (var writer = new WaveFileWriter(
                path,
                WaveFormat.CreateIeeeFloatWaveFormat(_currentSampleRate, 1)))
            {
                writer.WriteSamples(_currentSamples, 0, _currentSamples.Length);
            }

            _isDirty = false;
            UpdateWindowTitle();
        }

        //private void SaveWithPrompt()
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0)
        //        return;

        //    // Falls wir schon eine WAV-Datei haben: Nachfrage Overwrite/Rename
        //    if (!string.IsNullOrEmpty(_currentFilePath) &&
        //        _currentFilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        //    {
        //        var result = MessageBox.Show(this,
        //            $"Datei überschreiben?\n{_currentFilePath}\n\n" +
        //            "Yes = überschreiben\nNo = unter neuem Namen speichern\nCancel = abbrechen",
        //            "Speichern",
        //            MessageBoxButtons.YesNoCancel,
        //            MessageBoxIcon.Question);

        //        if (result == DialogResult.Cancel)
        //            return;

        //        if (result == DialogResult.Yes)
        //        {
        //            SaveCurrentFile(saveAs: false);
        //        }
        //        else if (result == DialogResult.No)
        //        {
        //            SaveCurrentFile(saveAs: true);
        //        }
        //    }
        //    else
        //    {
        //        // bisher keine oder kein WAV -> direkt „Speichern unter“
        //        SaveCurrentFile(saveAs: true);
        //    }
        //}
        private void ExportSamplesToFile(float[] samples, int sampleRate, string filePath, AudioExportFormat format)
        {
            if (samples == null || samples.Length == 0)
                return;

            switch (format)
            {
                case AudioExportFormat.Wav:
                    {
                        // WAV bleibt wie gehabt: 32-bit float, mono
                        using (var writer = new WaveFileWriter(
                                   filePath,
                                   WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
                        {
                            writer.WriteSamples(samples, 0, samples.Length);
                        }
                        break;
                    }

                case AudioExportFormat.Mp3:
                case AudioExportFormat.Aac:
                    {
                        // Media Foundation initialisieren (idempotent)
                        MediaFoundationApi.Startup();

                        // 1) float[] -> ISampleProvider (mono)
                        var monoProvider = new SimpleArraySampleProvider(samples, sampleRate, 1, 0);

                        // 2) Mono -> Stereo duplizieren (bessere Kompatibilität)
                        var stereoProvider = new MonoToStereoSampleProvider(monoProvider);

                        // 3) float -> 16-bit PCM
                        var wave16 = new SampleToWaveProvider16(stereoProvider);

                        // 4) Ziel-Samplerate festlegen: 44100 Hz ist safest
                        int targetRate = 44100;
                        var targetFormat = new WaveFormat(targetRate, 16, 2);

                        IWaveProvider source = wave16;

                        // Resampling nur, wenn nötig
                        if (wave16.WaveFormat.SampleRate != targetRate ||
                            wave16.WaveFormat.Channels != 2 ||
                            wave16.WaveFormat.BitsPerSample != 16)
                        {
                            using (var resampler = new MediaFoundationResampler(source, targetFormat))
                            {
                                resampler.ResamplerQuality = 60;

                                int bitrate = 192000; // 192 kbit/s

                                try
                                {
                                    if (format == AudioExportFormat.Mp3)
                                        MediaFoundationEncoder.EncodeToMp3(resampler, filePath, bitrate);
                                    else
                                        MediaFoundationEncoder.EncodeToAac(resampler, filePath, bitrate);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(this,
                                        "Fehler beim Encodieren:\n" + ex.Message,
                                        "Export",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                                }
                            }
                        }
                        else
                        {
                            // dürfte praktisch nie hier landen, aber der Vollständigkeit halber
                            int bitrate = 192000;

                            try
                            {
                                if (format == AudioExportFormat.Mp3)
                                    MediaFoundationEncoder.EncodeToMp3(source, filePath, bitrate);
                                else
                                    MediaFoundationEncoder.EncodeToAac(source, filePath, bitrate);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this,
                                    "Fehler beim Encodieren:\n" + ex.Message,
                                    "Export",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }

                        break;
                    }
            }
        }

        private bool TryChooseExportFileAndFormat(
    bool selectionOnly,
    out string filePath,
    out AudioExportFormat format)
        {
            filePath = null;
            format = AudioExportFormat.Wav;

            string defaultName = selectionOnly ? "selection.wav" : "audio.wav";

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                var baseName = Path.GetFileNameWithoutExtension(_currentFilePath);
                defaultName = selectionOnly ? baseName + "_selection.wav" : baseName + "_edited.wav";
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = selectionOnly ? "Bereich exportieren als..." : "Speichern unter...";
                sfd.FileName = defaultName;
                sfd.Filter =
                    "WAV (*.wav)|*.wav|" +
                    "MP3 (*.mp3)|*.mp3|" +
                    "AAC/MP4 (*.m4a;*.mp4)|*.m4a;*.mp4";

                if (sfd.ShowDialog(this) != DialogResult.OK)
                    return false;

                filePath = sfd.FileName;
            }

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".mp3":
                    format = AudioExportFormat.Mp3;
                    break;
                case ".m4a":
                case ".mp4":
                case ".aac":
                    format = AudioExportFormat.Aac;
                    break;
                default:
                    format = AudioExportFormat.Wav;
                    break;
            }

            return true;
        }
        //private void SaveAsWithFormat()
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0)
        //        return;

        //    if (!TryChooseExportFileAndFormat(selectionOnly: false,
        //            out string path, out AudioExportFormat format))
        //        return;

        //    ExportSamplesToFile(_currentSamples, _currentSampleRate, path, format);

        //    // Nur wenn wir wirklich den „Haupttrack“ speichern:
        //    _currentFilePath = path;
        //    _isDirty = false;
        //    UpdateWindowTitle();
        //}
        private string GetSettingsFilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinimalSoundEditor");

            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
        private void SaveThemeSettings()
        {
            var data = new Dictionary<string, string>
            {
                ["CurrentMode"] = _currentThemeMode.ToString()
            };

            var json = JsonSerializer.Serialize(
                data,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(GetSettingsFilePath(), json);
        }

        private void LoadThemeSettings()
        {
            string path = GetSettingsFilePath();
            if (!File.Exists(path))
                return;

            try
            {
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (data == null) return;

                if (data.TryGetValue("CurrentMode", out var modeStr) &&
                    Enum.TryParse<ThemeMode>(modeStr, out var mode))
                {
                    _currentThemeMode = mode;
                }
            }
            catch
            {
                // egal – dann bleiben wir bei Defaults
            }
        }



        //private void ExportSelectionAs()
        //{
        //    if (_currentSamples == null || _currentSamples.Length == 0)
        //        return;

        //    if (!TryGetCurrentSelection(out int start, out int end))
        //    {
        //        MessageBox.Show(this, "Kein Bereich selektiert.", "Exportieren",
        //            MessageBoxButtons.OK, MessageBoxIcon.Information);
        //        return;
        //    }

        //    int length = end - start;
        //    if (length <= 0)
        //        return;

        //    if (!TryChooseExportFileAndFormat(selectionOnly: true,
        //            out string path, out AudioExportFormat format))
        //        return;

        //    var sel = new float[length];
        //    Array.Copy(_currentSamples, start, sel, 0, length);

        //    ExportSamplesToFile(sel, _currentSampleRate, path, format);
        //}

        private bool TryGetCurrentSelection(out int startSample, out int endSample)
        {
            // zuerst Detail-View
            if (_detailView != null &&
                _detailView.TryGetSelection(out startSample, out endSample) &&
                endSample > startSample)
            {
                return true;
            }

            // dann Overview-View
            if (_overviewView != null &&
                _overviewView.TryGetSelection(out startSample, out endSample) &&
                endSample > startSample)
            {
                return true;
            }

            startSample = 0;
            endSample = 0;
            return false;
        }

        private void JumpToSample(int sampleIndex, bool restartIfPlaying)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            int maxIndex = _currentSamples.Length - 1;
            if (maxIndex < 0) return;

            sampleIndex = Math.Max(0, Math.Min(sampleIndex, maxIndex));

            _playbackSamplePosition = sampleIndex;
            _overviewView.PlaybackSample = sampleIndex;
            _detailView.PlaybackSample = sampleIndex;

            if (restartIfPlaying && _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
            {
                BtnPlay_Click(null, EventArgs.Empty);
            }
        }
        // Repräsentation einer Theme-Farbpalette für die JSON-Datei
        private class ThemeColorSet
        {
            public string FormBack { get; set; }
            public string ButtonBack { get; set; }
            public string ButtonFore { get; set; }
            public string ButtonBorder { get; set; }

            public string WaveBackground { get; set; }
            public string WaveColor { get; set; }
            public string WaveZeroLine { get; set; }
            public string WaveSelectionFill { get; set; }
            public string WaveSelectionEdge { get; set; }
            public string WavePlayhead { get; set; }
            public string WaveText { get; set; }
        }

        // war vorher private
        public static string GetThemeDefaultsFilePath()
        {
            var exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            if (string.IsNullOrEmpty(exeDir))
                exeDir = Environment.CurrentDirectory;

            return Path.Combine(exeDir, "themes.json");
        }

        // war vorher private – jetzt z.B. internal oder public
        public static void SaveThemeDefaultsIfMissing()
        {
            SaveThemeDefaults(force: false);
        }


        // ✅ jetzt statisch, damit wir sie aus SaveThemeDefaultsIfMissing() aufrufen können
        private static ThemeColorSet ThemeToColorSet(ThemeDefinition t)
        {
            return new ThemeColorSet
            {
                FormBack = ColorToHex(t.FormBack),
                ButtonBack = ColorToHex(t.ButtonBack),
                ButtonFore = ColorToHex(t.ButtonFore),
                ButtonBorder = ColorToHex(t.ButtonBorder),

                WaveBackground = ColorToHex(t.Waveform.Background),
                WaveColor = ColorToHex(t.Waveform.WaveColor),
                WaveZeroLine = ColorToHex(t.Waveform.ZeroLineColor),
                WaveSelectionFill = ColorToHex(t.Waveform.SelectionFillColor),
                WaveSelectionEdge = ColorToHex(t.Waveform.SelectionEdgeColor),
                WavePlayhead = ColorToHex(t.Waveform.PlayheadColor),
                WaveText = ColorToHex(t.Waveform.TextColor)
            };
        }



        private void ApplyColorSetToTheme(ThemeColorSet set, ThemeDefinition t)
        {
            if (set == null || t == null || t.Waveform == null)
                return;

            t.FormBack = HexToColor(set.FormBack, t.FormBack);
            t.ButtonBack = HexToColor(set.ButtonBack, t.ButtonBack);
            t.ButtonFore = HexToColor(set.ButtonFore, t.ButtonFore);
            t.ButtonBorder = HexToColor(set.ButtonBorder, t.ButtonBorder);

            t.Waveform.Background = HexToColor(set.WaveBackground, t.Waveform.Background);
            t.Waveform.WaveColor = HexToColor(set.WaveColor, t.Waveform.WaveColor);
            t.Waveform.ZeroLineColor = HexToColor(set.WaveZeroLine, t.Waveform.ZeroLineColor);

            // 👇 Transparente Auswahl
            var selBase = HexToColor(set.WaveSelectionFill, t.Waveform.SelectionFillColor);
            t.Waveform.SelectionFillColor = Color.FromArgb(SelectionFillAlpha, selBase);

            t.Waveform.SelectionEdgeColor = HexToColor(set.WaveSelectionEdge, t.Waveform.SelectionEdgeColor);
            t.Waveform.PlayheadColor = HexToColor(set.WavePlayhead, t.Waveform.PlayheadColor);
            t.Waveform.TextColor = HexToColor(set.WaveText, t.Waveform.TextColor);
        }


        private void LoadThemeDefaultsFromFile()
        {
            string path = GetThemeDefaultsFilePath();
            if (!File.Exists(path))
            {
                // beim ersten Mal Datei anlegen
                SaveThemeDefaultsIfMissing();
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var defaults = JsonSerializer.Deserialize<ThemeDefaults>(json);
                if (defaults == null) return;

                if (defaults.Light != null)
                    ApplyColorSetToTheme(defaults.Light, _lightTheme);

                if (defaults.Dark != null)
                    ApplyColorSetToTheme(defaults.Dark, _darkTheme);
            }
            catch
            {
                // Wenn Datei kaputt ist, lieber auf Code-Defaults bleiben
            }
        }

        private class ThemeDefaults
        {
            public ThemeColorSet Light { get; set; } = new ThemeColorSet();
            public ThemeColorSet Dark { get; set; } = new ThemeColorSet();
        }
        private static string ColorToHex(Color c)
     => $"#{c.R:X2}{c.G:X2}{c.B:X2}";


        private static Color HexToColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            string s = hex.Trim();
            if (s.StartsWith("#"))
                s = s[1..];

            try
            {
                if (s.Length == 6)
                {
                    // RRGGBB
                    byte r = Convert.ToByte(s.Substring(0, 2), 16);
                    byte g = Convert.ToByte(s.Substring(2, 2), 16);
                    byte b = Convert.ToByte(s.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
                else if (s.Length == 8)
                {
                    // optional AARRGGBB
                    byte a = Convert.ToByte(s.Substring(0, 2), 16);
                    byte r = Convert.ToByte(s.Substring(2, 2), 16);
                    byte g = Convert.ToByte(s.Substring(4, 2), 16);
                    byte b = Convert.ToByte(s.Substring(6, 2), 16);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // ignore parse errors
            }

            return fallback;
        }

        private void JumpToSelectionEdge(bool toStart)
        {
            if (!TryGetCurrentSelection(out int start, out int end))
                return;

            int target = toStart ? start : (end - 1);
            JumpToSample(target, restartIfPlaying: true);
        }

        /// <summary>
        /// Einfacher SampleProvider für float[]-Buffer, mit Startposition und Positionsabfrage.
        /// </summary>
        public class SimpleArraySampleProvider : IPositionedSampleProvider
        {
            private readonly float[] _buffer;
            private int _position;

            public SimpleArraySampleProvider(float[] buffer, int sampleRate, int channels, int startSample = 0)
            {
                _buffer = buffer ?? Array.Empty<float>();
                _position = Math.Max(0, Math.Min(startSample, _buffer.Length));
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            }

            public WaveFormat WaveFormat { get; }

            public int PositionSamples => _position;

            public int Read(float[] destBuffer, int offset, int count)
            {
                if (_buffer.Length == 0)
                    return 0;

                int available = _buffer.Length - _position;
                if (available <= 0)
                    return 0;

                int toCopy = Math.Min(available, count);

                for (int n = 0; n < toCopy; n++)
                {
                    destBuffer[offset + n] = _buffer[_position + n];
                }

                _position += toCopy;
                return toCopy;
            }
        }
        private void SelectAll()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            int total = _currentSamples.Length;

            _overviewView.SetSelection(0, total, raiseEvent: true);
            _detailView.SetSelection(0, total, raiseEvent: true);

            ZoomAll();
        }

        public class LoopingArraySampleProvider : IPositionedSampleProvider
        {
            private readonly float[] _buffer;
            private readonly int _loopStart;
            private readonly int _loopEnd; // exklusiv
            private int _position;

            public LoopingArraySampleProvider(float[] buffer, int sampleRate, int channels, int loopStartSample, int loopEndSample)
            {
                _buffer = buffer ?? Array.Empty<float>();

                int total = _buffer.Length;
                loopStartSample = Math.Max(0, Math.Min(loopStartSample, total));
                loopEndSample = Math.Max(0, Math.Min(loopEndSample, total));

                if (loopEndSample <= loopStartSample)
                {
                    loopStartSample = 0;
                    loopEndSample = total;
                }

                _loopStart = loopStartSample;
                _loopEnd = loopEndSample;

                _position = _loopStart;

                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
            }

            public WaveFormat WaveFormat { get; }

            public int PositionSamples => _position;

            public int Read(float[] destBuffer, int offset, int count)
            {
                if (_buffer.Length == 0 || _loopEnd <= _loopStart)
                    return 0;

                int written = 0;

                while (written < count)
                {
                    if (_position >= _loopEnd)
                    {
                        _position = _loopStart; // 🔁 Zurück zum Loop-Anfang
                    }

                    int samplesUntilLoopEnd = _loopEnd - _position;
                    int samplesToWrite = Math.Min(samplesUntilLoopEnd, count - written);

                    for (int n = 0; n < samplesToWrite; n++)
                    {
                        destBuffer[offset + written + n] = _buffer[_position + n];
                    }

                    _position += samplesToWrite;
                    written += samplesToWrite;
                }

                return written;
            }


        }

    }
}
