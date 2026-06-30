using MinimalSoundEditor;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows.Forms;
using static MinimalSoundEditor.MainForm;

namespace MinimalSoundEditor
{
    public partial class MainForm : Form
    {
        // Shared editor state and services live here: themes, loading, playback, undo, clipboard, and audio export.
        // UI composition and ASIO capture are separated into MainForm.Menu.cs and MainForm.Recording.cs.

        // ✅ Singleton-Referenz für statische Helper (themes.json usw.)
        private static MainForm _instance;

        // ===== Hover Zoom =====
        private readonly Dictionary<Control, (Size size, Point loc)> _hoverBackup
            = new Dictionary<Control, (Size, Point)>();

        private ToolStripMenuItem _miThemeLight;
        private ToolStripMenuItem _miThemeDark;

        public const int SelectionFillAlpha = 110; // oder 80, wie du magst

        private string? _pendingAutoLoadVideoPath;

        public enum ThemeMode
        {
            Light,
            Dark
        }

        /// <summary>Complete color definition for one light or dark editor theme.</summary>
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

        // Clipboard (Copy/Paste)
        private float[] _clipboardSamples = Array.Empty<float>();

        // Loop
        // private CheckBox _chkLoop;
        private bool _loopEnabled = false;

        // Auto-Follow (Detail-View folgt Playhead)
        private bool _autoFollowEnabled = true;

        private string _currentFilePath;   // aktuell geladene Datei

        // Auto-Load-Feature
        private string _lastLoadedFilePath;   // Pfad der zuletzt erfolgreich geladenen Datei
        private bool _autoLoadLastFile = true; // intern: ob beim Start automatisch geladen werden soll

        private bool _isDirty;             // wurde editiert?

        private enum AudioExportFormat
        {
            Wav,
            Flac,
            Mp3,
            Aac // AAC in .m4a/.mp4 Container
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

        /// <summary>Creates independent light/dark theme objects before persisted overrides are loaded.</summary>
        private void InitThemes()
        {
            _lightTheme = new ThemeDefinition { Waveform = new WaveformViewTheme() };
            _darkTheme = new ThemeDefinition { Waveform = new WaveformViewTheme() };

            SetDefaultLightTheme(_lightTheme);
            SetDefaultDarkTheme(_darkTheme);

            _currentThemeMode = ThemeMode.Dark; // Start im Dark-Mode
        }
        private static ThemeDefinition CloneTheme(ThemeDefinition src)
        {
            if (src == null)
                return null;

            return new ThemeDefinition
            {
                Mode = src.Mode,
                FormBack = src.FormBack,
                ButtonBack = src.ButtonBack,
                ButtonFore = src.ButtonFore,
                ButtonBorder = src.ButtonBorder,
                Waveform = src.Waveform == null ? null : new WaveformViewTheme
                {
                    Background = src.Waveform.Background,
                    WaveColor = src.Waveform.WaveColor,
                    ZeroLineColor = src.Waveform.ZeroLineColor,
                    SelectionFillColor = src.Waveform.SelectionFillColor,
                    SelectionEdgeColor = src.Waveform.SelectionEdgeColor,
                    PlayheadColor = src.Waveform.PlayheadColor,
                    TextColor = src.Waveform.TextColor
                }
            };
        }

        private static void CopyTheme(ThemeDefinition src, ThemeDefinition dst)
        {
            if (src == null || dst == null)
                return;

            dst.Mode = src.Mode;
            dst.FormBack = src.FormBack;
            dst.ButtonBack = src.ButtonBack;
            dst.ButtonFore = src.ButtonFore;
            dst.ButtonBorder = src.ButtonBorder;

            if (src.Waveform == null)
            {
                dst.Waveform = null;
            }
            else
            {
                if (dst.Waveform == null)
                    dst.Waveform = new WaveformViewTheme();

                dst.Waveform.Background = src.Waveform.Background;
                dst.Waveform.WaveColor = src.Waveform.WaveColor;
                dst.Waveform.ZeroLineColor = src.Waveform.ZeroLineColor;
                dst.Waveform.SelectionFillColor = src.Waveform.SelectionFillColor;
                dst.Waveform.SelectionEdgeColor = src.Waveform.SelectionEdgeColor;
                dst.Waveform.PlayheadColor = src.Waveform.PlayheadColor;
                dst.Waveform.TextColor = src.Waveform.TextColor;
            }
        }

        private void UpdateThemeMenuChecks()
        {
            if (_miThemeLight == null || _miThemeDark == null)
                return;

            _miThemeLight.Checked = _currentThemeMode == ThemeMode.Light;
            _miThemeDark.Checked = _currentThemeMode == ThemeMode.Dark;
        }

        /// <summary>Applies the active palette consistently to forms, controls, waveforms, and menu state.</summary>
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
            {
                _overviewView.ApplyTheme(t.Waveform);

                // Overview: schwächerer, transparenter Selection-Balken
                var baseFill = t.Waveform.SelectionFillColor;
                var baseEdge = t.Waveform.SelectionEdgeColor;

                var ovFill = Color.FromArgb(80, baseFill.R, baseFill.G, baseFill.B);   // halbtransparent
                var ovEdge = Color.FromArgb(160, baseEdge.R, baseEdge.G, baseEdge.B);   // etwas kräftigerer Rand

                _overviewView.SetSelectionColors(ovFill, ovEdge);
            }

