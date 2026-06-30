using NAudio.Wave;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    /// <summary>
    /// Small recording console that selects an ASIO source, previews its level, and delegates
    /// actual recording ownership to MainForm through start/stop events.
    /// </summary>
    internal sealed class AsioRecordingSettingsForm : Form
    {
        private readonly ComboBox _driverBox = new();
        private readonly ComboBox _inputBox = new();
        private readonly ComboBox _sampleRateBox = new();
        private readonly Label _statusLabel = new();
        private readonly Button _startButton = new();
        private readonly Button _closeButton = new();
        private readonly Button _controlPanelButton = new();

        private readonly Panel _levelTrack = new();
        private readonly Panel _levelFill = new();
        private readonly Label _levelValueLabel = new();
        private readonly System.Windows.Forms.Timer _levelUiTimer = new() { Interval = 50 };
        private readonly System.Windows.Forms.Timer _previewRestartTimer = new() { Interval = 180 };

        // Preview capture is deliberately separate from the real recorder so metering never stores audio.
        private AsioOut? _previewRecorder;
        private float[]? _previewCaptureBuffer;
        private int _previewPeakBits;
        private float _displayedPreviewPeak;
        private bool _isClosing;
        private bool _recording;

        private readonly string? _preferredDriver;
        private readonly int _preferredInputOffset;
        private readonly int _preferredSampleRate;

        public event EventHandler? StartRecordingRequested;
        public event EventHandler? StopRecordingRequested;

        public string DriverName => _driverBox.SelectedItem?.ToString() ?? string.Empty;
        public int InputChannelOffset => Math.Max(0, _inputBox.SelectedIndex);
        public int SampleRate => _sampleRateBox.SelectedItem is int value ? value : 48000;
        public bool IsRecording => _recording;

        /// <summary>Restores the last-used device choices and prepares the live input meter.</summary>
        public AsioRecordingSettingsForm(
            string? preferredDriver,
            int preferredInputOffset,
            int preferredSampleRate)
        {
            _preferredDriver = preferredDriver;
            _preferredInputOffset = Math.Max(0, preferredInputOffset);
            _preferredSampleRate = preferredSampleRate > 0 ? preferredSampleRate : 48000;

            Text = "Mini-Studio";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 315);
            Font = new Font("Segoe UI", 9F);

            BuildUi();

            _levelUiTimer.Tick += (_, _) => UpdateInputLevelUi();
            _previewRestartTimer.Tick += (_, _) =>
            {
                _previewRestartTimer.Stop();
                StartInputPreview();
            };

            Load += (_, _) => LoadDrivers();
            Shown += (_, _) => ScheduleInputPreviewRestart();
            FormClosing += OnFormClosing;
            FormClosed += (_, _) =>
            {
                _isClosing = true;
                StopInputPreview();
                _levelUiTimer.Dispose();
                _previewRestartTimer.Dispose();
            };
        }

        /// <summary>Creates the compact studio controls in code so the dialog remains self-contained.</summary>
        private void BuildUi()
        {
            var driverLabel = new Label
            {
                AutoSize = true,
                Location = new Point(18, 22),
                Text = "ASIO-Treiber"
            };

            _driverBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _driverBox.Location = new Point(145, 18);
            _driverBox.Size = new Size(270, 23);
            _driverBox.SelectedIndexChanged += (_, _) =>
            {
                if (_recording)
                    return;

                StopInputPreview();
                ReloadInputs();
            };

            _controlPanelButton.Text = "Control Panel";
            _controlPanelButton.Location = new Point(423, 17);
            _controlPanelButton.Size = new Size(82, 25);
            _controlPanelButton.Click += (_, _) => OpenControlPanel();

            var inputLabel = new Label
            {
                AutoSize = true,
                Location = new Point(18, 65),
                Text = "Eingang"
            };

            _inputBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _inputBox.Location = new Point(145, 61);
            _inputBox.Size = new Size(180, 23);
            _inputBox.SelectedIndexChanged += (_, _) => ScheduleInputPreviewRestart();

            var rateLabel = new Label
            {
                AutoSize = true,
                Location = new Point(18, 108),
                Text = "Samplerate"
            };

            _sampleRateBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _sampleRateBox.Location = new Point(145, 104);
            _sampleRateBox.Size = new Size(180, 23);
            _sampleRateBox.Items.AddRange(new object[]
            {
                44100, 48000, 88200, 96000, 176400, 192000
            });
            _sampleRateBox.SelectedIndexChanged += (_, _) => ScheduleInputPreviewRestart();

            int preferredIndex = _sampleRateBox.Items.IndexOf(_preferredSampleRate);
            _sampleRateBox.SelectedIndex = preferredIndex >= 0
                ? preferredIndex
                : _sampleRateBox.Items.IndexOf(48000);

            var levelLabel = new Label
            {
                AutoSize = true,
                Location = new Point(18, 151),
                Text = "Eingangspegel"
            };

            _levelTrack.Location = new Point(145, 145);
            _levelTrack.Size = new Size(270, 24);
            _levelTrack.BackColor = Color.FromArgb(48, 52, 55);
            _levelTrack.BorderStyle = BorderStyle.FixedSingle;

            _levelFill.Location = new Point(0, 0);
            _levelFill.Size = new Size(0, _levelTrack.ClientSize.Height);
            _levelFill.BackColor = Color.MediumSeaGreen;
            _levelTrack.Controls.Add(_levelFill);

            _levelValueLabel.Location = new Point(423, 148);
            _levelValueLabel.Size = new Size(82, 21);
            _levelValueLabel.TextAlign = ContentAlignment.MiddleRight;
            _levelValueLabel.Text = "-∞ dB";

            var scaleLabel = new Label
            {
                Location = new Point(145, 171),
                Size = new Size(270, 16),
                ForeColor = Color.DimGray,
                Text = "-60 dB                         -12       -3       0",
                TextAlign = ContentAlignment.TopLeft
            };

            _statusLabel.Location = new Point(18, 194);
            _statusLabel.Size = new Size(487, 48);
            _statusLabel.ForeColor = Color.DimGray;
            _statusLabel.Text = "Der ausgewählte Eingang wird als Mono-Spur aufgenommen.";

            _startButton.Text = "Aufnahme starten";
            _startButton.Location = new Point(270, 268);
            _startButton.Size = new Size(148, 32);
            _startButton.Click += (_, _) => ToggleRecording();

            _closeButton.Text = "Schließen";
            _closeButton.Location = new Point(426, 268);
            _closeButton.Size = new Size(79, 32);
            _closeButton.Click += (_, _) => Close();

            Controls.AddRange(new Control[]
            {
                driverLabel,
                _driverBox,
                _controlPanelButton,
                inputLabel,
                _inputBox,
                rateLabel,
                _sampleRateBox,
                levelLabel,
                _levelTrack,
                _levelValueLabel,
                scaleLabel,
                _statusLabel,
                _startButton,
                _closeButton
            });

            AcceptButton = _startButton;
            CancelButton = _closeButton;
        }

        /// <summary>Switches the dialog between editable preview mode and the locked red recording state.</summary>
        public void SetRecordingState(bool recording)
        {
            _recording = recording;

            _driverBox.Enabled = !recording;
            _inputBox.Enabled = !recording;
            _sampleRateBox.Enabled = !recording;
            _controlPanelButton.Enabled = !recording && !string.IsNullOrWhiteSpace(DriverName);

            if (recording)
            {
                StopInputPreview();

                _startButton.Text = "Aufnahme stoppen";
                _startButton.BackColor = Color.Firebrick;
                _startButton.ForeColor = Color.White;
                _startButton.FlatStyle = FlatStyle.Flat;
                _startButton.FlatAppearance.BorderColor = Color.DarkRed;
                _startButton.UseVisualStyleBackColor = false;

                _statusLabel.ForeColor = Color.Firebrick;
                _statusLabel.Text =
                    $"REC • Input {InputChannelOffset + 1} • {SampleRate} Hz • Mono\n" +
                    "Die Aufnahme läuft. Mit dem roten Knopf wird sie beendet und in den Editor übernommen.";
            }
            else
            {
                _startButton.Text = "Aufnahme starten";
                _startButton.FlatStyle = FlatStyle.Standard;
                _startButton.ForeColor = SystemColors.ControlText;
                _startButton.BackColor = SystemColors.Control;
                _startButton.UseVisualStyleBackColor = true;

                _statusLabel.ForeColor = Color.DimGray;
                _statusLabel.Text = "Bereit für eine neue Aufnahme.";

                if (!_isClosing)
                    ScheduleInputPreviewRestart();
            }
        }

        /// <summary>Receives the real recorder peak and elapsed time for the live REC display.</summary>
        public void UpdateRecordingLevel(float peak, TimeSpan elapsed)
        {
            if (!_recording || _isClosing)
                return;

            SetInputLevelUi(peak);
            _statusLabel.ForeColor = peak >= 0.99f ? Color.Firebrick : Color.DimGray;
            _statusLabel.Text =
                $"REC {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}  " +
                $"• Input {InputChannelOffset + 1} • {SampleRate} Hz • Mono";
        }

        /// <summary>Validates settings before raising start/stop requests; MainForm owns the audio buffers.</summary>
        private void ToggleRecording()
        {
            if (_recording)
            {
                StopRecordingRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (!ValidateSettingsForRecording())
            {
                ScheduleInputPreviewRestart();
                return;
            }

            StopInputPreview();
            StartRecordingRequested?.Invoke(this, EventArgs.Empty);

            // Bei Abbruch der Sicherheitsabfrage oder einem Startfehler bleibt das
            // Mini-Studio geöffnet und zeigt wieder den Live-Pegel.
            if (!_recording && !_isClosing)
                ScheduleInputPreviewRestart();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.R))
            {
                ToggleRecording();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Enumerates installed ASIO drivers and prefers the remembered or MOTU entry.</summary>
        private void LoadDrivers()
        {
            try
            {
                string[] drivers = AsioOut.GetDriverNames();
                _driverBox.Items.Clear();
                _driverBox.Items.AddRange(drivers.Cast<object>().ToArray());

                if (drivers.Length == 0)
                {
                    SetUnavailable("Kein ASIO-Treiber gefunden. Bitte zuerst den MOTU-M-Series-Treiber installieren.");
                    return;
                }

                int selectedIndex = -1;

                if (!string.IsNullOrWhiteSpace(_preferredDriver))
                {
                    selectedIndex = Array.FindIndex(
                        drivers,
                        d => string.Equals(d, _preferredDriver, StringComparison.OrdinalIgnoreCase));
                }

                if (selectedIndex < 0)
                {
                    selectedIndex = Array.FindIndex(
                        drivers,
                        d => d.Contains("MOTU", StringComparison.OrdinalIgnoreCase));
                }

                _driverBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
            }
            catch (Exception ex)
            {
                SetUnavailable("ASIO-Treiber konnten nicht gelesen werden: " + ex.Message);
            }
        }

        /// <summary>Reopens the selected driver briefly to discover its current input-channel count.</summary>
        private void ReloadInputs()
        {
            if (_recording)
                return;

            StopInputPreview();
            _inputBox.Items.Clear();
            _startButton.Enabled = false;
            _controlPanelButton.Enabled = false;

            if (string.IsNullOrWhiteSpace(DriverName))
                return;

            try
            {
                using var asio = new AsioOut(DriverName);
                int inputCount = asio.DriverInputChannelCount;

                if (inputCount <= 0)
                {
                    SetUnavailable("Dieser ASIO-Treiber meldet keine Aufnahme-Eingänge.");
                    return;
                }

                for (int i = 0; i < inputCount; i++)
                    _inputBox.Items.Add($"Input {i + 1}");

                _inputBox.SelectedIndex = Math.Min(_preferredInputOffset, inputCount - 1);
                _startButton.Enabled = true;
                _controlPanelButton.Enabled = true;
                _statusLabel.ForeColor = Color.DimGray;
                _statusLabel.Text = $"{inputCount} Eingangskanäle gefunden. Der gewählte Kanal wird als Mono-Spur aufgenommen.";
                ScheduleInputPreviewRestart();
            }
            catch (Exception ex)
            {
                SetUnavailable("Der ASIO-Treiber konnte nicht geöffnet werden: " + ex.Message);
            }
        }

        /// <summary>Debounces rapid device changes so the ASIO driver is not reopened for every UI event.</summary>
        private void ScheduleInputPreviewRestart()
        {
            if (_isClosing || _recording || !Visible)
                return;

            _previewRestartTimer.Stop();
            _previewRestartTimer.Start();
        }

        /// <summary>Starts one-channel, record-only ASIO capture used exclusively for the level meter.</summary>
        private void StartInputPreview()
        {
            StopInputPreview();

            if (_isClosing ||
                _recording ||
                string.IsNullOrWhiteSpace(DriverName) ||
                _inputBox.SelectedIndex < 0 ||
                _sampleRateBox.SelectedIndex < 0)
            {
                return;
            }

            AsioOut? recorder = null;
            try
            {
                recorder = new AsioOut(DriverName)
                {
                    InputChannelOffset = InputChannelOffset,
                    AutoStop = false
                };

                if (!recorder.IsSampleRateSupported(SampleRate))
                {
                    recorder.Dispose();
                    recorder = null;
                    SetPreviewUnavailable($"{SampleRate} Hz wird in der aktuellen MOTU-Konfiguration nicht unterstützt.");
                    return;
                }

                recorder.InitRecordAndPlayback(
                    waveProvider: null,
                    recordChannels: 1,
                    recordOnlySampleRate: SampleRate);

                recorder.AudioAvailable += PreviewRecorder_AudioAvailable;
                recorder.PlaybackStopped += PreviewRecorder_PlaybackStopped;

                _previewCaptureBuffer = null;
                _previewPeakBits = 0;
                _displayedPreviewPeak = 0f;
                _previewRecorder = recorder;

                recorder.Play();
                _levelUiTimer.Start();

                _statusLabel.ForeColor = Color.DimGray;
                _statusLabel.Text =
                    $"Live-Vorschau: Input {InputChannelOffset + 1}, {SampleRate} Hz. " +
                    "Der Pegel wird nur angezeigt und nicht über die Lautsprecher wiedergegeben.";
            }
            catch (Exception ex)
            {
                if (recorder != null)
                {
                    try { recorder.Dispose(); } catch { }
                }

                _previewRecorder = null;
                SetPreviewUnavailable("Live-Pegel nicht verfügbar: " + ex.Message);
            }
        }

        /// <summary>Detaches callbacks before disposing the driver to avoid late events touching a closed dialog.</summary>
        private void StopInputPreview()
        {
            _previewRestartTimer.Stop();
            _levelUiTimer.Stop();

            AsioOut? recorder = _previewRecorder;
            _previewRecorder = null;

            if (recorder != null)
            {
                recorder.AudioAvailable -= PreviewRecorder_AudioAvailable;
                recorder.PlaybackStopped -= PreviewRecorder_PlaybackStopped;

                try { recorder.Stop(); } catch { }
                try { recorder.Dispose(); } catch { }
            }

            _previewCaptureBuffer = null;
            Interlocked.Exchange(ref _previewPeakBits, 0);
            _displayedPreviewPeak = 0f;
            SetInputLevelUi(0f);
        }

        /// <summary>Calculates only the buffer peak and publishes it atomically to the UI timer.</summary>
        private void PreviewRecorder_AudioAvailable(object? sender, AsioAudioAvailableEventArgs e)
        {
            if (_previewRecorder == null || !ReferenceEquals(sender, _previewRecorder))
                return;

            try
            {
                int sampleCount = e.SamplesPerBuffer * Math.Max(1, e.InputBuffers.Length);
                if (_previewCaptureBuffer == null || _previewCaptureBuffer.Length != sampleCount)
                    _previewCaptureBuffer = new float[sampleCount];

#pragma warning disable CS0618
                e.GetAsInterleavedSamples(_previewCaptureBuffer);
#pragma warning restore CS0618

                float peak = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float sample = _previewCaptureBuffer[i];
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                        continue;

                    float absolute = Math.Abs(sample);
                    if (absolute > peak)
                        peak = absolute;
                }

                Interlocked.Exchange(
                    ref _previewPeakBits,
                    BitConverter.SingleToInt32Bits(Math.Clamp(peak, 0f, 1f)));
            }
            catch
            {
                // Die eigentliche Aufnahme zeigt einen ausführlichen Fehlerdialog.
                // In der Vorschau genügt es, den Meter stehen zu lassen.
            }
        }

        private void PreviewRecorder_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (_isClosing || _recording || _previewRecorder == null || !ReferenceEquals(sender, _previewRecorder))
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (_isClosing || _recording || _previewRecorder == null)
                        return;

                    StopInputPreview();
                    SetPreviewUnavailable(
                        e.Exception == null
                            ? "Die ASIO-Live-Vorschau wurde vom Treiber beendet."
                            : "Die ASIO-Live-Vorschau wurde beendet: " + e.Exception.Message);
                }));
            }
            catch
            {
                // Dialog wurde bereits geschlossen.
            }
        }

        private void UpdateInputLevelUi()
        {
            float newestPeak = BitConverter.Int32BitsToSingle(
                Interlocked.Exchange(ref _previewPeakBits, 0));

            _displayedPreviewPeak = Math.Max(newestPeak, _displayedPreviewPeak * 0.82f);
            SetInputLevelUi(_displayedPreviewPeak);
        }

        /// <summary>Maps linear amplitude to a -60..0 dB meter and highlights warning/clip ranges.</summary>
        private void SetInputLevelUi(float peak)
        {
            peak = Math.Clamp(peak, 0f, 1f);

            double db = peak > 0.000001f
                ? 20.0 * Math.Log10(peak)
                : double.NegativeInfinity;

            double normalized = double.IsNegativeInfinity(db)
                ? 0.0
                : Math.Clamp((db + 60.0) / 60.0, 0.0, 1.0);

            int availableWidth = Math.Max(0, _levelTrack.ClientSize.Width - 2);
            _levelFill.Location = new Point(0, 0);
            _levelFill.Height = Math.Max(0, _levelTrack.ClientSize.Height - 2);
            _levelFill.Width = (int)Math.Round(availableWidth * normalized);

            if (peak >= 0.707f)
                _levelFill.BackColor = Color.IndianRed;
            else if (peak >= 0.251f)
                _levelFill.BackColor = Color.Goldenrod;
            else
                _levelFill.BackColor = Color.MediumSeaGreen;

            if (peak >= 0.99f)
            {
                _levelValueLabel.ForeColor = Color.Firebrick;
                _levelValueLabel.Text = "CLIP!";
            }
            else
            {
                _levelValueLabel.ForeColor = SystemColors.ControlText;
                _levelValueLabel.Text = double.IsNegativeInfinity(db)
                    ? "-∞ dB"
                    : $"{db:0.0} dB";
            }
        }

        private void SetPreviewUnavailable(string message)
        {
            SetInputLevelUi(0f);
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = message;
        }

        /// <summary>Releases the preview before opening the vendor panel because many ASIO drivers are single-client.</summary>
        private void OpenControlPanel()
        {
            if (_recording || string.IsNullOrWhiteSpace(DriverName))
                return;

            StopInputPreview();

            try
            {
                using (var asio = new AsioOut(DriverName))
                {
                    asio.ShowControlPanel();
                }

                ReloadInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Das ASIO-Control-Panel konnte nicht geöffnet werden:\n" + ex.Message,
                    "ASIO",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                ScheduleInputPreviewRestart();
            }
        }

        /// <summary>Checks that the selected driver can actually run at the requested sample rate.</summary>
        private bool ValidateSettingsForRecording()
        {
            if (string.IsNullOrWhiteSpace(DriverName) || _inputBox.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Bitte ASIO-Treiber und Eingang auswählen.", "Mini-Studio");
                return false;
            }

            try
            {
                using var asio = new AsioOut(DriverName);
                if (!asio.IsSampleRateSupported(SampleRate))
                {
                    MessageBox.Show(
                        this,
                        $"Der Treiber unterstützt {SampleRate} Hz in der aktuellen Konfiguration nicht.\n" +
                        "Bitte eine andere Samplerate wählen oder das MOTU-Control-Panel prüfen.",
                        "Samplerate nicht verfügbar",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Die ASIO-Einstellungen konnten nicht geprüft werden:\n" + ex.Message,
                    "Mini-Studio",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_recording)
                StopRecordingRequested?.Invoke(this, EventArgs.Empty);

            _isClosing = true;
            StopInputPreview();
        }

        private void SetUnavailable(string message)
        {
            StopInputPreview();
            _statusLabel.ForeColor = Color.Firebrick;
            _statusLabel.Text = message;
            _startButton.Enabled = false;
            _controlPanelButton.Enabled = false;
        }
    }
}
