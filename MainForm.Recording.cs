using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    public partial class MainForm
    {
        // This partial owns the real ASIO device, captured sample buffers, and transfer into the editor document.

        private ToolStripStatusLabel? _recordStatusLabel;
        private ToolStripProgressBar? _recordLevelMeter;
        private System.Windows.Forms.Timer? _recordUiTimer;
        private AsioRecordingSettingsForm? _recordingStudio;

        private AsioOut? _asioRecorder;
        // ASIO callbacks run off the UI thread, so buffer mutation and meter publication must be synchronized.
        private readonly object _recordingSamplesLock = new();
        private List<float>? _recordedSamples;
        private float[]? _asioCaptureBuffer;
        private Stopwatch? _recordStopwatch;
        private volatile bool _isRecording;
        private int _recordPeakBits;
        private float _displayedRecordPeak;
        private Exception? _recordingCallbackException;

        private string? _lastAsioDriverName;
        private int _lastAsioInputOffset;
        private int _lastAsioSampleRate = 48000;

        private IWin32Window RecordingMessageOwner =>
            _recordingStudio is { IsDisposed: false } ? _recordingStudio : this;

        /// <summary>Creates the recording status controls and timer used by both the main window and Mini-Studio.</summary>
        private void InitAsioRecordingUi()
        {
            _recordStatusLabel = new ToolStripStatusLabel
            {
                Text = "REC",
                Visible = false
            };

            _recordLevelMeter = new ToolStripProgressBar
            {
                Minimum = 0,
                Maximum = 1000,
                Value = 0,
                Size = new Size(110, 16),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            _statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
            _statusStrip.Items.Add(_recordStatusLabel);
            _statusStrip.Items.Add(_recordLevelMeter);

            _recordUiTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _recordUiTimer.Tick += (_, _) => UpdateRecordingUi();
        }

        private void ToggleAsioRecording()
        {
            if (_isRecording)
                StopAsioRecording(loadIntoEditor: true, showErrors: true);
            else
                StartAsioRecording();
        }

        /// <summary>Opens or focuses the Mini-Studio after protecting any unsaved current document.</summary>
        private void StartAsioRecording()
        {
            if (_isClosing)
                return;

            if (_recordingStudio != null && !_recordingStudio.IsDisposed)
            {
                _recordingStudio.Activate();
                return;
            }

            int preferredSampleRate = _currentSampleRate > 0
                ? _currentSampleRate
                : _lastAsioSampleRate;

            using var studio = new AsioRecordingSettingsForm(
                _lastAsioDriverName,
                _lastAsioInputOffset,
                preferredSampleRate);

            _recordingStudio = studio;
            studio.StartRecordingRequested += RecordingStudio_StartRecordingRequested;
            studio.StopRecordingRequested += RecordingStudio_StopRecordingRequested;

            try
            {
                studio.ShowDialog(this);
            }
            finally
            {
                if (_isRecording)
                    StopAsioRecording(loadIntoEditor: true, showErrors: false);

                studio.StartRecordingRequested -= RecordingStudio_StartRecordingRequested;
                studio.StopRecordingRequested -= RecordingStudio_StopRecordingRequested;
                _recordingStudio = null;
                UpdateMenuOnlyUi();
            }
        }

        private void RecordingStudio_StartRecordingRequested(object? sender, EventArgs e)
        {
            if (_isClosing || _isRecording || sender is not AsioRecordingSettingsForm studio)
                return;

            if (!CheckUnsavedChangesBeforeRecording())
            {
                studio.SetRecordingState(false);
                return;
            }

            BeginAsioRecording(studio);
        }

        private void RecordingStudio_StopRecordingRequested(object? sender, EventArgs e)
        {
            StopAsioRecording(loadIntoEditor: true, showErrors: true);
        }

        /// <summary>Claims the selected ASIO input, initializes one-channel capture, and starts the record clock.</summary>
        private bool BeginAsioRecording(AsioRecordingSettingsForm studio)
        {
            StopNormalPlaybackForRecording();

            AsioOut? recorder = null;
            try
            {
                _lastAsioDriverName = studio.DriverName;
                _lastAsioInputOffset = studio.InputChannelOffset;
                _lastAsioSampleRate = studio.SampleRate;

                _recordedSamples = new List<float>(Math.Max(studio.SampleRate * 10, 4096));
                _asioCaptureBuffer = null;
                _recordingCallbackException = null;
                _recordPeakBits = 0;
                _displayedRecordPeak = 0f;

                recorder = new AsioOut(studio.DriverName)
                {
                    InputChannelOffset = studio.InputChannelOffset,
                    AutoStop = false
                };

                recorder.InitRecordAndPlayback(
                    waveProvider: null,
                    recordChannels: 1,
                    recordOnlySampleRate: studio.SampleRate);

                recorder.AudioAvailable += AsioRecorder_AudioAvailable;
                recorder.PlaybackStopped += AsioRecorder_PlaybackStopped;
                recorder.DriverResetRequest += AsioRecorder_DriverResetRequest;

                _asioRecorder = recorder;
                _recordStopwatch = Stopwatch.StartNew();
                _isRecording = true;

                SetRecordingUiState(true);
                recorder.Play();
                _recordUiTimer?.Start();
                return true;
            }
            catch (Exception ex)
            {
                _isRecording = false;

                if (recorder != null)
                {
                    try { recorder.Dispose(); } catch { }
                }

                _asioRecorder = null;
                _recordedSamples = null;
                _asioCaptureBuffer = null;
                SetRecordingUiState(false);

                MessageBox.Show(
                    studio,
                    "Die ASIO-Aufnahme konnte nicht gestartet werden:\n\n" + ex.Message +
                    "\n\nPrüfe, ob der MOTU-M4-Treiber installiert ist und nicht bereits exklusiv von einer anderen Anwendung benutzt wird.",
                    "ASIO-Aufnahme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>Stops callbacks first, detaches the driver, snapshots captured samples, and optionally opens the result.</summary>
        private void StopAsioRecording(bool loadIntoEditor, bool showErrors)
        {
            if (!_isRecording && _asioRecorder == null)
                return;

            _isRecording = false;
            _recordUiTimer?.Stop();
            _recordStopwatch?.Stop();

            AsioOut? recorder = _asioRecorder;
            _asioRecorder = null;

            if (recorder != null)
            {
                recorder.AudioAvailable -= AsioRecorder_AudioAvailable;
                recorder.PlaybackStopped -= AsioRecorder_PlaybackStopped;
                recorder.DriverResetRequest -= AsioRecorder_DriverResetRequest;

                try { recorder.Stop(); } catch { }
                try { recorder.Dispose(); } catch { }
            }

            float[] samples;
            lock (_recordingSamplesLock)
            {
                samples = _recordedSamples?.ToArray() ?? Array.Empty<float>();
                _recordedSamples = null;
            }

            _asioCaptureBuffer = null;
            SetRecordingUiState(false);

            Exception? callbackError = _recordingCallbackException;
            _recordingCallbackException = null;

            if (loadIntoEditor && samples.Length > 0)
                LoadRecordedSamplesIntoEditor(samples, _lastAsioSampleRate);
            else if (loadIntoEditor && showErrors)
            {
                MessageBox.Show(
                    RecordingMessageOwner,
                    "Es wurden keine Audiodaten aufgenommen.",
                    "ASIO-Aufnahme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            if (callbackError != null && showErrors)
            {
                MessageBox.Show(
                    RecordingMessageOwner,
                    "Während der Aufnahme ist ein ASIO-Fehler aufgetreten:\n\n" + callbackError.Message,
                    "ASIO-Aufnahme",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        /// <summary>Sanitizes incoming samples, appends them to the recording buffer, and publishes the latest peak atomically.</summary>
        private void AsioRecorder_AudioAvailable(object? sender, AsioAudioAvailableEventArgs e)
        {
            if (!_isRecording)
                return;

            try
            {
                int sampleCount = e.SamplesPerBuffer * Math.Max(1, e.InputBuffers.Length);
                if (_asioCaptureBuffer == null || _asioCaptureBuffer.Length != sampleCount)
                    _asioCaptureBuffer = new float[sampleCount];

#pragma warning disable CS0618
                e.GetAsInterleavedSamples(_asioCaptureBuffer);
#pragma warning restore CS0618

                float peak = 0f;
                for (int i = 0; i < sampleCount; i++)
                {
                    float sample = _asioCaptureBuffer[i];
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                        sample = 0f;
                    else
                        sample = Math.Clamp(sample, -1f, 1f);

                    _asioCaptureBuffer[i] = sample;
                    float absolute = Math.Abs(sample);
                    if (absolute > peak)
                        peak = absolute;
                }

                lock (_recordingSamplesLock)
                {
                    if (_isRecording)
                        _recordedSamples?.AddRange(_asioCaptureBuffer);
                }

                Interlocked.Exchange(ref _recordPeakBits, BitConverter.SingleToInt32Bits(peak));
            }
            catch (Exception ex)
            {
                if (Interlocked.CompareExchange(ref _recordingCallbackException, ex, null) == null)
                {
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (_isRecording)
                                StopAsioRecording(loadIntoEditor: true, showErrors: true);
                        }));
                    }
                    catch
                    {
                        // Form is already closing.
                    }
                }
            }
        }

        /// <summary>Handles an unexpected driver stop on the UI thread while preserving already captured audio.</summary>
        private void AsioRecorder_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (!_isRecording)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!_isRecording)
                        return;

                    StopAsioRecording(loadIntoEditor: true, showErrors: false);
                    MessageBox.Show(
                        RecordingMessageOwner,
                        e.Exception == null
                            ? "Der ASIO-Treiber hat die Aufnahme beendet."
                            : "Der ASIO-Treiber hat die Aufnahme beendet:\n\n" + e.Exception.Message,
                        "ASIO-Aufnahme",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }));
            }
            catch
            {
                // Form is already closing.
            }
        }

        /// <summary>Ends capture cleanly when the vendor control panel changes the active ASIO configuration.</summary>
        private void AsioRecorder_DriverResetRequest(object? sender, EventArgs e)
        {
            if (!_isRecording)
                return;

            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (!_isRecording)
                        return;

                    StopAsioRecording(loadIntoEditor: true, showErrors: false);
                    MessageBox.Show(
                        RecordingMessageOwner,
                        "Die MOTU-ASIO-Einstellungen wurden während der Aufnahme geändert. " +
                        "Die Aufnahme wurde deshalb beendet. Starte sie danach neu.",
                        "ASIO-Treiber geändert",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }));
            }
            catch
            {
                // Form is already closing.
            }
        }

        /// <summary>Applies peak decay and sends a stable meter/time display to both recording interfaces.</summary>
        private void UpdateRecordingUi()
        {
            if (!_isRecording)
                return;

            float newestPeak = BitConverter.Int32BitsToSingle(
                Interlocked.Exchange(ref _recordPeakBits, 0));

            _displayedRecordPeak = Math.Max(newestPeak, _displayedRecordPeak * 0.82f);

            if (_recordLevelMeter != null)
            {
                int value = (int)Math.Round(Math.Clamp(_displayedRecordPeak, 0f, 1f) * 1000f);
                _recordLevelMeter.Value = Math.Clamp(value, _recordLevelMeter.Minimum, _recordLevelMeter.Maximum);
            }

            TimeSpan elapsed = _recordStopwatch?.Elapsed ?? TimeSpan.Zero;

            if (_recordStatusLabel != null)
            {
                string level = _displayedRecordPeak > 0.000001f
                    ? $"{20.0 * Math.Log10(_displayedRecordPeak):0.0} dB"
                    : "-∞ dB";
                string clip = _displayedRecordPeak >= 0.99f ? "  CLIP!" : string.Empty;

                _recordStatusLabel.Text =
                    $"REC {elapsed.Hours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds / 100}  {level}{clip}";
            }

            if (_recordingStudio is { IsDisposed: false } studio)
                studio.UpdateRecordingLevel(_displayedRecordPeak, elapsed);
        }

        private void SetRecordingUiState(bool recording)
        {
            if (_recordingStudio is { IsDisposed: false } studio)
                studio.SetRecordingState(recording);

            if (_recordStatusLabel != null)
                _recordStatusLabel.Visible = recording;

            if (_recordLevelMeter != null)
            {
                if (!recording)
                    _recordLevelMeter.Value = 0;

                _recordLevelMeter.Visible = recording;
            }

            UpdateMenuOnlyUi();
        }

        private void StopNormalPlaybackForRecording()
        {
            _playbackTimer?.Stop();

            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                try { _waveOut.Stop(); } catch { }
                _waveOut.Dispose();
                _waveOut = null;
            }

            _currentProvider = null;
            UpdatePlayButtonVisualState();
        }

        /// <summary>Prevents a new recording from silently replacing edited samples already in the document.</summary>
        private bool CheckUnsavedChangesBeforeRecording()
        {
            if (_currentSamples == null || _currentSamples.Length == 0 || !_isDirty)
                return true;

            string fileName = string.IsNullOrEmpty(_currentFilePath)
                ? "unbenannter Clip"
                : System.IO.Path.GetFileName(_currentFilePath);

            DialogResult result = MessageBox.Show(
                RecordingMessageOwner,
                $"Du hast ungespeicherte Änderungen an \"{fileName}\".\n\n" +
                "Möchtest du sie exportieren, bevor du eine neue Aufnahme startest?",
                "Änderungen exportieren?",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (result == DialogResult.Cancel)
                return false;

            if (result == DialogResult.No)
                return true;

            return ExportWholeTrackForUnsavedChanges();
        }

        /// <summary>Turns a completed mono recording into a new unsaved document and resets playback/video state.</summary>
        private void LoadRecordedSamplesIntoEditor(float[] samples, int sampleRate)
        {
            if (samples.Length == 0 || sampleRate <= 0)
                return;

            ClearVideoState();

            _currentSamples = samples;
            _currentSampleRate = sampleRate;
            _currentChannels = 1;

            _overviewView.SampleRate = sampleRate;
            _detailView.SampleRate = sampleRate;

            _overviewView.Samples = _currentSamples;
            _overviewView.VisibleStartSample = 0;
            _overviewView.VisibleSampleCount = 0;
            _overviewView.SetHighlightRange(null, null);
            _overviewView.ClearSelection();

            _detailView.Samples = _currentSamples;
            _detailView.VisibleStartSample = 0;
            _detailView.VisibleSampleCount = 0;
            _detailView.ClearSelection();

            _playbackSamplePosition = 0;
            _overviewView.PlaybackSample = 0;
            _detailView.PlaybackSample = 0;

            _undoStack.Clear();
            _chkLoop.Checked = false;

            _currentFilePath = string.Empty;
            _isDirty = true;

            UpdateInfo(sampleRate, samples.Length);
            _lblInfo.Text = $"Neue ASIO-Aufnahme • {samples.Length / (double)sampleRate:0.00} s • {sampleRate} Hz • Mono";
            UpdatePlaybackTimerInterval();
            UpdateStatusBar();
            UpdateWindowTitle();

            _overviewView.Invalidate();
            _detailView.Invalidate();
        }
    }
}