            if (_detailView != null)
            {
                _detailView.ApplyTheme(t.Waveform);

                // Detail: volle Bearbeitungs-Selektion (Originalfarben)
                _detailView.SetSelectionColors(
                    t.Waveform.SelectionFillColor,
                    t.Waveform.SelectionEdgeColor);
            }
        }
        private void StyleButtons(Control parent, ThemeDefinition theme)
        {
            if (parent == null)
                return;

            foreach (Control c in parent.Controls)
            {
                if (c is Button btn)
                {
                    // ❗ VideoPreview-Button NICHT vom Theme überschreiben
                    if (btn == _btnVideoPreview)
                        continue;

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

            t.FormBack = Color.FromArgb(0xF5, 0xF5, 0xF5);
            t.ButtonBack = Color.FromArgb(0xF5, 0xF5, 0xF5);
            t.ButtonFore = Color.FromArgb(0x00, 0x00, 0x00);
            t.ButtonBorder = Color.FromArgb(0xA9, 0xA9, 0xA9);

            t.Waveform.Background = Color.FromArgb(0xC8, 0xEC, 0xF7);
            t.Waveform.WaveColor = Color.FromArgb(0x00, 0x80, 0xC0);
            t.Waveform.ZeroLineColor = Color.FromArgb(0xA9, 0xA9, 0xA9);
            t.Waveform.SelectionFillColor = Color.FromArgb(0xFF, 0xD7, 0x00);
            t.Waveform.SelectionEdgeColor = Color.FromArgb(0xFF, 0xD7, 0x00);
            t.Waveform.PlayheadColor = Color.FromArgb(0xFF, 0x00, 0x00);
            t.Waveform.TextColor = Color.FromArgb(0x00, 0x00, 0x00);
        }


        internal static void SetDefaultDarkTheme(ThemeDefinition t)
        {
            if (t.Waveform == null) t.Waveform = new WaveformViewTheme();
            t.Mode = ThemeMode.Dark;

            t.FormBack = Color.FromArgb(0x20, 0x20, 0x20);
            t.ButtonBack = Color.FromArgb(0x37, 0x37, 0x37);
            t.ButtonFore = Color.FromArgb(0xF5, 0xF5, 0xF5);
            t.ButtonBorder = Color.FromArgb(0x69, 0x69, 0x69);

            t.Waveform.Background = Color.FromArgb(0x00, 0x00, 0x00);
            t.Waveform.WaveColor = Color.FromArgb(0x00, 0x80, 0x80);
            t.Waveform.ZeroLineColor = Color.FromArgb(0x80, 0x80, 0x80);
            t.Waveform.SelectionFillColor = Color.FromArgb(0xFF, 0xFF, 0x00);
            t.Waveform.SelectionEdgeColor = Color.FromArgb(0xFF, 0xD7, 0x00);
            t.Waveform.PlayheadColor = Color.FromArgb(0xFF, 0x00, 0x00);
            t.Waveform.TextColor = Color.FromArgb(0xF5, 0xF5, 0xF5);
        }


        private void OverviewView_SelectionChanged(int startSample, int endSample)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (endSample <= startSample)
                return;

            int selectionLength = endSample - startSample;

            // 1 Sekunde in Samples (Fallback 44.1k)
            int marginSamples = _currentSampleRate > 0 ? _currentSampleRate : 44100;

            // DetailView weiß jetzt, wie weit links/rechts gescrollt werden darf
            _detailView.ExtraScrollSamples = marginSamples;

            // Sichtfenster: Auswahl + 1s davor + 1s danach
            int viewStart = startSample - marginSamples;        // darf NEGATIV sein
            int viewCount = selectionLength + marginSamples * 2;

            if (viewCount <= 0)
                viewCount = selectionLength;

            _detailView.VisibleStartSample = viewStart;
            _detailView.VisibleSampleCount = viewCount;

            // WICHTIG:
            // Keine Selektion im DetailView ändern!
            // Der Overview dient NUR als "Fenster" (Zoom/Ausschnitt),
            // die Bearbeitungs-Auswahl unten bleibt unangetastet.

            UpdateStatusBar();
        }

        private void OpenThemeSettings()
        {
            // themes.json -> Light/Dark in _lightTheme/_darkTheme laden
            LoadThemeDefaultsFromFile();

            using (var dlg = new ThemeSettingsForm(_lightTheme, _darkTheme, _currentThemeMode))
            {
                // Live-Preview: jede Änderung im Dialog wird sofort angewendet
                dlg.ThemeChanged += () => ApplyTheme();

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _currentThemeMode = dlg.SelectedMode;

                    ApplyTheme();
                    SaveThemeSettings();

                    // WICHTIG:
                    // themes.json wird jetzt im Dialog (SaveJsonModel) geschrieben.
                    // Kein SaveThemeDefaults(force: true) mehr hier!
                }
                else
                {
                    // Cancel: wir bleiben bei dem Theme-Zustand, den wir zuletzt gesehen haben
                    ApplyTheme();
                }

                UpdateThemeMenuChecks();
            }
        }




