using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            // 1) Designer-Init
            InitializeComponent();

            // 2) Logik-Init
            _instance = this;

            // === OVERVIEW (oben, klein) ===
            _overviewView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            _overviewView.SelectionChanged += OverviewView_SelectionChanged;
            _overviewView.MouseDoubleClick += OverviewView_MouseDoubleClick;

            // === DETAIL (unten) ===
            _detailView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            _detailView.SelectionChanged += DetailView_SelectionChanged;

            // Loop
            _chkLoop.CheckedChanged += (s, e) =>
            {
                _loopEnabled = _chkLoop.Checked;

                if (_chkLoop.Checked)
                    _chkLoop.BackgroundImage = Resource1.icon_looping;
                else
                    _chkLoop.BackgroundImage = Resource1.icon_loop;
            };

            // Icons stylen …
            //StyleToolbarButton(_btnOpen, Resource1.icon_openFile, "");
            //StyleToolbarButton(_btnDeleteSelection, Resource1.icon_del, "");
            //StyleToolbarButton(_btnUndo, Resource1.icon_undo, "");
            //StyleToolbarButton(_btnPlay, Resource1.icon_play, "");
            //StyleToolbarButton(_btnStop, Resource1.icon_stop, "");
            // Loop-Icon-Block …
            //StyleToolbarButton(_btnTheme, Resource1.icon_themes, "");

            // LABEL (hier ist noch ein kleiner Bug, s.u.)

            InitThemes();
            LoadThemeDefaultsFromFile();
            LoadThemeSettings();
            ApplyTheme();
            UpdateThemeMenuChecks();

            // === Playback-Timer (UI-Thread) ===
            _playbackTimer = new System.Windows.Forms.Timer
            {
                Interval = 16 // ~60 FPS
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            // Form-Events
            this.FormClosing += MainForm_FormClosing;
            this.Resize += MainForm_Resize;

            MakeToolbarButtonLookFlat(_btnOpen);
            MakeToolbarButtonLookFlat(_btnDeleteSelection);
            MakeToolbarButtonLookFlat(_btnUndo);
            MakeToolbarButtonLookFlat(_btnPlay);
            MakeToolbarButtonLookFlat(_btnStop);
            MakeToolbarButtonLookFlat(_btnTheme);
            MakeLoopButtonLookFlat(_chkLoop);

            // Hover Zoom für Toolbar Buttons
            EnableHoverZoom(_btnOpen);
            EnableHoverZoom(_btnDeleteSelection);
            EnableHoverZoom(_btnUndo);
            EnableHoverZoom(_btnPlay);
            EnableHoverZoom(_btnStop);
            EnableHoverZoom(_btnTheme);
            EnableHoverZoom(_chkLoop);
        }


        private void BtnOpen_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Audio-Dateien|*.wav;*.mp3;*.flac;*.aiff;*.wma;*.m4a|Alle Dateien|*.*",
                Title = "Audiodatei öffnen"
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                LoadAudioFile(ofd.FileName);
                _lblInfo.Text = ofd.FileName;
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

        private void BtnDeleteSelection_Click(object sender, EventArgs e)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            // Nur im Detail-Track löschen
            _detailView.DeleteSelection();

            // Loop-Bereich ungültig, weil sich die Samples verschoben haben
            _chkLoop.Checked = false;

            // Samples aus Detail-View übernehmen
            _currentSamples = _detailView.Samples ?? Array.Empty<float>();
            _overviewView.Samples = _currentSamples;

            if (_currentSamples.Length == 0)
            {
                _playbackSamplePosition = 0;
                _overviewView.PlaybackSample = 0;
                _detailView.PlaybackSample = 0;
            }
            else if (_playbackSamplePosition >= _currentSamples.Length)
            {
                _playbackSamplePosition = _currentSamples.Length - 1;
                _overviewView.PlaybackSample = _playbackSamplePosition;
                _detailView.PlaybackSample = _playbackSamplePosition;
            }

            UpdateInfo(_currentSampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _isDirty = true;
            UpdateWindowTitle();
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
            {
                MessageBox.Show(this, "Keine Audiodatei geladen.", "Hinweis",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                try { _waveOut.Stop(); } catch { }
                _waveOut.Dispose();
                _waveOut = null;
            }

            //_waveOut = new WaveOutEvent
            //{
            //    DesiredLatency = 50,   // ca. 50 ms Gesamtlatenz
            //    NumberOfBuffers = 2    // 2 kleine Buffer
            //};

            // etwas konservativere Standard-Einstellungen verwenden
            _waveOut = new WaveOutEvent();

            // Prüfen, ob wir loop-fähig sind
            bool hasLoopSelection = false;
            int loopStart = 0;
            int loopEnd = 0;

            if (_loopEnabled)
            {
                // 1) bevorzugt: Selektion im Detail-Track
                if (_detailView.TryGetSelection(out var ds, out var de) && de > ds)
                {
                    hasLoopSelection = true;
                    loopStart = ds;
                    loopEnd = de;
                }
                // 2) falls dort nichts: Selektion im Overview-Track
                else if (_overviewView.TryGetSelection(out var os, out var oe) && oe > os)
                {
                    hasLoopSelection = true;
                    loopStart = os;
                    loopEnd = oe;
                }
            }

            if (hasLoopSelection)
            {
                // Loop von aktueller Selektion
                _currentProvider = new LoopingArraySampleProvider(
                    _currentSamples,
                    _currentSampleRate,
                    1,
                    loopStart,
                    loopEnd);

                // Playhead am Loop-Anfang
                _playbackSamplePosition = loopStart;
                _overviewView.PlaybackSample = _playbackSamplePosition;
                _detailView.PlaybackSample = _playbackSamplePosition;
            }
            else
            {
                // normales Playback ab aktuellem Playhead
                _currentProvider = new SimpleArraySampleProvider(
                    _currentSamples,
                    _currentSampleRate,
                    1,
                    _playbackSamplePosition);
            }


            _waveOut.Init(_currentProvider);
            _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
            _waveOut.Play();

            _playbackTimer?.Start();
            UpdatePlayButtonVisualState();
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_waveOut == null)
                return;

            try { _waveOut.Stop(); _playbackTimer?.Stop(); } catch { }
            _playbackTimer?.Stop();
            UpdatePlayButtonVisualState();
        }

        private void BtnTheme_Click(object sender, EventArgs e)
        {
            OpenThemeSettings();
        }

        private void BtnUndo_Click(object sender, EventArgs e)
        {
            Undo();
        }
        // Tastatur-Shortcuts
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            //// CTRL+O -> Öffnen
            //if (keyData == (Keys.Control | Keys.O))
            //{
            //    BtnOpen_Click(null, EventArgs.Empty);
            //    return true;
            //}

            // CTRL+T -> Theme-Settings
            if (keyData == (Keys.Control | Keys.T))
            {
                OpenThemeSettings();
                return true;
            }

            // SPACE -> Play/Stop
            if (keyData == Keys.Space)
            {
                TogglePlayStop();
                return true;
            }

            // CTRL+Z -> Undo
            if (keyData == (Keys.Control | Keys.Z))
            {
                Undo();
                return true;
            }

            // L -> Loop Toggle
            if (keyData == Keys.L)
            {
                _chkLoop.Checked = !_chkLoop.Checked;
                return true;
            }

            // DEL -> Bereich löschen
            if (keyData == Keys.Delete)
            {
                BtnDeleteSelection_Click(null, EventArgs.Empty);
                return true;
            }

            // NUM 1 -> an den Anfang der Selektion springen
            if (keyData == Keys.NumPad1)
            {
                JumpToSelectionEdge(toStart: true);
                return true;
            }

            // NUM 2 -> ans Ende der Selektion springen
            if (keyData == Keys.NumPad2)
            {
                JumpToSelectionEdge(toStart: false);
                return true;
            }

            // NUM 0 -> an den Anfang des ganzen Clips springen
            if (keyData == Keys.NumPad0)
            {
                JumpToStartOfFile();
                return true;
            }

            // CTRL+NUM 0 -> View All (alles auszoomen)
            //if (keyData == (Keys.Control | Keys.NumPad0))
            //{
            //    ZoomAll();
            //    return true;
            //}

            // CTRL+N -> Bereich normalisieren
            if (keyData == (Keys.Control | Keys.N))
            {
                NormalizeSelection();
                return true;
            }

            // CTRL+E -> Bereich exportieren
            //if (keyData == (Keys.Control | Keys.E))
            //{
            //    ExportSelection();
            //    return true;
            //}

            //// CTRL+S -> Datei speichern (Overwrite/Rename)
            //if (keyData == (Keys.Control | Keys.S))
            //{
            //    SaveWithPrompt(this, null);
            //    return true;
            //}

            // CTRL+A -> Alles selektieren
            if (keyData == (Keys.Control | Keys.A))
            {
                SelectAll();
                return true;
            }

            // CTRL+SHIFT+S -> Save As (Format wählen)
            if (keyData == (Keys.Control | Keys.Shift | Keys.S))
            {
                SaveAsWithFormat(this, null);
                return true;
            }

            // CTRL+SHIFT+E -> Export Selection As (Format wählen)
            if (keyData == (Keys.Control | Keys.Shift | Keys.E))
            {
                ExportSelectionAs(this, null);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        private void SaveWithPrompt(object sender, EventArgs e)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // Falls wir schon eine WAV-Datei haben: Nachfrage Overwrite/Rename
            if (!string.IsNullOrEmpty(_currentFilePath) &&
                _currentFilePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show(this,
                    $"Datei überschreiben?\n{_currentFilePath}\n\n" +
                    "Yes = überschreiben\nNo = unter neuem Namen speichern\nCancel = abbrechen",
                    "Speichern",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Cancel)
                    return;

                if (result == DialogResult.Yes)
                {
                    SaveCurrentFile(saveAs: false);
                }
                else if (result == DialogResult.No)
                {
                    SaveCurrentFile(saveAs: true);
                }
            }
            else
            {
                // bisher keine oder kein WAV -> direkt „Speichern unter“
                SaveCurrentFile(saveAs: true);
            }
        }

        private void SaveAsWithFormat(object sender, EventArgs e)
        {

            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryChooseExportFileAndFormat(selectionOnly: false,
                    out string path, out AudioExportFormat format))
                return;

            ExportSamplesToFile(_currentSamples, _currentSampleRate, path, format);

            // Nur wenn wir wirklich den „Haupttrack“ speichern:
            _currentFilePath = path;
            _isDirty = false;
            UpdateWindowTitle();

        }
        private void MakeToolbarButtonLookFlat(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            // WICHTIG: Hover/Down-Farben NICHT hier anfassen – das macht StyleButtons()
            b.TabStop = false;
            b.BackgroundImageLayout = ImageLayout.Stretch;
        }


        private void MakeLoopButtonLookFlat(CheckBox cb)
        {
            cb.Appearance = Appearance.Button;
            cb.FlatStyle = FlatStyle.Flat;
            cb.FlatAppearance.BorderSize = 0;
            // Hover/Checked-Farben kommen von StyleButtons()
            cb.TabStop = false;
            cb.BackgroundImageLayout = ImageLayout.Stretch;
        }


        private void UpdatePlayButtonVisualState()
        {
            if (_btnPlay == null)
                return;

            var theme = CurrentTheme;

            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
            {
                // sanfter grüner Tint auf Basis der Theme-Farbe
                _btnPlay.BackColor = Blend(theme.ButtonBack, Color.LimeGreen, 0.55f);
            }
            else
            {
                _btnPlay.BackColor = theme.ButtonBack;
            }
        }

        private void ExportSelectionAs(object sender, EventArgs e)
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

            if (!TryChooseExportFileAndFormat(selectionOnly: true,
                    out string path, out AudioExportFormat format))
                return;

            var sel = new float[length];
            Array.Copy(_currentSamples, start, sel, 0, length);

            ExportSamplesToFile(sel, _currentSampleRate, path, format);

        }

        private void miFileExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void miViewZoomAll_Click(object sender, EventArgs e)
        {
            ZoomAll();
        }
        private void ZoomAll()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            _detailView.VisibleStartSample = 0;
            _detailView.VisibleSampleCount = 0; // 0 = „ganzer Track“
        }
    }
}
