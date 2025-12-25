using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    public class WaveformView : Control
    {
        private float[] _samples = Array.Empty<float>(); // nie null
        private float _zoom = 1.0f; // vertikaler Zoom
        private bool _isMouseDown;
        private int? _selectionStartSample;
        private int? _selectionEndSample;

        private int _playbackSample; // aktueller Playhead (Sampleindex)

        // Sichtbarer Ausschnitt (horizontal):
        // 0 / <=0 = ganzer Track
        private int _visibleStartSample = 0;
        private int _visibleSampleCount = 0;

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer per Klick den Playhead verschiebt.
        /// Übergibt den Sampleindex (global, bezogen auf _samples).
        /// </summary>
        public event Action<int> PlaybackPositionChangedByClick;

        /// <summary>
        /// Wird ausgelöst, wenn mit der Maus eine Auswahl gemacht wurde (MouseUp).
        /// Übergibt Start/Ende (global, bezogen auf _samples).
        /// </summary>
        public event Action<int, int> SelectionChanged;

        public WaveformView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            BackColor = Color.Black;
        }

        /// <summary>
        /// Mono-Samples im Bereich [-1, 1]
        /// </summary>
        public float[] Samples
        {
            get => _samples;
            set
            {
                _samples = value ?? Array.Empty<float>();
                ClearSelection();
                _playbackSample = 0;
                _visibleStartSample = 0;
                _visibleSampleCount = 0; // 0 = ganzer Track
                Invalidate();
            }
        }

        /// <summary>
        /// Vertikaler Zoomfaktor
        /// </summary>
        public float Zoom
        {
            get => _zoom;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;

                _zoom = Math.Max(0.1f, Math.Min(10f, value));
                Invalidate();
            }
        }

        /// <summary>
        /// Aktuelle Abspielposition in Samples (Playhead, global).
        /// </summary>
        public int PlaybackSample
        {
            get => _playbackSample;
            set
            {
                int total = _samples?.Length ?? 0;
                if (total <= 0)
                {
                    _playbackSample = 0;
                }
                else
                {
                    _playbackSample = Math.Max(0, Math.Min(value, total - 1));
                }
                Invalidate();
            }
        }

        /// <summary>
        /// Beginn des sichtbaren Bereichs (Sampleindex, global).
        /// </summary>
        public int VisibleStartSample
        {
            get => _visibleStartSample;
            set
            {
                _visibleStartSample = Math.Max(0, value);
                Invalidate();
            }
        }

        /// <summary>
        /// Anzahl der sichtbaren Samples. 0 oder kleiner = „ganzer Track ab VisibleStart“.
        /// </summary>
        public int VisibleSampleCount
        {
            get => _visibleSampleCount;
            set
            {
                _visibleSampleCount = value;
                Invalidate();
            }
        }

        private bool HasSelection =>
            _samples.Length > 0 &&
            _selectionStartSample.HasValue &&
            _selectionEndSample.HasValue &&
            _selectionStartSample.Value != _selectionEndSample.Value;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.Clear(BackColor);

            var samples = _samples ?? Array.Empty<float>();
            int totalSamples = samples.Length;

            if (totalSamples == 0 ||
                ClientSize.Width <= 1 ||
                ClientSize.Height <= 1)
            {
                using var b = new SolidBrush(Color.Gray);
                const string msg = "Keine Audiodatei geladen";
                var size = g.MeasureString(msg, Font);
                g.DrawString(msg, Font, b,
                    (ClientSize.Width - size.Width) / 2f,
                    (ClientSize.Height - size.Height) / 2f);
                return;
            }

            int width = ClientSize.Width;
            int height = ClientSize.Height;
            float midY = height / 2f;
            float scaleY = _zoom * (height / 2f - 4);

            // Sichtfenster berechnen
            int viewStart = Math.Max(0, Math.Min(_visibleStartSample, totalSamples));
            int maxCount = totalSamples - viewStart;
            int viewCount = _visibleSampleCount > 0
                ? Math.Min(_visibleSampleCount, maxCount)
                : maxCount;

            if (viewCount <= 0)
                return;

            // Wellenform zeichnen – nur im Sichtfenster
            for (int x = 0; x < width; x++)
            {
                int localStart = x * viewCount / width;
                int localEnd = (x + 1) * viewCount / width;

                if (localEnd <= localStart) localEnd = localStart + 1;
                if (localStart >= viewCount) break;
                if (localEnd > viewCount) localEnd = viewCount;

                int startSample = viewStart + localStart;
                int endSample = viewStart + localEnd;

                if (startSample >= totalSamples)
                    break;

                if (endSample > totalSamples)
                    endSample = totalSamples;

                float min = 0f;
                float max = 0f;

                for (int i = startSample; i < endSample; i++)
                {
                    if (i < 0 || i >= totalSamples)
                        continue;

                    float s = samples[i];
                    if (s < min) min = s;
                    if (s > max) max = s;
                }

                int y1 = (int)(midY - max * scaleY);
                int y2 = (int)(midY - min * scaleY);

                g.DrawLine(Pens.Lime, x, y1, x, y2);
            }

            // 0-Linie
            g.DrawLine(Pens.DarkGray, 0, (int)midY, width, (int)midY);

            // Auswahl zeichnen
            if (HasSelection)
            {
                var sel = GetNormalizedSelection(totalSamples);
                int selStart = sel.start;
                int selEnd = sel.end;

                // In sichtbares Fenster schneiden
                int windowStart = viewStart;
                int windowEnd = viewStart + viewCount;

                int drawStart = Math.Max(selStart, windowStart);
                int drawEnd = Math.Min(selEnd, windowEnd);

                if (drawEnd > drawStart)
                {
                    float x1 = (drawStart - viewStart) * width / (float)viewCount;
                    float x2 = (drawEnd - viewStart) * width / (float)viewCount;

                    if (x2 < x1)
                    {
                        float tmp = x1;
                        x1 = x2;
                        x2 = tmp;
                    }

                    using var brush = new SolidBrush(Color.FromArgb(80, Color.Yellow));
                    g.FillRectangle(brush, x1, 0, x2 - x1, height);
                }
            }

            // Playhead (rot), nur wenn innerhalb des sichtbaren Fensters
            if (totalSamples > 0)
            {
                int windowStart = viewStart;
                int windowEnd = viewStart + viewCount;

                if (_playbackSample >= windowStart && _playbackSample < windowEnd)
                {
                    int local = _playbackSample - viewStart;
                    int xPos = (int)(local * (width - 1) / (float)viewCount);

                    using var pen = new Pen(Color.Red, 1);
                    g.DrawLine(pen, xPos, 0, xPos, height);
                }
            }
        }

        private (int start, int end) GetNormalizedSelection(int totalSamples)
        {
            if (totalSamples <= 0)
                return (0, 0);

            int s = _selectionStartSample ?? 0;
            int e = _selectionEndSample ?? 0;
            if (s > e)
            {
                int tmp = s;
                s = e;
                e = tmp;
            }
            s = Math.Max(0, Math.Min(s, totalSamples));
            e = Math.Max(0, Math.Min(e, totalSamples));
            return (s, e);
        }

        private int XToSampleIndex(int x)
        {
            int width = ClientSize.Width;
            int totalSamples = _samples?.Length ?? 0;

            if (width <= 1 || totalSamples == 0)
                return 0;

            int viewStart = Math.Max(0, Math.Min(_visibleStartSample, totalSamples));
            int maxCount = totalSamples - viewStart;
            int viewCount = _visibleSampleCount > 0
                ? Math.Min(_visibleSampleCount, maxCount)
                : maxCount;

            if (viewCount <= 0)
                return 0;

            x = Math.Max(0, Math.Min(width - 1, x));
            int localIndex = x * viewCount / width;
            int sampleIndex = viewStart + localIndex;

            if (sampleIndex < 0) sampleIndex = 0;
            if (sampleIndex >= totalSamples) sampleIndex = totalSamples - 1;

            return sampleIndex;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_samples == null || _samples.Length == 0) return;

            _isMouseDown = true;
            int idx = XToSampleIndex(e.X);

            _selectionStartSample = idx;
            _selectionEndSample = idx;

            PlaybackSample = idx;
            PlaybackPositionChangedByClick?.Invoke(idx);

            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isMouseDown || _samples == null || _samples.Length == 0) return;

            _selectionEndSample = XToSampleIndex(e.X);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isMouseDown = false;

            if (_samples == null || _samples.Length == 0)
                return;

            if (HasSelection)
            {
                var sel = GetNormalizedSelection(_samples.Length);
                SelectionChanged?.Invoke(sel.start, sel.end);
            }
        }

        public void ClearSelection()
        {
            _selectionStartSample = null;
            _selectionEndSample = null;
            Invalidate();
        }

        public void DeleteSelection()
        {
            if (_samples == null || _samples.Length == 0 || !HasSelection)
                return;

            int totalSamples = _samples.Length;
            var (start, end) = GetNormalizedSelection(totalSamples);
            if (end <= start)
                return;

            int cutLength = end - start;
            int newLength = totalSamples - cutLength;
            if (newLength <= 0)
            {
                _samples = Array.Empty<float>();
                ClearSelection();
                PlaybackSample = 0;
                Invalidate();
                return;
            }

            var newSamples = new float[newLength];

            Array.Copy(_samples, 0, newSamples, 0, start);
            Array.Copy(_samples, end, newSamples, start, totalSamples - end);

            _samples = newSamples;

            // Playhead an Schnittstelle setzen
            PlaybackSample = start;

            ClearSelection();
            Invalidate();
        }
    }
}