        /// <summary>Writes factory theme defaults once, or replaces them when explicitly requested.</summary>
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
            // Bearbeitungs-Selektion im DetailView => nur als Highlight im Overview anzeigen
            if (_overviewView == null)
                return;

            if (endSample > startSample)
            {
                _overviewView.SetHighlightRange(startSample, endSample);
            }
            else
            {
                _overviewView.SetHighlightRange(null, null);
            }

            UpdateStatusBar();
        }


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

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Eine laufende ASIO-Aufnahme zuerst sauber beenden und als Clip übernehmen.
            if (_isRecording)
                StopAsioRecording(loadIntoEditor: true, showErrors: false);

            // 1) Vor dem echten Schließen prüfen, ob etwas gespeichert werden soll.
            //    Wenn der Benutzer "Abbrechen" wählt oder das Speichern abbricht,
            //    brechen wir das Schließen ab.
            if (!CheckUnsavedChangesBeforeClose())
            {
                e.Cancel = true;
                _isClosing = false; // wichtig: damit Playback etc. normal weiterlaufen
                return;
            }

            // 2) Ab hier wirklich schließen -> keine weiteren Aktionen mehr zulassen
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


        /// <summary>Decodes a file into the editor's mono sample array and resets document/playback state.</summary>
        private void LoadAudioFile(string filePath)
        {
            using var reader = new NAudio.Wave.AudioFileReader(filePath);

            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;
            _currentChannels = reader.WaveFormat.Channels;

            _currentSampleRate = sampleRate;
            var monoSamples = new List<float>();

            // WaveViews über Samplerate informieren
            _overviewView.SampleRate = _currentSampleRate;
            _detailView.SampleRate = _currentSampleRate;

            _currentSamples = monoSamples.ToArray();

            _overviewView.Samples = _currentSamples;
            _overviewView.VisibleStartSample = 0;
            _overviewView.VisibleSampleCount = 0;
            _overviewView.SetHighlightRange(null, null);

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
            _lastLoadedFilePath = filePath; // NEU: für Auto-Load merken
            _isDirty = false;
            UpdateWindowTitle();
            AddToRecentFiles(filePath);
            // Settings inkl. LastFile direkt persistieren
            SaveThemeSettings();

            try
            {
                var fi = new FileInfo(filePath);

                double seconds = 0;
                if (_currentSamples != null && sampleRate > 0)
                    seconds = (double)_currentSamples.Length / sampleRate;

                string niceTime =
                    seconds >= 60
                    ? $"{(int)(seconds / 60)}:{(seconds % 60):00}"
                    : $"{seconds:0.00}s";

                _lblInfo.Text = fi.Name;
                //                    $"{fi.Name}   •   {niceTime}   •   {sampleRate} Hz   •   {(channels > 1 ? $"{channels} Channels" : "Mono")}";
            }
            catch
            {
                _lblInfo.Text = Path.GetFileName(filePath);
            }
            UpdateStatusBar();
        }
        /// <summary>Entry point used by Program for files supplied on the command line or by file association.</summary>
        public void LoadAudioFileFromExternal(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!File.Exists(path))
                return;

