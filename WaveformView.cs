using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    public class WaveformView : Control
    {
        private float[] _samples = Array.Empty<float>(); // nie null
        private float _zoom = 1.0f; // vertikaler Zoom

        private int? _selectionStartSample;
        private int? _selectionEndSample;

        private int _playbackSample; // aktueller Playhead (Sampleindex)

        // Sichtbarer Ausschnitt (horizontal)
        private int _visibleStartSample = 0;
        private int _visibleSampleCount = 0; // <=0 = ganzer Track

        // Dragging / Selection
        private bool _isMouseDown;
        private DragMode _dragMode = DragMode.None;
        private const int EdgeHitPixels = 6; // Klick-Toleranz an Selektionskanten

        private bool _isHoveringEdge = false;

        private enum DragMode
        {
            None,
            NewSelection,
            ResizeLeft,
            ResizeRight
        }

        /// <summary>
        /// Wird ausgelöst, wenn der Benutzer per Klick den Playhead verschiebt.
        /// Übergibt den Sampleindex (global).
        /// </summary>
        public event Action<int> PlaybackPositionChangedByClick;

        /// <summary>
        /// Wird ausgelöst, wenn nach einer Auswahl (MouseUp oder SetSelection) 
        /// eine gültige Selektion vorliegt. Übergibt Start/Ende (global).
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

        // --------------------------------------------------------------------
        // Public Properties
        // --------------------------------------------------------------------

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
                _visibleSampleCount = 0; // ganzer Track
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

        // --------------------------------------------------------------------
        // Rendering
        // --------------------------------------------------------------------

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

                    // Kanten leicht hervorheben
                    using var edgePen = new Pen(Color.Gold, 2);
                    g.DrawLine(edgePen, x1, 0, x1, height);
                    g.DrawLine(edgePen, x2, 0, x2, height);
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

        // --------------------------------------------------------------------
        // Helper: Selektion & Mapping
        // --------------------------------------------------------------------

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

        private int SampleToX(int sampleIndex)
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

            int local = sampleIndex - viewStart;
            if (local < 0) local = 0;
            if (local > viewCount) local = viewCount;

            int x = (int)(local * (width - 1) / (float)viewCount);
            return x;
        }

        // --------------------------------------------------------------------
        // Maus-Interaktion
        // --------------------------------------------------------------------
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            Cursor = Cursors.Default;
            _isHoveringEdge = false;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_samples == null || _samples.Length == 0) return;

            _isMouseDown = true;
            int totalSamples = _samples.Length;

            // Klick-Sample
            int idx = XToSampleIndex(e.X);

            // Prüfen, ob wir eine Selektion haben und ob wir an einer Kante klicken
            if (HasSelection)
            {
                var sel = GetNormalizedSelection(totalSamples);
                int selStart = sel.start;
                int selEnd = sel.end;

                int xLeft = SampleToX(selStart);
                int xRight = SampleToX(selEnd);

                var dxLeft = Math.Abs(e.X - xLeft);
                var dxRight = Math.Abs(e.X - xRight);

                bool nearLeft = dxLeft <= EdgeHitPixels;
                bool nearRight = dxRight <= EdgeHitPixels;

                if (nearLeft && !nearRight)
                {
                    _dragMode = DragMode.ResizeLeft;
                    Cursor = Cursors.SizeWE;           // ⚡ Resize-Cursor setzen
                    return;
                }
                if (nearRight && !nearLeft)
                {
                    _dragMode = DragMode.ResizeRight;
                    Cursor = Cursors.SizeWE;           // ⚡ Resize-Cursor setzen
                    return;
                }
                if (nearLeft && nearRight)
                {
                    // extrem schmale Auswahl: nimm z.B. rechts
                    _dragMode = DragMode.ResizeRight;
                    Cursor = Cursors.SizeWE;           // ⚡ Resize-Cursor setzen
                    return;
                }
            }

            // Sonst: neue Selektion beginnen
            _dragMode = DragMode.NewSelection;

            _selectionStartSample = idx;
            _selectionEndSample = idx;

            PlaybackSample = idx;
            PlaybackPositionChangedByClick?.Invoke(idx);

            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_samples == null || _samples.Length == 0)
            {
                Cursor = Cursors.Default;
                _isHoveringEdge = false;
                return;
            }

            int totalSamples = _samples.Length;

            // -------- HOVER-LOGIK FÜR KANTEN (wenn keine Maustaste gedrückt) --------
            if (!_isMouseDown && HasSelection)
            {
                var sel = GetNormalizedSelection(totalSamples);
                int selStart = sel.start;
                int selEnd = sel.end;

                int xLeft = SampleToX(selStart);
                int xRight = SampleToX(selEnd);

                var dxLeft = Math.Abs(e.X - xLeft);
                var dxRight = Math.Abs(e.X - xRight);

                bool nearLeft = dxLeft <= EdgeHitPixels;
                bool nearRight = dxRight <= EdgeHitPixels;

                if (nearLeft || nearRight)
                {
                    Cursor = Cursors.SizeWE;       // ⚡ über Kante -> Resize-Cursor
                    _isHoveringEdge = true;
                }
                else if (_isHoveringEdge)
                {
                    Cursor = Cursors.Default;      // weg von der Kante -> Normal
                    _isHoveringEdge = false;
                }
            }

            // -------- DRAGGEN (nur wenn Maus gedrückt) --------
            if (!_isMouseDown || _dragMode == DragMode.None)
                return;

            int idx = XToSampleIndex(e.X);

            switch (_dragMode)
            {
                case DragMode.NewSelection:
                    _selectionEndSample = idx;
                    break;

                case DragMode.ResizeLeft:
                    {
                        int end = _selectionEndSample ?? idx;
                        _selectionStartSample = idx;
                        // Start darf nicht rechts von End liegen
                        var sel = GetNormalizedSelection(totalSamples);
                        _selectionStartSample = sel.start;
                        _selectionEndSample = sel.end;
                        break;
                    }

                case DragMode.ResizeRight:
                    {
                        int start = _selectionStartSample ?? idx;
                        _selectionEndSample = idx;
                        var sel = GetNormalizedSelection(totalSamples);
                        _selectionStartSample = sel.start;
                        _selectionEndSample = sel.end;
                        break;
                    }
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isMouseDown = false;

            if (_samples == null || _samples.Length == 0)
            {
                _dragMode = DragMode.None;
                Cursor = Cursors.Default;
                _isHoveringEdge = false;
                return;
            }

            if (_selectionStartSample.HasValue && _selectionEndSample.HasValue)
            {
                // 🔁 Selektion einmal „in Stein meißeln“: start <= end
                var sel = GetNormalizedSelection(_samples.Length);
                _selectionStartSample = sel.start;
                _selectionEndSample = sel.end;

                if (sel.start != sel.end)
                {
                    // Nur wenn echte Länge vorhanden ist, Event auslösen
                    SelectionChanged?.Invoke(sel.start, sel.end);
                }
                else
                {
                    // Nichts ausgewählt → Selektion löschen
                    ClearSelection();
                }
            }

            _dragMode = DragMode.None;
            Cursor = Cursors.Default;
            _isHoveringEdge = false;
        }



        // --------------------------------------------------------------------
        // Public API für Selektion
        // --------------------------------------------------------------------

        public void ClearSelection()
        {
            _selectionStartSample = null;
            _selectionEndSample = null;
            Invalidate();
        }

        /// <summary>
        /// Setzt eine Selektion programmatisch (globale Sampleindices).
        /// Optional kann SelectionChanged ausgelöst werden.
        /// </summary>
        public void SetSelection(int startSample, int endSample, bool raiseEvent = true)
        {
            int total = _samples?.Length ?? 0;
            if (total <= 0)
            {
                ClearSelection();
                return;
            }

            if (endSample < startSample)
            {
                int tmp = startSample;
                startSample = endSample;
                endSample = tmp;
            }

            startSample = Math.Max(0, Math.Min(startSample, total));
            endSample = Math.Max(0, Math.Min(endSample, total));

            _selectionStartSample = startSample;
            _selectionEndSample = endSample;

            Invalidate();

            if (raiseEvent && HasSelection)
            {
                var sel = GetNormalizedSelection(total);
                SelectionChanged?.Invoke(sel.start, sel.end);
            }
        }

        /// <summary>
        /// Schneidet die aktuelle Auswahl aus dem Sample-Puffer.
        /// </summary>
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
