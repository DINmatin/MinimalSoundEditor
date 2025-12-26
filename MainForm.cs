using System;
using System.Collections.Generic;
using System.Windows.Forms;
using NAudio.Wave;

namespace MinimalSoundEditor
{
    public class MainForm : Form
    {
        private WaveformView _overviewView;
        private WaveformView _detailView;
        private Button _btnOpen;
        private Button _btnZoomIn;
        private Button _btnZoomOut;
        private Button _btnDeleteSelection;
        private Button _btnPlay;
        private Button _btnStop;
        private Label _lblInfo;

        // Audio-Daten
        private float[] _currentSamples = Array.Empty<float>();
        private int _currentSampleRate = 44100;

        // Playback
        private WaveOutEvent _waveOut;
        private SimpleArraySampleProvider _currentProvider;
        private System.Windows.Forms.Timer _playbackTimer;
        private int _playbackSamplePosition; // aktueller Sampleindex
        private double _trackDurationSeconds; // Gesamtdauer
        private bool _isClosing;

        // Undo
        private Stack<float[]> _undoStack = new Stack<float[]>();

        public MainForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "Minimal Sound Editor";
            Width = 1000;
            Height = 600;
            KeyPreview = true;

            // Overview (oben, klein)
            _overviewView = new WaveformView
            {
                Dock = DockStyle.Fill,
                Zoom = 0.5f
            };
            _overviewView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            _overviewView.SelectionChanged += OverviewView_SelectionChanged;
            _overviewView.MouseDoubleClick += OverviewView_MouseDoubleClick;

            var overviewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80
            };
            overviewPanel.Controls.Add(_overviewView);

            // Detail (unten, für Editing)
            _detailView = new WaveformView
            {
                Dock = DockStyle.Fill,
                Zoom = 1.0f
            };
            _detailView.PlaybackPositionChangedByClick += Waveform_PlaybackPositionChangedByClick;
            // SelectionChanged vom Detail verwenden wir für Editing (DeleteSelection), aber
            // nicht zum Zoomen – deshalb hier kein Handler.

            _btnOpen = new Button
            {
                Text = "Öffnen...",
                Width = 90,
                Left = 10,
                Top = 10
            };
            _btnOpen.Click += BtnOpen_Click;

            _btnZoomIn = new Button
            {
                Text = "Zoom +",
                Width = 80,
                Left = 110,
                Top = 10
            };
            _btnZoomIn.Click += (s, e) =>
            {
                _detailView.Zoom *= 1.5f;
                UpdateInfo();
            };

            _btnZoomOut = new Button
            {
                Text = "Zoom -",
                Width = 80,
                Left = 200,
                Top = 10
            };
            _btnZoomOut.Click += (s, e) =>
            {
                _detailView.Zoom /= 1.5f;
                UpdateInfo();
            };

            _btnDeleteSelection = new Button
            {
                Text = "Auswahl löschen",
                Width = 120,
                Left = 290,
                Top = 10
            };
            _btnDeleteSelection.Click += BtnDeleteSelection_Click;

            _btnPlay = new Button
            {
                Text = "Play",
                Width = 70,
                Left = 420,
                Top = 10
            };
            _btnPlay.Click += BtnPlay_Click;

            _btnStop = new Button
            {
                Text = "Stop",
                Width = 70,
                Left = 500,
                Top = 10
            };
            _btnStop.Click += BtnStop_Click;

            _lblInfo = new Label
            {
                Text = "Keine Datei geladen",
                AutoSize = true,
                Left = 580,
                Top = 15
            };

            var topPanel = new Panel
            {
                Height = 45,
                Dock = DockStyle.Top
            };

            topPanel.Controls.AddRange(new Control[]
            {
                _btnOpen,
                _btnZoomIn,
                _btnZoomOut,
                _btnDeleteSelection,
                _btnPlay,
                _btnStop,
                _lblInfo
            });

            Controls.Add(_detailView);
            Controls.Add(overviewPanel);
            Controls.Add(topPanel);

            // Playback-Timer (UI-Thread)
            _playbackTimer = new System.Windows.Forms.Timer
            {
                Interval = 5
            };
            _playbackTimer.Tick += PlaybackTimer_Tick;

            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;
        }

        // Tastatur-Shortcuts
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // CTRL+O -> Öffnen
            if (keyData == (Keys.Control | Keys.O))
            {
                BtnOpen_Click(null, EventArgs.Empty);
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

            return base.ProcessCmdKey(ref msg, keyData);
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

        private void LoadAudioFile(string filePath)
        {
            using var reader = new NAudio.Wave.AudioFileReader(filePath);

            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;

            _currentSampleRate = sampleRate;

            var monoSamples = new List<float>();

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
                _lblInfo.Text = $"Samples: {_currentSamples.Length} | Zoom: {_detailView.Zoom:0.00}x";
                return;
            }

            _trackDurationSeconds = sampleCount / (double)sampleRate;
            _lblInfo.Text = $"Samples: {sampleCount} | Dauer: {_trackDurationSeconds:0.00} s | Zoom: {_detailView.Zoom:0.00}x";
        }

        private void UpdatePlaybackTimerInterval()
        {
            if (_currentSamples == null || _currentSamples.Length == 0 || _currentSampleRate <= 0)
                return;

            int width = _detailView.Width;
            if (width <= 0)
                width = 1000;

            _trackDurationSeconds = _currentSamples.Length / (double)_currentSampleRate;

            double idealMs = (_trackDurationSeconds * 1000.0) / width;

            int intervalMs = (int)Math.Max(2, Math.Min(idealMs, 15));

            if (_playbackTimer != null)
                _playbackTimer.Interval = intervalMs;
        }

        private void BtnDeleteSelection_Click(object sender, EventArgs e)
        {
            if (_currentSamples == null || _currentSamples.Length == 0)
                return;

            // Undo sichern
            _undoStack.Push(CloneSamples(_currentSamples));

            // Nur im Detail-Track löschen
            _detailView.DeleteSelection();

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
        }

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
        }

        // PLAY
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

            _waveOut = new WaveOutEvent();

            _currentProvider = new SimpleArraySampleProvider(
                _currentSamples,
                _currentSampleRate,
                1,
                _playbackSamplePosition);

            _waveOut.Init(_currentProvider);
            _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
            _waveOut.Play();

            _playbackTimer?.Start();
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
                    }));
                }
            }
            catch { }
        }

        // STOP
        private void BtnStop_Click(object sender, EventArgs e)
        {
            if (_waveOut == null)
                return;

            try { _waveOut.Stop(); } catch { }
            _playbackTimer?.Stop();
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
            _playbackSamplePosition = sampleIndex;
            _overviewView.PlaybackSample = sampleIndex;
            _detailView.PlaybackSample = sampleIndex;
        }

        // Overview-Auswahl -> Zoom im Detail
        private void OverviewView_SelectionChanged(int startSample, int endSample)
        {
            if (endSample <= startSample)
                return;

            int length = endSample - startSample;

            _detailView.VisibleStartSample = startSample;
            _detailView.VisibleSampleCount = length;
        }
    }

    /// <summary>
    /// Einfacher SampleProvider für float[]-Buffer, mit Startposition und Positionsabfrage.
    /// </summary>
    public class SimpleArraySampleProvider : ISampleProvider
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
}