            LoadAudioFile(path);
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
        /// <summary>
        /// Prüft, ob es ungespeicherte Änderungen gibt, und fragt optional nach,
        /// ob vor dem Beenden gespeichert werden soll.
        /// Rückgabe:
        /// - true  = Schließen ist ok
        /// - false = Schließen abbrechen
        /// </summary>
        /// <summary>Offers the unified export before allowing an edited document to be discarded on exit.</summary>
        private bool CheckUnsavedChangesBeforeClose()
        {
            // Wenn nichts geladen oder nichts geändert wurde -> kein nerviger Dialog
            if (_currentSamples == null || _currentSamples.Length == 0 || !_isDirty)
                return true;

            string fileName = string.IsNullOrEmpty(_currentFilePath)
                ? "unbenannter Clip"
                : Path.GetFileName(_currentFilePath);

            var result = MessageBox.Show(
                this,
                $"Möchtest du die Änderungen an \"{fileName}\" exportieren, bevor du die App schließt?",
                "Änderungen exportieren?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Cancel)
            {
                // Benutzer möchte doch nicht schließen
                return false;
            }

            if (result == DialogResult.No)
            {
                // Änderungen verwerfen -> schließen
                return true;
            }

            // result == Yes -> den ganzen Clip exportieren.
            return ExportWholeTrackForUnsavedChanges();
        }


        private void UpdatePlaybackTimerInterval()
        {
            // Einfach konstant lassen – Windows-Timer kann eh nicht besser als ~15 ms
            if (_playbackTimer != null)
                _playbackTimer.Interval = 16;   // ~60 FPS
        }


        private float[] CloneSamples(float[] src)
        {
            if (src == null) return Array.Empty<float>();
            var copy = new float[src.Length];
            Array.Copy(src, copy, src.Length);
            return copy;
        }

        /// <summary>Restores the last complete sample snapshot and refreshes all dependent UI state.</summary>
        private void Undo()
        {
            if (_undoStack.Count == 0)
                return;

            if (_currentSamples == null)
                return;

            // Zoom-/Fenster-Einstellungen merken
            int prevDetailStart = _detailView.VisibleStartSample;
            int prevDetailCount = _detailView.VisibleSampleCount;
            int prevOverviewStart = _overviewView.VisibleStartSample;
            int prevOverviewCount = _overviewView.VisibleSampleCount;

            // Bearbeitungs-Selektion im DetailView merken
            bool hadSelection = _detailView.TryGetSelection(out int selStart, out int selEnd) && selEnd > selStart;

            // Playhead-Position merken
            int prevPlayhead = _playbackSamplePosition;

            // Samples aus Undo-Stack wiederherstellen
            _currentSamples = _undoStack.Pop();

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            int total = _currentSamples.Length;

            // Zoom im DetailView wiederherstellen (so gut es geht)
            if (prevDetailCount <= 0 || prevDetailCount > total)
                prevDetailCount = total;

            _detailView.VisibleSampleCount = prevDetailCount;
            _detailView.VisibleStartSample = prevDetailStart;

            // Fenster im Overview wiederherstellen (auf Dateibereich geklemmt)
            if (prevOverviewCount <= 0 || prevOverviewCount > total)
                prevOverviewCount = total;

            if (prevOverviewStart < 0)
                prevOverviewStart = 0;
            if (prevOverviewStart > total - 1)
                prevOverviewStart = Math.Max(0, total - prevOverviewCount);

            _overviewView.VisibleSampleCount = prevOverviewCount;
            _overviewView.VisibleStartSample = prevOverviewStart;

            // Playhead-Position innerhalb der neuen Länge einklemmen
            int newPlay = prevPlayhead;
            if (newPlay < 0) newPlay = 0;
            if (newPlay >= total && total > 0) newPlay = total - 1;

            _playbackSamplePosition = newPlay;
            _overviewView.PlaybackSample = newPlay;
            _detailView.PlaybackSample = newPlay;

            UpdateInfo(_currentSampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _chkLoop.Checked = false;

            // Bearbeitungs-Selektion im DetailView wiederherstellen
            if (hadSelection && total > 0)
            {
                selStart = Math.Max(0, Math.Min(selStart, total - 1));
                selEnd = Math.Max(selStart + 1, Math.Min(selEnd, total));
                _detailView.SetSelection(selStart, selEnd, raiseEvent: true);
            }
            else
            {
                _detailView.ClearSelection();
            }

            // Fenster-Auswahl im Overview entsprechend aktualisieren (nur Anzeige, kein Event)
            int vs = _detailView.VisibleStartSample;
            int ve = vs + _detailView.VisibleSampleCount;
            if (ve > total) ve = total;
            if (ve > vs)
            {
                _overviewView.SetSelection(vs, ve, raiseEvent: false);
            }
        }


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
        /// <summary>Samples the audio device position on the UI timer and updates playhead, loop, and video preview.</summary>
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
            SyncVideoPreview(force: false);
            // Auto-Follow: DetailView so scrollen, dass der Playhead im Fenster bleibt
            if (_autoFollowEnabled)
            {
                int viewStart = _detailView.VisibleStartSample;
                int viewCount = _detailView.VisibleSampleCount;

                // 0 oder weniger heißt: „ganzer Track“ – dann nicht scrollen
                if (viewCount <= 0)
                {
                    viewCount = _currentSamples.Length;
                }

                if (viewCount > 0 && _currentSamples.Length > 0)
                {
                    int viewEnd = viewStart + viewCount;

                    // ein bisschen Puffer, damit der Playhead nicht direkt am Rand klebt
                    int margin = (int)(viewCount * 0.2); // 20% des Fensters

                    // zu weit links? -> Fenster nach links schieben
                    if (pos < viewStart + margin)
                    {
                        int newStart = pos - margin;
                        _detailView.VisibleStartSample = newStart;
                    }
                    // zu weit rechts? -> Fenster nach rechts schieben
                    else if (pos > viewEnd - margin)
                    {
                        int newStart = pos - (viewCount - margin);
                        _detailView.VisibleStartSample = newStart;
                    }
                }
            }

            UpdateStatusBar();

        }

        // Klick in Overview/Detail -> Playhead setzen
        private void Waveform_PlaybackPositionChangedByClick(int sampleIndex)
        {
            // nutzt jetzt denselben Weg wie Tastatur-Sprünge
            JumpToSample(sampleIndex, restartIfPlaying: true);
            SyncVideoPreview(force: true);
        }
        /// <summary>Sends locator changes to the preview while avoiding unnecessary FFmpeg frame renders.</summary>
        private void SyncVideoPreview(bool force)
        {
            if (_videoPreview == null || _videoPreview.IsDisposed) return;
            if (string.IsNullOrEmpty(_currentVideoPath)) return;

            var now = DateTime.UtcNow;
            if (!force)
            {
                double ms = (now - _lastVideoSyncUtc).TotalMilliseconds;
                if (ms < VideoSyncMinIntervalMs) return;
            }

            _lastVideoSyncUtc = now;

            double seconds = GetLocatorSeconds();

            bool isPlaying = _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;
            _videoPreview.SetTime(seconds, isPlaybackTick: isPlaying);


        }

        /// <summary>Normalizes the active selection to a user-selected target and records one undo snapshot.</summary>
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

            UpdateStatusBar();
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

        /// <summary>Routes one sample range to the native WAV writer or the FFmpeg-backed compressed exporters.</summary>
        private bool ExportSamplesToFile(float[] samples, int sampleRate, string filePath, AudioExportFormat format)
        {
            if (samples == null || samples.Length == 0)
                return false;

            try
            {
                switch (format)
                {
                    case AudioExportFormat.Wav:
                        // WAV: 32-bit float, mono
                        using (var writer = new WaveFileWriter(
                                   filePath,
                                   WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
                        {
                            writer.WriteSamples(samples, 0, samples.Length);
                        }
                        return true;

                    case AudioExportFormat.Flac:
                        return ExportFlacWithFfmpeg(samples, sampleRate, filePath);

                    case AudioExportFormat.Mp3:
                    case AudioExportFormat.Aac:
                        MediaFoundationApi.Startup();

                        var monoProvider = new SimpleArraySampleProvider(samples, sampleRate, 1, 0);
                        var stereoProvider = new MonoToStereoSampleProvider(monoProvider);
                        var wave16 = new SampleToWaveProvider16(stereoProvider);

                        int targetRate = 44100;
                        var targetFormat = new WaveFormat(targetRate, 16, 2);
                        IWaveProvider source = wave16;
                        int bitrate = 192000;

                        if (wave16.WaveFormat.SampleRate != targetRate ||
                            wave16.WaveFormat.Channels != 2 ||
                            wave16.WaveFormat.BitsPerSample != 16)
                        {
                            using var resampler = new MediaFoundationResampler(source, targetFormat);
                            resampler.ResamplerQuality = 60;

                            if (format == AudioExportFormat.Mp3)
                                MediaFoundationEncoder.EncodeToMp3(resampler, filePath, bitrate);
                            else
                                MediaFoundationEncoder.EncodeToAac(resampler, filePath, bitrate);
                        }
                        else
                        {
                            if (format == AudioExportFormat.Mp3)
                                MediaFoundationEncoder.EncodeToMp3(source, filePath, bitrate);
                            else
                                MediaFoundationEncoder.EncodeToAac(source, filePath, bitrate);
                        }

                        return true;

                    default:
                        throw new NotSupportedException("Unbekanntes Exportformat.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Fehler beim Exportieren:\n" + ex.Message,
                    "Export",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>Uses a temporary WAV so FFmpeg receives a simple, lossless input for FLAC encoding.</summary>
        private bool ExportFlacWithFfmpeg(float[] samples, int sampleRate, string filePath)
        {
            string tempWav = Path.Combine(
                Path.GetTempPath(),
                "mse_flac_" + Guid.NewGuid().ToString("N") + ".wav");

            try
            {
                using (var writer = new WaveFileWriter(
                           tempWav,
                           WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
                {
                    writer.WriteSamples(samples, 0, samples.Length);
                }

                string ffmpeg = GetFfmpegPath();
                var psi = new ProcessStartInfo
                {
                    FileName = ffmpeg,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };

                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add("-hide_banner");
                psi.ArgumentList.Add("-loglevel");
                psi.ArgumentList.Add("error");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add(tempWav);
                psi.ArgumentList.Add("-c:a");
                psi.ArgumentList.Add("flac");
                psi.ArgumentList.Add("-compression_level");
                psi.ArgumentList.Add("8");
                psi.ArgumentList.Add(filePath);

                using Process? process = Process.Start(psi);
                if (process == null)
                    throw new InvalidOperationException("ffmpeg konnte nicht gestartet werden.");

                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || !File.Exists(filePath))
                {
                    string details = string.IsNullOrWhiteSpace(stderr)
                        ? $"ffmpeg ExitCode: {process.ExitCode}"
                        : stderr.Trim();
                    throw new InvalidOperationException(
                        "FLAC-Export mit ffmpeg fehlgeschlagen.\n" + details);
                }

                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempWav))
                        File.Delete(tempWav);
                }
                catch
                {
                    // Temporäre Datei wird beim nächsten Windows-Cleanup entfernt.
                }
            }
        }

        /// <summary>Uses the selected filename extension and filter to determine the requested export codec.</summary>
        private bool TryChooseExportFileAndFormat(
            bool selectionOnly,
            out string filePath,
            out AudioExportFormat format)
        {
            filePath = string.Empty;
            format = AudioExportFormat.Wav;

            string sourceBaseName = "audio";
            string sourceExtension = ".wav";

            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                sourceBaseName = Path.GetFileNameWithoutExtension(_currentFilePath);
                sourceExtension = Path.GetExtension(_currentFilePath).ToLowerInvariant();
            }
            else if (!string.IsNullOrEmpty(_originalVideoPath))
            {
                sourceBaseName = Path.GetFileNameWithoutExtension(_originalVideoPath);
            }

            int filterIndex = sourceExtension switch
            {
                ".flac" => 2,
                ".mp3" => 3,
                ".m4a" or ".mp4" or ".aac" => 4,
                _ => 1
            };

            string suffix = selectionOnly ? "_selection" : "_edited";
            string defaultExtension = filterIndex switch
            {
                2 => ".flac",
                3 => ".mp3",
                4 => ".m4a",
                _ => ".wav"
            };

            using var sfd = new SaveFileDialog
            {
                Title = selectionOnly ? "Auswahl exportieren..." : "Alles exportieren...",
                FileName = sourceBaseName + suffix + defaultExtension,
                Filter =
                    "WAV (*.wav)|*.wav|" +
                    "FLAC (*.flac)|*.flac|" +
                    "MP3 (*.mp3)|*.mp3|" +
                    "AAC/M4A (*.m4a)|*.m4a",
                FilterIndex = filterIndex,
                AddExtension = true,
                OverwritePrompt = true
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return false;

            format = sfd.FilterIndex switch
            {
                2 => AudioExportFormat.Flac,
                3 => AudioExportFormat.Mp3,
                4 => AudioExportFormat.Aac,
                _ => AudioExportFormat.Wav
            };

            string wantedExtension = format switch
            {
                AudioExportFormat.Flac => ".flac",
                AudioExportFormat.Mp3 => ".mp3",
                AudioExportFormat.Aac => ".m4a",
                _ => ".wav"
            };

            filePath = Path.ChangeExtension(sfd.FileName, wantedExtension) ?? sfd.FileName;
            return true;
        }

        /// <summary>Stores mutable settings under the user profile because Program Files is not writable at runtime.</summary>
        private string GetSettingsFilePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinimalSoundEditor");

            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
        /// <summary>Persists current themes, mode, recent files, and last-opened file as user settings.</summary>
        private void SaveThemeSettings()
        {
            var data = new Dictionary<string, string>
            {
                ["CurrentMode"] = _currentThemeMode.ToString()
            };

            if (_recentFiles != null && _recentFiles.Count > 0)
            {
                data["RecentFiles"] = string.Join("|", _recentFiles);
            }

            // NEU: zuletzt geladene Datei merken (falls vorhanden)
            if (!string.IsNullOrEmpty(_lastLoadedFilePath))
                data["LastFilePath"] = _lastLoadedFilePath;

            // NEU: Auto-Load-Flag speichern
            data["AutoLoadLastFile"] = _autoLoadLastFile ? "true" : "false";

            var json = JsonSerializer.Serialize(
                data,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(GetSettingsFilePath(), json);
        }


        /// <summary>Loads user settings and migrates the former executable-folder JSON on first run.</summary>
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
                // NEU: Recent-Files-Liste laden
                if (data.TryGetValue("RecentFiles", out var recentStr) &&
                    !string.IsNullOrWhiteSpace(recentStr))
                {
                    _recentFiles.Clear();
                    foreach (var raw in recentStr.Split('|'))
                    {
                        var p = raw.Trim();
                        if (string.IsNullOrEmpty(p))
                            continue;

                        // doppelte Einträge vermeiden
                        if (!_recentFiles.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase)))
                            _recentFiles.Add(p);
                    }
                }
                // NEU: zuletzt geladener Clip
                if (data.TryGetValue("LastFilePath", out var lastPath) &&
                    !string.IsNullOrWhiteSpace(lastPath))
                {
                    _lastLoadedFilePath = lastPath;
                }

                // NEU: Auto-Load-Flag (falls noch nicht gespeichert, bleibt Default = true)
                if (data.TryGetValue("AutoLoadLastFile", out var autoStr) &&
                    bool.TryParse(autoStr, out var auto))
                {
                    _autoLoadLastFile = auto;
                }
            }
            catch
            {
                // egal – dann bleiben wir bei Defaults
            }
        }
        private void AddToRecentFiles(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            // Entfernen, falls schon in der Liste (case-insensitive)
            _recentFiles.RemoveAll(p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));

            // vorne einfügen
            _recentFiles.Insert(0, filePath);

            // auf MaxRecentFiles begrenzen
            if (_recentFiles.Count > MaxRecentFiles)
                _recentFiles.RemoveRange(MaxRecentFiles, _recentFiles.Count - MaxRecentFiles);

            RebuildRecentFilesMenu();
            SaveThemeSettings();
        }

        private void RebuildRecentFilesMenu()
        {
            if (_miRecentFiles == null)
                return;

            _miRecentFiles.DropDownItems.Clear();

            if (_recentFiles.Count == 0)
            {
                var empty = new ToolStripMenuItem("(keine)") { Enabled = false };
                _miRecentFiles.DropDownItems.Add(empty);
                return;
            }

            foreach (var path in _recentFiles)
            {
                string fileName = Path.GetFileName(path);
                var item = new ToolStripMenuItem(fileName)
                {
                    Tag = path,
                    ToolTipText = path
                };
                item.Click += RecentFileMenuItem_Click;
                _miRecentFiles.DropDownItems.Add(item);
            }
        }
        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using var dlg = new AboutForm();
            dlg.ShowDialog(this);
        }

