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
        private ContextMenuStrip _detailContextMenu;

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

            // Scroll-Verhalten: Overview statisch, Detail scrollbar
            _overviewView.AllowHorizontalScroll = false;
            _detailView.AllowHorizontalScroll = true;

            // Anzeige-Optionen
            _overviewView.ShowDbScale = false;
            _detailView.ShowDbScale = true;

            // === DETAIL (unten) ===
            _detailView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            _detailView.SelectionChanged += DetailView_SelectionChanged;
            _detailView.VisibleRangeChanged += DetailView_VisibleRangeChanged;

            InitDetailContextMenu();

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
            MakeToolbarButtonLookFlat(_btnSave);
            MakeToolbarButtonLookFlat(_btnSaveAs);
            MakeToolbarButtonLookFlat(_btnExport);
            MakeToolbarButtonLookFlat(_btnTrim);
            MakeToolbarButtonLookFlat(_btnNormalize);
            MakeToolbarButtonLookFlat(_btnCompress);
            MakeToolbarButtonLookFlat(_btnFadeIn);
            MakeToolbarButtonLookFlat(_btnFadeOut);
            MakeLoopButtonLookFlat(_chkLoop);

            // Hover Zoom für Toolbar Buttons
            EnableHoverZoom(_btnOpen);
            EnableHoverZoom(_btnDeleteSelection);
            EnableHoverZoom(_btnUndo);
            EnableHoverZoom(_btnPlay);
            EnableHoverZoom(_btnStop);
            EnableHoverZoom(_btnTheme);
            EnableHoverZoom(_btnSave);
            EnableHoverZoom(_btnSaveAs);
            EnableHoverZoom(_btnExport);
            EnableHoverZoom(_btnTrim);
            EnableHoverZoom(_btnNormalize);
            EnableHoverZoom(_btnCompress);
            EnableHoverZoom(_btnFadeIn);
            EnableHoverZoom(_btnFadeOut);
            EnableHoverZoom(_chkLoop);
        }

        private void InitDetailContextMenu()
        {
            _detailContextMenu = new ContextMenuStrip();
            _detailContextMenu.ShowImageMargin = false;
            _detailContextMenu.Opening += DetailContextMenu_Opening;

            // Zoom Selection
            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Zoom", null,
                    (s, e) => ZoomSelection()));

            _detailContextMenu.Items.Add(new ToolStripSeparator());

            // Normalize Selection
            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Normalize", null,
                    (s, e) => NormalizeSelection()));

            // Compress Selection – ruft erstmal deinen Button-Handler auf
            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Compress", null,
                    (s, e) => _btnCompress_Click(_detailView, EventArgs.Empty)));

            //_detailContextMenu.Items.Add(new ToolStripSeparator());

            // Fade In / Fade Out – gleich wie Buttons
            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Fade In", null,
                    (s, e) => _btnFadeIn_Click(_detailView, EventArgs.Empty)));

            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Fade Out", null,
                    (s, e) => _btnFadeOut_Click(_detailView, EventArgs.Empty)));

            // Cut = wie DeleteSelection(dein DEL - Handler)
            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Cut selection", null,
                    (s, e) => BtnDeleteSelection_Click(_detailView, EventArgs.Empty)));

            // Silence-Submenü
            var silenceMenu = new ToolStripMenuItem("Silence");

            silenceMenu.DropDownItems.Add(
                new ToolStripMenuItem("Mute selection", null,
                    (s, e) => MuteSelection()));

            silenceMenu.DropDownItems.Add(
                new ToolStripMenuItem("Insert before", null,
                    (s, e) => InsertSilenceBeforeSelection()));

            silenceMenu.DropDownItems.Add(
                new ToolStripMenuItem("Insert after", null,
                    (s, e) => InsertSilenceAfterSelection()));

            _detailContextMenu.Items.Add(silenceMenu);
            _detailContextMenu.Items.Add(new ToolStripSeparator());

            // Export Selection
            _detailContextMenu.Items.Add(
                new ToolStripMenuItem("Export...", null,
                    (s, e) => ExportSelection()));
            //Donate

            _detailContextMenu.Items.Add(new ToolStripSeparator());
            _detailContextMenu.Items.Add(new ToolStripLabel());
            // Dem DetailView zuweisen
            _detailView.ContextMenuStrip = _detailContextMenu;
        }

        private void DetailContextMenu_Opening(object sender, CancelEventArgs e)
        {
            // Kein Clip geladen -> kein Menü
            if (_currentSamples == null || _currentSamples.Length == 0)
            {
                e.Cancel = true;
                return;
            }

            // Im DetailView muss eine gültige Selektion existieren
            if (_detailView == null ||
                !_detailView.TryGetSelection(out int start, out int end) ||
                end <= start)
            {
                e.Cancel = true;
                return;
            }
        }

        private void ZoomSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // Bearbeitungs-Selektion kommt NUR aus dem DetailView
            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Zoom Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int selectionLength = end - start;
            if (selectionLength <= 0)
                return;

            int total = _currentSamples.Length;

            // 1 Sekunde Luft links/rechts (Fallback 44.1k)
            int marginSamples = _currentSampleRate > 0 ? _currentSampleRate : 44100;

            // DetailView darf vor und nach dem Clip Luft haben (Negative + Air),
            // darum NICHT auf [0,total] clampen.
            int viewStart = start - marginSamples;
            int viewCount = selectionLength + marginSamples * 2;
            if (viewCount <= 0)
                viewCount = selectionLength;

            _detailView.ExtraScrollSamples = marginSamples;
            _detailView.VisibleStartSample = viewStart;
            _detailView.VisibleSampleCount = viewCount;

            // Overview-Fenster zur Info nachziehen (aber ohne Event)
            int ovStart = Math.Max(0, viewStart);
            int ovEnd = ovStart + viewCount;
            if (ovEnd > total) ovEnd = total;

            if (ovEnd > ovStart)
            {
                _overviewView.SetSelection(ovStart, ovEnd, raiseEvent: false);
            }
        }

        private void MuteSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Silence / Mute",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 0)
                return;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            // Stummschalten
            for (int i = start; i < end && i < _currentSamples.Length; i++)
            {
                _currentSamples[i] = 0f;
            }

            // Views neu füttern + Zoom & Selektion wiederherstellen (wie bei NormalizeSelection)
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

        private void InsertSilenceBeforeSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Silence / Insert before",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 0)
                return;

            int total = _currentSamples.Length;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            var newSamples = new float[total + length];

            // 0..start-1 unverändert
            if (start > 0)
                Array.Copy(_currentSamples, 0, newSamples, 0, start);

            // [start, start+length) bleibt 0.0f = Stille

            // Rest hinter der Selektion verschieben
            Array.Copy(_currentSamples, start, newSamples, start + length, total - start);

            _currentSamples = newSamples;
            int newTotal = _currentSamples.Length;

            // Zoom im DetailView beibehalten
            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            _detailView.VisibleStartSample = oldVisibleStart;
            _detailView.VisibleSampleCount = oldVisibleCount;

            // Selektion soll weiter den ursprünglichen Content markieren → nach rechts verschoben
            int newSelStart = start + length;
            int newSelEnd = end + length;

            _overviewView.SetSelection(newSelStart, newSelEnd, raiseEvent: false);
            _detailView.SetSelection(newSelStart, newSelEnd, raiseEvent: false);

            // Playhead, falls rechts vom Insert-Punkt, mitverschieben
            if (_playbackSamplePosition >= start)
                _playbackSamplePosition += length;
            if (_playbackSamplePosition >= newTotal)
                _playbackSamplePosition = newTotal - 1;

            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            UpdateInfo(_currentSampleRate, newTotal);
            UpdatePlaybackTimerInterval();

            _chkLoop.Checked = false;
            _isDirty = true;
            UpdateWindowTitle();
        }

        private void InsertSilenceAfterSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Silence / Insert after",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 0)
                return;

            int total = _currentSamples.Length;
            int insertPos = end; // direkt hinter der Auswahl

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            var newSamples = new float[total + length];

            // Alles bis inkl. Auswahl übernehmen
            if (insertPos > 0)
                Array.Copy(_currentSamples, 0, newSamples, 0, insertPos);

            // [insertPos, insertPos+length) = Stille (default 0.0f)

            // Rest hinten anhängen
            Array.Copy(_currentSamples, insertPos, newSamples, insertPos + length, total - insertPos);

            _currentSamples = newSamples;
            int newTotal = _currentSamples.Length;

            // Zoom im DetailView beibehalten
            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            _detailView.VisibleStartSample = oldVisibleStart;
            _detailView.VisibleSampleCount = oldVisibleCount;

            // Auswahl bleibt auf dem ursprünglichen Content [start, end]
            _overviewView.SetSelection(start, end, raiseEvent: false);
            _detailView.SetSelection(start, end, raiseEvent: false);

            // Playhead, falls hinter dem Insert-Punkt, mitschieben
            if (_playbackSamplePosition >= insertPos)
                _playbackSamplePosition += length;
            if (_playbackSamplePosition >= newTotal)
                _playbackSamplePosition = newTotal - 1;

            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            UpdateInfo(_currentSampleRate, newTotal);
            UpdatePlaybackTimerInterval();

            _chkLoop.Checked = false;
            _isDirty = true;
            UpdateWindowTitle();
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

            // LEFT / RIGHT arrows -> Playhead steuern (nur im Stop-Modus)
            {
                Keys keyCode = keyData & Keys.KeyCode;
                bool ctrl = (keyData & Keys.Control) == Keys.Control;
                bool shift = (keyData & Keys.Shift) == Keys.Shift;

                if (keyCode == Keys.Left || keyCode == Keys.Right)
                {
                    bool moveRight = (keyCode == Keys.Right);

                    // Sample-Rate (Fallback 44.1k)
                    int sr = _currentSampleRate > 0 ? _currentSampleRate : 44100;

                    int stepSamples;

                    if (ctrl)
                    {
                        // CTRL + Pfeil: ~1 Sekunde
                        stepSamples = sr;
                    }
                    else if (shift)
                    {
                        // SHIFT + Pfeil: 1 Sample (Feintuning)
                        stepSamples = 1;
                    }
                    else
                    {
                        // Pfeil ohne Modifier: ca. 10 ms
                        stepSamples = (int)Math.Max(1, Math.Round(sr * 0.01)); // 0.01 s
                    }

                    if (MoveLocatorBy(moveRight ? stepSamples : -stepSamples))
                        return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }
        /// <summary>
        /// Bewegt den Playhead um eine gegebene Anzahl Samples, wenn nicht abgespielt wird.
        /// Scrollt den Detail-View mit, falls der Playhead den sichtbaren Bereich verlässt.
        /// </summary>
        private bool MoveLocatorBy(int deltaSamples)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return false;

            // während des Playbacks keine manuelle Lokatorsteuerung
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
                return false;

            if (deltaSamples == 0)
                return false;

            int total = _currentSamples.Length;

            // neue Position clampen
            int newPos = _playbackSamplePosition + deltaSamples;
            if (newPos < 0) newPos = 0;
            if (newPos >= total) newPos = total - 1;

            if (newPos == _playbackSamplePosition)
                return false;

            _playbackSamplePosition = newPos;

            // beide Views aktualisieren
            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            // Detail-View mitscrollen lassen
            EnsurePlayheadVisibleInDetail();

            return true;
        }

        /// <summary>
        /// Passt VisibleStartSample im Detail-View so an, dass der Playhead im Sichtfenster liegt.
        /// Aktualisiert auch das Fenster im Overview.
        /// </summary>
        private void EnsurePlayheadVisibleInDetail()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            int total = _currentSamples.Length;
            int viewStart = _detailView.VisibleStartSample;
            int viewCount = _detailView.VisibleSampleCount;

            if (viewCount <= 0)
            {
                // falls noch nicht gesetzt: gesamter Clip sichtbar
                viewStart = 0;
                viewCount = total;
                _detailView.VisibleStartSample = viewStart;
                _detailView.VisibleSampleCount = viewCount;
            }

            int play = _playbackSamplePosition;
            int windowEnd = viewStart + viewCount;

            if (play < viewStart)
            {
                // nach links scrollen
                _detailView.VisibleStartSample = play;
            }
            else if (play >= windowEnd)
            {
                // nach rechts scrollen
                int newStart = play - viewCount + 1;
                if (newStart < 0) newStart = 0;
                _detailView.VisibleStartSample = newStart;
            }

            // Fensterausschnitt im Overview aktualisieren (nur Anzeige, kein Event)
            int vs = _detailView.VisibleStartSample;
            int ve = vs + _detailView.VisibleSampleCount;
            if (ve > total) ve = total;
            if (ve > vs)
            {
                _overviewView.SetSelection(vs, ve, raiseEvent: false);
            }
        }

        private void DetailView_VisibleRangeChanged(int startSample, int endSample)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            int total = _currentSamples.Length;
            if (total <= 0)
                return;

            // innerhalb der Datei einklemmen
            startSample = Math.Max(0, Math.Min(startSample, total));
            endSample = Math.Max(0, Math.Min(endSample, total));

            if (endSample <= startSample)
                return;

            // Im Overview als Auswahl anzeigen – aber OHNE Event,
            // damit OverviewView_SelectionChanged nicht zurückfeuert.
            _overviewView.SetSelection(startSample, endSample, raiseEvent: false);
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
            _detailView.VisibleSampleCount = _currentSamples.Length; // ganzer Track, aber scrollbar
        }

        private void _btnSave_Click(object sender, EventArgs e)
        {
            // „Normales“ Speichern mit Overwrite/Rename-Frage
            SaveWithPrompt(this, e);
        }

        private void _btnSaveAs_Click(object sender, EventArgs e)
        {
            // Speichern mit Formatwahl (WAV/MP3/AAC)
            SaveAsWithFormat(this, e);
        }

        private void _btnExport_Click(object sender, EventArgs e)
        {
            // Aktuelle Selektion als separate Datei exportieren (Formatwahl)
            ExportSelectionAs(this, e);
        }

        private void _btnCompress_Click(object sender, EventArgs e)
        {
            // Einfache Dynamik-Kompression nur auf den ausgewählten Bereich
            CompressSelection();
        }

        private void _btnTrim_Click(object sender, EventArgs e)
        {
            // Sehr leise Teile am ANFANG und ENDE des GESAMTEN Clips entfernen
            TrimSilenceAtStartAndEnd();
        }

        private void _btnFadeIn_Click(object sender, EventArgs e)
        {
            // Linearer Fade-In auf die aktuelle Selektion
            FadeInSelection();
        }

        private void _btnFadeOut_Click(object sender, EventArgs e)
        {
            // Linearer Fade-Out auf die aktuelle Selektion
            FadeOutSelection();
        }
        /// <summary>
        /// Aktualisiert Overview/Detail-View, Selektion, Playhead und UI,
        /// nachdem _currentSamples im ausgewählten Bereich verändert wurden.
        /// </summary>
        private void RefreshViewsAfterEditingSelection(
            int selectionStart,
            int selectionEnd,
            int oldVisibleStart,
            int oldVisibleCount)
        {
            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            _detailView.VisibleStartSample = oldVisibleStart;
            _detailView.VisibleSampleCount = oldVisibleCount;

            _overviewView.SetSelection(selectionStart, selectionEnd, raiseEvent: false);
            _detailView.SetSelection(selectionStart, selectionEnd, raiseEvent: false);

            _overviewView.PlaybackSample = _playbackSamplePosition;
            _detailView.PlaybackSample = _playbackSamplePosition;

            UpdateInfo(_currentSampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _isDirty = true;
            UpdateWindowTitle();
        }
        /// <summary>
        /// Wendet einen linearen Fade-In nur auf den aktuell selektierten Bereich an.
        /// </summary>
        private void FadeInSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Fade In",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 1)
                return;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            // Linearer Fade: 0.0 -> 1.0 über den Bereich
            for (int i = 0; i < length; i++)
            {
                float factor = (float)i / (length - 1); // 0..1
                _currentSamples[start + i] *= factor;
            }

            RefreshViewsAfterEditingSelection(start, end, oldVisibleStart, oldVisibleCount);
        }
        /// <summary>
        /// Wendet einen linearen Fade-Out nur auf den aktuell selektierten Bereich an.
        /// </summary>
        private void FadeOutSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Fade Out",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 1)
                return;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            // Linearer Fade: 1.0 -> 0.0 über den Bereich
            for (int i = 0; i < length; i++)
            {
                float factor = (float)(length - 1 - i) / (length - 1); // 1..0
                _currentSamples[start + i] *= factor;
            }

            RefreshViewsAfterEditingSelection(start, end, oldVisibleStart, oldVisibleCount);
        }
        /// <summary>
        /// Einfache Dynamik-Kompression nur im selektierten Bereich.
        /// Threshold und Ratio sind bewusst simpel gehalten.
        /// </summary>
        private void CompressSelection()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            if (!TryGetCurrentSelection(out int start, out int end))
            {
                MessageBox.Show(this, "Kein Bereich selektiert.", "Komprimieren",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int length = end - start;
            if (length <= 0)
                return;

            // Feste Parameter: Threshold ~ -6 dBFS, Ratio 4:1
            const float threshold = 0.5f;  // linear
            const float ratio = 4.0f;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            int oldVisibleStart = _detailView.VisibleStartSample;
            int oldVisibleCount = _detailView.VisibleSampleCount;

            for (int i = start; i < end; i++)
            {
                float s = _currentSamples[i];
                float abs = Math.Abs(s);

                if (abs <= threshold)
                    continue;

                float excess = abs - threshold;
                float compressed = threshold + excess / ratio;

                if (compressed > 1f) compressed = 1f;

                float sign = (s >= 0f) ? 1f : -1f;
                _currentSamples[i] = sign * compressed;
            }

            RefreshViewsAfterEditingSelection(start, end, oldVisibleStart, oldVisibleCount);
        }
        /// <summary>
        /// Entfernt sehr leise Bereiche am Anfang und Ende des GESAMTEN Clips.
        /// Die Selektion wird ignoriert.
        /// </summary>
        private void TrimSilenceAtStartAndEnd()
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // "Sehr leise" = kleiner als dieser Schwellwert
            const float threshold = 0.01f; // ~ -40 dBFS, ziemlich leise

            int total = _currentSamples.Length;
            int start = 0;
            int end = total - 1;

            // Anfang suchen
            while (start < total && Math.Abs(_currentSamples[start]) <= threshold)
            {
                start++;
            }

            // Ende suchen
            while (end >= start && Math.Abs(_currentSamples[end]) <= threshold)
            {
                end--;
            }

            // Nichts zu trimmen?
            if (start == 0 && end == total - 1)
            {
                MessageBox.Show(this,
                    "Am Anfang und Ende wurden keine sehr leisen Bereiche gefunden.",
                    "Trim",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            float[] newSamples;
            if (end < start)
            {
                // Alles ist "leise" -> komplett leer machen
                newSamples = Array.Empty<float>();
            }
            else
            {
                int newLength = end - start + 1;
                newSamples = new float[newLength];
                Array.Copy(_currentSamples, start, newSamples, 0, newLength);
            }

            _currentSamples = newSamples;

            // Views updaten
            _overviewView.Samples = _currentSamples;
            _detailView.Samples = _currentSamples;

            // Selektion & Loop zurücksetzen
            _overviewView.ClearSelection();
            _detailView.ClearSelection();
            _chkLoop.Checked = false;

            // Playhead an den Anfang
            _playbackSamplePosition = 0;
            _overviewView.PlaybackSample = 0;
            _detailView.PlaybackSample = 0;

            UpdateInfo(_currentSampleRate, _currentSamples.Length);
            UpdatePlaybackTimerInterval();

            _isDirty = true;
            UpdateWindowTitle();
        }

        private void _btnNormalize_Click(object sender, EventArgs e)
        {
            NormalizeSelection();
        }
    }
}