        private void RecentFileMenuItem_Click(object sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem mi)
                return;

            if (mi.Tag is not string path || string.IsNullOrEmpty(path))
                return;

            if (!File.Exists(path))
            {
                MessageBox.Show(this,
                    "Die Datei existiert nicht mehr:\n" + path,
                    "Datei nicht gefunden",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                _recentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
                RebuildRecentFilesMenu();
                SaveThemeSettings();
                return;
            }

            try
            {
                LoadAudioFile(path);
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Fehler beim Laden der Datei:\n" + ex.Message,
                    "Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        bool TryGetCurrentSelection(out int startSample, out int endSample)
        {
            // Bearbeitungs-Selektion kommt NUR aus dem DetailView
            if (_detailView != null &&
                _detailView.TryGetSelection(out startSample, out endSample) &&
                endSample > startSample)
            {
                return true;
            }

            startSample = 0;
            endSample = 0;
            return false;
        }


        /// <summary>Moves both waveform playheads and optionally recreates playback from the new location.</summary>
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

            UpdateStatusBar();

            SyncVideoPreview(force: true);
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
            var settingsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MinimalSoundEditor");

            Directory.CreateDirectory(settingsDir);
            string settingsPath = Path.Combine(settingsDir, "themes.json");

            // Einmalige Migration aus alten portablen/per-user Installationen.
            string legacyPath = Path.Combine(AppContext.BaseDirectory, "themes.json");
            if (!File.Exists(settingsPath) && File.Exists(legacyPath))
            {
                try
                {
                    File.Copy(legacyPath, settingsPath, overwrite: false);
                }
                catch (IOException)
                {
                    // Falls parallel bereits eine Datei angelegt wurde, verwenden wir diese.
                }
                catch (UnauthorizedAccessException)
                {
                    // Die App bleibt auch ohne Migration benutzbar.
                }
            }

            return settingsPath;
        }

        // war vorher private – jetzt z.B. internal oder public
        public static void SaveThemeDefaultsIfMissing()
        {
            SaveThemeDefaults(force: false);
        }

        /// <summary>
        /// Versucht beim Start, den zuletzt geladenen Clip automatisch zu öffnen.
        /// Nervt nicht: macht nur was, wenn AutoLoad aktiviert ist,
        /// ein Pfad vorhanden ist und die Datei noch existiert.
        /// </summary>
        private void TryAutoLoadLastFileOnStartup()
        {
            if (!_autoLoadLastFile)
                return;

            if (string.IsNullOrEmpty(_lastLoadedFilePath))
                return;

            if (!File.Exists(_lastLoadedFilePath))
                return;

            try
            {
                string ext = Path.GetExtension(_lastLoadedFilePath);

                if (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    // MP4 erst nach Shown laden (sonst kein Handle/Owner, Preview öffnet nicht sauber)
                    _pendingAutoLoadVideoPath = _lastLoadedFilePath;
                    return;
                }

                // normale Audiodatei direkt laden
                LoadAudioFile(_lastLoadedFilePath);

            }
            catch
            {
                // Keine Fehlermeldung beim Start – still fail,
                // damit der User nicht genervt wird, falls das File kaputt ist.
            }
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

        /// <summary>Copies the normalized detail selection into an internal floating-point clipboard.</summary>
        private void CopySelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Copy",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 0)
                return;

            _clipboardSamples = new float[length];
            Array.Copy(_currentSamples, start, _clipboardSamples, 0, length);
        }
        /// <summary>Inserts clipboard samples at the selection or locator and shifts following audio.</summary>
        private void PasteInsert()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (_clipboardSamples == null || _clipboardSamples.Length == 0)
            {
                MessageBox.Show(this, "Clipboard ist leer (keine Audiodaten kopiert).", "Paste (insert)",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int total = _currentSamples.Length;
            int clipLen = _clipboardSamples.Length;

            // Einfügeposition = aktueller Playhead
            int insertPos = _playbackSamplePosition;
            if (insertPos < 0) insertPos = 0;
            if (insertPos > total) insertPos = total;

            bool insertedAtStart = (insertPos == 0);

            // ✅ If we insert audio BEFORE the original start, the video should effectively start later on the audio timeline.
            // We store this as a (possibly negative) offset: videoTime = audioTime + _videoTimeOffsetSeconds.
            if (!string.IsNullOrEmpty(_currentVideoPath) && _currentSampleRate > 0 && insertPos == 0)
            {
                _videoTimeOffsetSeconds -= clipLen / (double)_currentSampleRate;
            }

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            var newSamples = new float[total + clipLen];

            // 0..insertPos-1
            if (insertPos > 0)
                Array.Copy(_currentSamples, 0, newSamples, 0, insertPos);

            // Clipboard
            Array.Copy(_clipboardSamples, 0, newSamples, insertPos, clipLen);

            // Rest hinten
            if (insertPos < total)
                Array.Copy(_currentSamples, insertPos, newSamples, insertPos + clipLen, total - insertPos);

            _currentSamples = newSamples;
            int newTotal = newSamples.Length;

            // Zoom im DetailView so gut es geht beibehalten
            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            if (oldVisibleCount <= 0 || oldVisibleCount > newTotal)
                oldVisibleCount = newTotal;

            if (oldVisibleStart < 0)
                oldVisibleStart = 0;
            if (oldVisibleStart > newTotal - oldVisibleCount)
                oldVisibleStart = Math.Max(0, newTotal - oldVisibleCount);

            _detailView.VisibleStartSample = oldVisibleStart;
            _detailView.VisibleSampleCount = oldVisibleCount;

            // neu eingefügten Bereich markieren
            int selStart = insertPos;
            int selEnd = insertPos + clipLen;

            _detailView.SetSelection(selStart, selEnd, raiseEvent: true);
            _overviewView.SetSelection(selStart, selEnd, raiseEvent: false);

            // Playhead ans Ende des eingefügten Blocks setzen
            _playbackSamplePosition = Math.Min(selEnd, newTotal - 1);
            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            UpdateInfo(_currentSampleRate, newTotal);
            UpdatePlaybackTimerInterval();

            // ✅ Wenn am Anfang eingefügt wurde: Video-Offset nach links schieben
            // (damit der “alte Inhalt” weiterhin zur gleichen Videostelle passt)
            if (!string.IsNullOrEmpty(_currentVideoPath) &&
                insertedAtStart &&
                _currentSampleRate > 0 &&
                clipLen > 0)
            {
                _videoTimeOffsetSeconds -= clipLen / (double)_currentSampleRate;
            }

            // ✅ Preview sofort neu setzen (Playhead hat sich auch bewegt)
            SyncVideoPreview(force: true);

            _isDirty = true;
            UpdateWindowTitle();
        }

        /// <summary>Replaces samples from the insertion point without changing track duration unless it reaches the end.</summary>
        private void PasteOverwrite()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (_clipboardSamples == null || _clipboardSamples.Length == 0)
            {
                MessageBox.Show(this,
                    "Clipboard ist leer (keine Audiodaten kopiert).",
                    "Paste (overwrite)",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            int total = _currentSamples.Length;
            int clipLen = _clipboardSamples.Length;

            // Start-Position = aktueller Lokator (Playhead)
            int start = _playbackSamplePosition;
            if (start < 0) start = 0;
            if (start > total) start = total; // am Ende einfügen/überschreiben

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            // neue Gesamtlänge: genug, damit das Clipboard vollständig hineinpasst
            int newTotal = Math.Max(total, start + clipLen);
            var newSamples = new float[newTotal];

            // vorhandenes Material übernehmen
            // (wird gleich teilweise überschrieben, wo Clipboard liegt)
            if (total > 0)
                Array.Copy(_currentSamples, 0, newSamples, 0, total);

            // Clipboard auf [start, start+clipLen) kopieren
            Array.Copy(_clipboardSamples, 0, newSamples, start, clipLen);

            _currentSamples = newSamples;

            // Zoom / Fenster im DetailView möglichst beibehalten
            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            if (oldVisibleCount <= 0 || oldVisibleCount > newTotal)
                oldVisibleCount = newTotal;

            if (oldVisibleStart < 0)
                oldVisibleStart = 0;
            if (oldVisibleStart > newTotal - oldVisibleCount)
                oldVisibleStart = Math.Max(0, newTotal - oldVisibleCount);

            _detailView.VisibleStartSample = oldVisibleStart;
            _detailView.VisibleSampleCount = oldVisibleCount;

            // neu eingefügten/überschriebenen Bereich selektieren
            int selStart = start;
            int selEnd = start + clipLen;
            if (selEnd > newTotal) selEnd = newTotal;

            _detailView.SetSelection(selStart, selEnd, raiseEvent: true);
            _overviewView.SetSelection(selStart, selEnd, raiseEvent: false);

            // Lokator ans Ende des eingefügten Blocks setzen
            _playbackSamplePosition = Math.Min(selEnd, newTotal - 1);
            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            UpdateInfo(_currentSampleRate, newTotal);
            UpdatePlaybackTimerInterval();

            _chkLoop.Checked = false;
            _isDirty = true;
            UpdateWindowTitle();
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


        /// <summary>Loads optional factory defaults before user-specific settings are layered on top.</summary>
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
        /// <summary>One-shot sample provider used for ordinary playback from an arbitrary start position.</summary>
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

        /// <summary>Sample provider that wraps inside a fixed selection while reporting an absolute track position.</summary>
        public class LoopingArraySampleProvider : IPositionedSampleProvider
        {
            private readonly float[] _buffer;
            private readonly int _loopStart;
            private readonly int _loopEnd; // exklusiv
            private int _position;

            public LoopingArraySampleProvider(
                float[] buffer,
                int sampleRate,
                int channels,
                int loopStartSample,
                int loopEndSample,
                int? startSample = null)
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

                // Startposition: entweder explizit übergeben oder Loop-Anfang
                int initialPos;
                if (startSample.HasValue)
                {
                    // innerhalb des Loops einklemmen
                    initialPos = Math.Max(_loopStart, Math.Min(startSample.Value, _loopEnd - 1));
                }
                else
                {
                    initialPos = _loopStart;
                }

                _position = initialPos;

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
