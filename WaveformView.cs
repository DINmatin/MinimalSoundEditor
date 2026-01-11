using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;


namespace MinimalSoundEditor
{
    public class WaveformViewTheme
    {
        public Color Background { get; set; }
        public Color WaveColor { get; set; }
        public Color ZeroLineColor { get; set; }
        public Color SelectionFillColor { get; set; }
        public Color SelectionEdgeColor { get; set; }
        public Color PlayheadColor { get; set; }
        public Color TextColor { get; set; }
    }

    public class WaveformView : Control
    {
        private const int RULER_HEIGHT = 14;   // oben: Zeit-Leiste
        private const int DB_SCALE_MARGIN = 40; // Breite der linken dB-Skala

        private float[] _samples = Array.Empty<float>(); // nie null
        private float _zoom = 1.0f; // vertikaler Zoom

        private int? _selectionStartSample;
        private int? _selectionEndSample;

        // zusätzlicher Anzeige-Bereich (z.B. Bearbeitungs-Selektion aus DetailView)
        private int? _highlightStartSample;
        private int? _highlightEndSample;

        private int _playbackSample; // aktueller Playhead (Sampleindex)

        // Sichtbarer Ausschnitt (horizontal)
        private int _visibleStartSample = 0;
        private int _visibleSampleCount = 0; // <=0 = ganzer Track

        // Dragging / Selection
        private bool _isMouseDown;
        private DragMode _dragMode = DragMode.None;
        private const int EdgeHitPixels = 6; // Klick-Toleranz an Selektionskanten
        private bool _isHoveringEdge = false;

        // für „Auswahl verschieben“
        private int _moveSelectionOffsetSamples;
        private int _moveSelectionLengthSamples;

        public int LastDeletedStartSample { get; private set; }
        public int LastDeletedSampleCount { get; private set; }
        private enum DragMode
        {
            None,
            NewSelection,
            ResizeLeft,
            ResizeRight,
            MoveSelection
        }

        // Peak-Cache + Bitmap-Cache
        private struct Peak
        {
            public float Min;
            public float Max;
        }
        /// <summary>
        /// Liefert die vertikale Skalierung für Amplituden.
        /// Amplitude 1.0 (= 0 dBFS) entspricht etwa der oberen Kante
        /// des Wellenformbereichs.
        /// </summary>
        private float GetAmplitudeScaleY(int contentHeight)
        {
            // halbe Höhe minus ein kleiner Rand
            return contentHeight / 2f - 4f;
        }

        private Peak[] _cachedPeaks = Array.Empty<Peak>();
        private int _cachedViewStart = -1;
        private int _cachedViewCount = -1;
        private int _cachedWidth = -1;
        private int _cachedHeight = -1;

        private Bitmap _waveformBitmap;
        private bool _peaksDirty = true;
        private bool _bitmapDirty = true;

        private WaveformViewTheme _theme;

        private int _sampleRate = 44100;
        /// <summary>
        /// Abtastrate in Hz, wird für die Zeit-Skala verwendet.
        /// </summary>
        public int SampleRate
        {
            get => _sampleRate;
            set
            {
                _sampleRate = value > 0 ? value : 44100;
                Invalidate();
            }
        }
        /// <summary>
        /// Zeigt links eine einfache dB-Skala an (nur im Detail-View sinnvoll).
        /// </summary>
        public bool ShowDbScale { get; set; }


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

        /// <summary>
        /// Wird ausgelöst, wenn der sichtbare Ausschnitt (Start/Ende) 
        /// durch User-Interaktion geändert wurde (Scrollen/Zoomen).
        /// </summary>
        public event Action<int, int> VisibleRangeChanged;

        public WaveformView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw,
                     true);

            _theme = new WaveformViewTheme
            {
                Background = Color.Black,
                WaveColor = Color.Lime,
                ZeroLineColor = Color.DimGray,
                SelectionFillColor = Color.FromArgb(80, Color.Yellow),
                SelectionEdgeColor = Color.Gold,
                PlayheadColor = Color.Red,
                TextColor = Color.Gray
            };
            BackColor = Color.Black;
            UpdateStyles();
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
                _visibleSampleCount = 0; // ganzer Track

                MarkPeaksDirty();
                MarkBitmapDirty();
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

                // nur vertikale Skalierung -> Peaks bleiben, Bitmap neu
                MarkBitmapDirty();
                Invalidate();
            }
        }
        public void ApplyTheme(WaveformViewTheme theme)
        {
            if (theme == null) return;

            // eigenes Theme-Objekt pro View (kein gemeinsames Reference)
            _theme = new WaveformViewTheme
            {
                Background = theme.Background,
                WaveColor = theme.WaveColor,
                ZeroLineColor = theme.ZeroLineColor,
                SelectionFillColor = theme.SelectionFillColor,
                SelectionEdgeColor = theme.SelectionEdgeColor,
                PlayheadColor = theme.PlayheadColor,
                TextColor = theme.TextColor
            };

            BackColor = _theme.Background;

            MarkPeaksDirty();
            MarkBitmapDirty();
            Invalidate();
        }

        /// <summary>
        /// Überschreibt nur die Farben der Selektion.
        /// </summary>
        public void SetSelectionColors(Color fill, Color edge)
        {
            if (_theme == null)
                _theme = new WaveformViewTheme();

            _theme.SelectionFillColor = fill;
            _theme.SelectionEdgeColor = edge;
            Invalidate();
        }

        /// <summary>
        /// Setzt einen zweiten, nur visuell angezeigten Bereich (z.B. Bearbeitungs-Selection).
        /// Übergabe null/null löscht das Highlight.
        /// </summary>
        public void SetHighlightRange(int? startSample, int? endSample)
        {
            if (startSample.HasValue && endSample.HasValue && endSample > startSample)
            {
                _highlightStartSample = startSample.Value;
                _highlightEndSample = endSample.Value;
            }
            else
            {
                _highlightStartSample = null;
                _highlightEndSample = null;
            }

            Invalidate();
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
        private static double GetNiceTimeStep(double visibleSeconds)
        {
            // sehr einfache Heuristik für Ticks
            if (visibleSeconds <= 1.0) return 0.1;   // 100 ms
            if (visibleSeconds <= 5.0) return 0.5;   // 500 ms
            if (visibleSeconds <= 10.0) return 1.0;   // 1 s
            if (visibleSeconds <= 30.0) return 2.0;   // 2 s
            if (visibleSeconds <= 60.0) return 5.0;   // 5 s
            if (visibleSeconds <= 300.0) return 10.0;  // 10 s
            if (visibleSeconds <= 900.0) return 30.0;  // 30 s
            return 60.0;                               // 1 min
        }

        /// <summary>
        /// Erster sichtbarer Sampleindex. Darf NEGATIV sein (Luft vor dem Clip),
        /// wird aber auf [-_extraScrollSamples, ...] begrenzt.
        /// </summary>
        public int VisibleStartSample
        {
            get => _visibleStartSample;
            set
            {
                int minStart = -_extraScrollSamples;
                _visibleStartSample = value < minStart ? minStart : value;
                MarkPeaksDirty();
                MarkBitmapDirty();
                Invalidate();
            }
        }
        /// <summary>
        /// Wie viele Samples man maximal VOR und NACH dem Clip scrollen darf.
        /// Wird typischerweise auf "1 Sekunde in Samples" gesetzt.
        /// </summary>
        private int _extraScrollSamples = 0;
        public int ExtraScrollSamples
        {
            get => _extraScrollSamples;
            set => _extraScrollSamples = Math.Max(0, value);
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
                MarkPeaksDirty();
                MarkBitmapDirty();
                Invalidate();
            }
        }

        /// <summary>
        /// Wenn true, kann der sichtbare Ausschnitt horizontal verschoben werden
        /// (z.B. per Mausrad oder Auto-Scroll beim Draggen).
        /// </summary>
        public bool AllowHorizontalScroll { get; set; } = true;

        private bool HasSelection =>
            _samples.Length > 0 &&
            _selectionStartSample.HasValue &&
            _selectionEndSample.HasValue &&
            _selectionStartSample.Value != _selectionEndSample.Value;


        /// <summary>
        /// Öffentliche Abfrage, ob aktuell eine Selektion existiert.
        /// </summary>
        public bool HasActiveSelection => HasSelection;

        /// <summary>
        /// Gibt die aktuelle (normalisierte) Selektion zurück.
        /// </summary>
        public bool TryGetSelection(out int startSample, out int endSample)
        {
            if (!HasSelection)
            {
                startSample = 0;
                endSample = 0;
                return false;
            }

            var sel = GetNormalizedSelection(_samples.Length);
            startSample = sel.start;
            endSample = sel.end;
            return true;
        }

        // --------------------------------------------------------------------
        // Rendering
        // --------------------------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            var samples = _samples ?? Array.Empty<float>();
            int totalSamples = samples.Length;

            int leftMargin = ShowDbScale ? DB_SCALE_MARGIN : 0;

            // zu klein? => nix machen
            if (width <= 1 || height <= 1)
            {
                return;
            }

            // keine Samples => Demo-Timeline zeichnen (0–5 s) und raus
            if (totalSamples == 0)
            {
                DrawEmptyTimeline(g, width, height);
                return;
            }


            // Sichtfenster berechnen
            // Darf VOR 0 und NACH dem Clip liegen.
            // Alles außerhalb [0, totalSamples) wird als Stille gezeichnet.
            int viewStart = _visibleStartSample;
            int viewCount = _visibleSampleCount > 0
                ? _visibleSampleCount
                : Math.Max(totalSamples, 1);




            if (viewCount <= 0)
            {
                g.Clear(BackColor);
                return;
            }


            // Haben sich Geometrie / Sichtfenster geändert?
            if (width != _cachedWidth ||
                height != _cachedHeight ||
                viewStart != _cachedViewStart ||
                viewCount != _cachedViewCount)
            {
                _cachedWidth = width;
                _cachedHeight = height;
                _cachedViewStart = viewStart;
                _cachedViewCount = viewCount;
                MarkPeaksDirty();
                MarkBitmapDirty();
            }

            // Peaks ggf. neu berechnen
            if (_peaksDirty)
            {
                RebuildPeaks(samples, totalSamples, viewStart, viewCount, width);
            }

            // Bitmap ggf. neu aufbauen
            if (_bitmapDirty)
            {
                RebuildWaveformBitmap(height);
            }

            // Hintergrund-Waveform zeichnen
            if (_waveformBitmap != null)
            {
                g.DrawImageUnscaled(_waveformBitmap, 0, 0);
            }
            else
            {
                g.Clear(BackColor);
            }
            // ---------------------------------------------
            // Luft-Bereiche einfärben (links < 0, rechts > total)
            // -> jetzt NACH der Waveform, aber nur im Wellenform-Bereich
            // ---------------------------------------------
            if (ClientSize.Width > 0 && viewCount > 0)
            {
                int contentTop = RULER_HEIGHT;
                int contentHeight = height - contentTop;
                if (contentHeight < 0) contentHeight = 0;

                // 50 % dunkler als die Wave-Hintergrundfarbe
                var baseBg = _theme?.Background ?? BackColor;
                var dark = DarkenColor(baseBg, 0.5f);

                double pxPerSample = (double)ClientSize.Width / viewCount;

                // ===== LINKER BEREICH (t < 0) =====
                if (viewStart < 0)
                {
                    int samplesLeft = -viewStart;
                    int px = (int)(samplesLeft * pxPerSample);
                    if (px > 0)
                    {
                        using (var b = new SolidBrush(dark))
                            g.FillRectangle(b, 0, contentTop, px, contentHeight);
                    }
                }

                // ===== RECHTER BEREICH (t > clipEnde) =====
                if (viewStart + viewCount > totalSamples)
                {
                    int overflow = (viewStart + viewCount) - totalSamples;
                    int px = (int)(overflow * pxPerSample);
                    if (px > 0)
                    {
                        int startX = ClientSize.Width - px;
                        if (startX < 0) startX = 0;

                        using (var b = new SolidBrush(dark))
                            g.FillRectangle(b, startX, contentTop,
                                            ClientSize.Width - startX, contentHeight);
                    }
                }
            }

            // Zeit-Leiste als eigener Balken oben
            using (var rulerBrush = new SolidBrush(GetRulerBackColor()))
            {
                g.FillRectangle(rulerBrush, 0, 0, width, RULER_HEIGHT);
            }

            // Zeit-Skala oben einblenden (Wavelab-Style light)
            if (_sampleRate > 0)
            {
                double startSeconds = viewStart / (double)_sampleRate;
                double visibleSeconds = viewCount / (double)_sampleRate;
                double endSeconds = startSeconds + visibleSeconds;

                double step = GetNiceTimeStep(visibleSeconds);
                int rulerHeight = 12; // Höhe der Tick-Striche

                using var tickPen = new Pen(_theme.ZeroLineColor, 1);
                using var textBrush = new SolidBrush(_theme.TextColor);
                var font = this.Font;

                // erster Tick >= startSeconds
                double firstTick = Math.Ceiling(startSeconds / step) * step;

                for (double t = firstTick; t <= endSeconds; t += step)
                {
                    double samplePos = t * _sampleRate;
                    double rel = (samplePos - viewStart) / viewCount; // 0..1
                    float x = (float)(rel * width);
                    if (x < 0 || x > width) continue;

                    // Tick-Strich
                    g.DrawLine(tickPen, x, 0, x, rulerHeight);

                    // Beschriftung: 0.0s, 1.0s, 5s, 10s ...
                    string label = (t < 10.0) ? $"{t:0.0}s" : $"{t:0}s";
                    var size = g.MeasureString(label, font);
                    float textX = x - size.Width / 2f;
                    float textY = rulerHeight; // direkt unter den Strichen

                    if (textX + size.Width >= 0 && textX <= width)
                    {
                        g.DrawString(label, font, textBrush, textX, textY);
                    }
                }
            }
            // dB-Skala links im Wellenform-Bereich
            if (ShowDbScale)
            {
                DrawDbScale(g, width, height);
            }

            // Bearbeitungs-Auswahl (Highlight) – z.B. Detail-Selection im Overview
            if (_highlightStartSample.HasValue &&
                _highlightEndSample.HasValue &&
                _highlightEndSample.Value > _highlightStartSample.Value)
            {
                int hiStart = _highlightStartSample.Value;
                int hiEnd = _highlightEndSample.Value;

                int windowStart = viewStart;
                int windowEnd = viewStart + viewCount;

                int drawStart = Math.Max(hiStart, windowStart);
                int drawEnd = Math.Min(hiEnd, windowEnd);

                if (drawEnd > drawStart)
                {
                    long num1 = (long)(drawStart - viewStart) * width;
                    long num2 = (long)(drawEnd - viewStart) * width;

                    int x1 = (int)(num1 / viewCount);
                    int x2 = (int)(num2 / viewCount);

                    if (x1 < 0) x1 = 0;
                    if (x1 > width) x1 = width;
                    if (x2 < 0) x2 = 0;
                    if (x2 > width) x2 = width;

                    if (x2 < x1)
                    {
                        int tmp = x1;
                        x1 = x2;
                        x2 = tmp;
                    }

                    if (ShowDbScale)
                    {
                        if (x1 < DB_SCALE_MARGIN) x1 = DB_SCALE_MARGIN;
                        if (x2 < DB_SCALE_MARGIN) x2 = DB_SCALE_MARGIN;
                    }

                    int selTop = RULER_HEIGHT;
                    int selHeight = height - RULER_HEIGHT;
                    if (selHeight < 0) selHeight = 0;

                    // z.B. halbtransparente Version der Playhead-Farbe für das Highlight
                    using (var brush = new SolidBrush(Color.FromArgb(80,
                        _theme.PlayheadColor.R,
                        _theme.PlayheadColor.G,
                        _theme.PlayheadColor.B)))
                    {
                        g.FillRectangle(brush, x1, selTop, x2 - x1, selHeight);
                    }
                }
            }

            // Auswahl zeichnen (Overlay)
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
                    // robustes Mapping mit long
                    long num1 = (long)(drawStart - viewStart) * width;
                    long num2 = (long)(drawEnd - viewStart) * width;

                    int x1 = (int)(num1 / viewCount);
                    int x2 = (int)(num2 / viewCount);

                    if (x1 < 0) x1 = 0;
                    if (x1 > width) x1 = width;
                    if (x2 < 0) x2 = 0;
                    if (x2 > width) x2 = width;

                    if (x2 < x1)
                    {
                        int tmp = x1;
                        x1 = x2;
                        x2 = tmp;
                    }

                    if (ShowDbScale)
                    {
                        if (x1 < DB_SCALE_MARGIN) x1 = DB_SCALE_MARGIN;
                        if (x2 < DB_SCALE_MARGIN) x2 = DB_SCALE_MARGIN;
                    }

                    using var brush = new SolidBrush(_theme.SelectionFillColor);
                    int selTop = RULER_HEIGHT;
                    int selHeight = height - RULER_HEIGHT;
                    if (selHeight < 0) selHeight = 0;
                    g.FillRectangle(brush, x1, selTop, x2 - x1, selHeight);

                    using var edgePen = new Pen(_theme.SelectionEdgeColor, 2);
                    g.DrawLine(edgePen, x1, selTop, x1, height);
                    g.DrawLine(edgePen, x2, selTop, x2, height);


                }
            }


            // Playhead (rot) – overflow-safe
            if (totalSamples > 0)
            {
                int windowStart = viewStart;
                int windowEnd = viewStart + viewCount;

                if (_playbackSample >= windowStart && _playbackSample < windowEnd)
                {
                    int local = _playbackSample - viewStart;
                    if (local < 0) local = 0;
                    if (local > viewCount) local = viewCount;

                    long num = (long)local * (width - 1);
                    int xPos = (int)(num / viewCount);

                    if (xPos < 0) xPos = 0;
                    if (xPos >= width) xPos = width - 1;

                    if (ShowDbScale && xPos < DB_SCALE_MARGIN)
                        xPos = DB_SCALE_MARGIN;

                    using var pen = new Pen(_theme.PlayheadColor, 1);

                    g.DrawLine(pen, xPos, 0, xPos, height);
                }
            }

        }
        private Color GetRulerBackColor()
        {
            // leicht aufgehellte Hintergrundfarbe
            var c = _theme.Background;
            int r = Math.Min(255, c.R + 15);
            int g = Math.Min(255, c.G + 15);
            int b = Math.Min(255, c.B + 15);
            return Color.FromArgb(r, g, b);
        }
        private static Color DarkenColor(Color c, float factor)
        {
            // factor 0..1: 1 = unverändert, 0.5 = 50 % dunkler
            if (factor < 0f) factor = 0f;
            if (factor > 1f) factor = 1f;

            int r = (int)(c.R * factor);
            int g = (int)(c.G * factor);
            int b = (int)(c.B * factor);

            if (r < 0) r = 0; if (r > 255) r = 255;
            if (g < 0) g = 0; if (g > 255) g = 255;
            if (b < 0) b = 0; if (b > 255) b = 255;

            return Color.FromArgb(c.A, r, g, b);
        }


        private void MarkPeaksDirty() => _peaksDirty = true;
        private void MarkBitmapDirty() => _bitmapDirty = true;

        private void RebuildPeaks(float[] samples, int totalSamples, int viewStart, int viewCount, int width)
        {
            if (width <= 0 || viewCount <= 0 || totalSamples <= 0)
            {
                _cachedPeaks = Array.Empty<Peak>();
                _peaksDirty = false;
                return;
            }

            if (_cachedPeaks == null || _cachedPeaks.Length != width)
                _cachedPeaks = new Peak[width];

            for (int x = 0; x < width; x++)
            {
                int localStart = (int)((long)x * viewCount / width);
                int localEnd = (int)((long)(x + 1) * viewCount / width);


                if (localEnd <= localStart) localEnd = localStart + 1;
                if (localStart >= viewCount)
                {
                    _cachedPeaks[x].Min = 0f;
                    _cachedPeaks[x].Max = 0f;
                    continue;
                }
                if (localEnd > viewCount) localEnd = viewCount;

                int startSample = viewStart + localStart;
                int endSample = viewStart + localEnd;

                if (startSample >= totalSamples)
                {
                    _cachedPeaks[x].Min = 0f;
                    _cachedPeaks[x].Max = 0f;
                    continue;
                }

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

                _cachedPeaks[x].Min = min;
                _cachedPeaks[x].Max = max;
            }

            _peaksDirty = false;
        }

        private void RebuildWaveformBitmap(int height)
        {
            int width = _cachedWidth;
            if (width <= 0 || height <= 0 || _cachedPeaks == null || _cachedPeaks.Length != width)
            {
                _waveformBitmap?.Dispose();
                _waveformBitmap = null;
                _bitmapDirty = false;
                return;
            }

            _waveformBitmap?.Dispose();
            _waveformBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            using var g = Graphics.FromImage(_waveformBitmap);
            g.Clear(_theme.Background);


            float midY = height / 2f;
            float scaleY = GetAmplitudeScaleY(height);

            using var penWave = new Pen(_theme.WaveColor, 1);


            for (int x = 0; x < width; x++)
            {
                var p = _cachedPeaks[x];
                int y1 = (int)(midY - p.Max * scaleY);
                int y2 = (int)(midY - p.Min * scaleY);

                g.DrawLine(penWave, x, y1, x, y2);
            }

            // 0-Linie
            using var zeroPen = new Pen(_theme.ZeroLineColor, 1);
            g.DrawLine(zeroPen, 0, (int)midY, width, (int)midY);


            _bitmapDirty = false;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            MarkPeaksDirty();
            MarkBitmapDirty();
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

            int viewStart = _visibleStartSample;
            int viewCount = _visibleSampleCount > 0
                ? _visibleSampleCount
                : Math.Max(totalSamples, 1);


            if (viewCount <= 0)
                return 0;


            x = Math.Max(0, Math.Min(width - 1, x));

            // ✅ hier war vorher: int localIndex = x * viewCount / width;
            long localIndexLong = (long)x * viewCount / width;
            int localIndex = (int)localIndexLong;

            int sampleIndex = viewStart + localIndex;

            // Links hart auf 0 klemmen ...
            if (sampleIndex < 0) sampleIndex = 0;

            // ... rechts aber EINEN Schritt über das Ende erlauben:
            // sampleIndex == totalSamples bedeutet: "Locator steht im Air-Bereich
            // direkt hinter dem letzten Sample" -> ideal als exklusives End-Index.
            if (sampleIndex > totalSamples) sampleIndex = totalSamples;

            return sampleIndex;
        }


        private int SampleToX(int sampleIndex)
        {
            int width = ClientSize.Width;
            int totalSamples = _samples?.Length ?? 0;

            if (width <= 1 || totalSamples == 0)
                return 0;

            int viewStart = _visibleStartSample;
            int viewCount = _visibleSampleCount > 0
                ? _visibleSampleCount
                : Math.Max(totalSamples, 1);


            if (viewCount <= 0)
                return 0;

            int local = sampleIndex - viewStart;
            if (local < 0) local = 0;
            if (local > viewCount) local = viewCount;

            long xLong = (long)local * (width - 1) / viewCount;
            int x = (int)xLong;

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
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if (_samples == null || _samples.Length == 0)
                return;

            int totalSamples = _samples.Length;
            int width = ClientSize.Width;
            if (width <= 0 || totalSamples <= 0)
                return;

            int viewStart = Math.Max(0, Math.Min(_visibleStartSample, totalSamples));
            int viewCount = _visibleSampleCount > 0
                ? Math.Min(_visibleSampleCount, totalSamples - viewStart)
                : (totalSamples - viewStart);

            if (viewCount <= 0)
                return;

            bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;

            // ===== CTRL + Mausrad: ZOOM =====
            if (ctrl)
            {
                int mouseSample = XToSampleIndex(e.X);

                double factor = e.Delta > 0 ? 0.8 : 1.25; // rein / raus
                int newViewCount = (int)Math.Round(viewCount * factor);

                const int MinSamples = 128;
                if (newViewCount < MinSamples) newViewCount = MinSamples;
                if (newViewCount > totalSamples) newViewCount = totalSamples;

                if (newViewCount == viewCount)
                    return;

                double rel = (width > 1) ? (double)e.X / (width - 1) : 0.5;
                int newStart = mouseSample - (int)Math.Round(newViewCount * rel);

                if (newStart < 0) newStart = 0;
                int maxStart = Math.Max(0, totalSamples - newViewCount);
                if (newStart > maxStart) newStart = maxStart;

                _visibleStartSample = newStart;
                _visibleSampleCount = newViewCount;

                MarkPeaksDirty();
                MarkBitmapDirty();
                Invalidate();

                VisibleRangeChanged?.Invoke(_visibleStartSample,
                                            _visibleStartSample + _visibleSampleCount);
                return;
            }

            // ===== Nur Scrollen ohne Ctrl =====
            if (!AllowHorizontalScroll)
                return;

            // ~10 % des aktuellen Fensters pro Wheel-Tick
            int scrollDeltaSamples = Math.Max(1, viewCount / 10);

            int newScrollStart = viewStart;

            if (e.Delta > 0)
            {
                // Rad nach oben -> nach links
                newScrollStart = Math.Max(0, viewStart - scrollDeltaSamples);
            }
            else if (e.Delta < 0)
            {
                // Rad nach unten -> nach rechts
                int maxStart = Math.Max(0, totalSamples - viewCount);
                newScrollStart = Math.Min(maxStart, viewStart + scrollDeltaSamples);
            }

            if (newScrollStart != viewStart)
            {
                _visibleStartSample = newScrollStart;

                if (_visibleSampleCount <= 0)
                    _visibleSampleCount = viewCount;

                MarkPeaksDirty();
                MarkBitmapDirty();
                Invalidate();

                VisibleRangeChanged?.Invoke(_visibleStartSample,
                                            _visibleStartSample + _visibleSampleCount);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (_samples == null || _samples.Length == 0) return;

            int totalSamples = _samples.Length;

            // nur linke + mittlere Maustaste unterstützen wir hier
            if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Middle)
                return;

            // 1) Klick in die Zeit-Leiste oben -> nur Playhead setzen, Selektion bleibt
            if (e.Button == MouseButtons.Left && e.Y <= RULER_HEIGHT)
            {
                int idx = XToSampleIndex(e.X);
                PlaybackSample = idx;
                PlaybackPositionChangedByClick?.Invoke(idx);
                Invalidate();
                return; // WICHTIG: nicht in die Auswahl-Logik fallen
            }

            // 2) Klick in die eigentliche Wellenform -> Selektion / Resize
            _isMouseDown = true;

            // Klick-Sample im aktuellen Sichtfenster
            int clickSample = XToSampleIndex(e.X);

            // Mittlere Maustaste + Klick IN der Auswahl -> Auswahl verschieben
            if (e.Button == MouseButtons.Middle && HasSelection)
            {
                var sel = GetNormalizedSelection(totalSamples);
                int selStart = sel.start;
                int selEnd = sel.end;

                if (clickSample >= selStart && clickSample <= selEnd)
                {
                    _dragMode = DragMode.MoveSelection;
                    _moveSelectionOffsetSamples = clickSample - selStart;
                    _moveSelectionLengthSamples = selEnd - selStart;
                    Cursor = Cursors.SizeWE;
                    return;
                }
            }

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
                    Cursor = Cursors.SizeWE;
                    return;
                }
                if (nearRight && !nearLeft)
                {
                    _dragMode = DragMode.ResizeRight;
                    Cursor = Cursors.SizeWE;
                    return;
                }
                if (nearLeft && nearRight)
                {
                    // extrem schmale Auswahl: nimm z.B. rechts
                    _dragMode = DragMode.ResizeRight;
                    Cursor = Cursors.SizeWE;
                    return;
                }
            }

            // Sonst: neue Selektion beginnen
            _dragMode = DragMode.NewSelection;

            _selectionStartSample = clickSample;
            _selectionEndSample = clickSample;

            PlaybackSample = clickSample;
            PlaybackPositionChangedByClick?.Invoke(clickSample);

            Invalidate();
        }

        private void AutoScrollWhileDragging(MouseEventArgs e, int totalSamples)
        {
            if (!AllowHorizontalScroll)
                return;

            int width = ClientSize.Width;
            if (width <= 0 || totalSamples <= 0)
                return;

            // Sichtfenster berechnen
            // Darf jetzt auch VOR 0 und NACH dem Clip liegen.
            // Alles außerhalb [0, totalSamples) wird als Stille gezeichnet.
            int viewStart = _visibleStartSample;
            int viewCount = _visibleSampleCount > 0
                ? _visibleSampleCount
                : Math.Max(totalSamples, 1);


            if (viewCount <= 0)
                return;

            const int edgePixels = 16;
            int scrollDeltaSamples = Math.Max(1, viewCount / 50); // ~2 % des Fensters

            bool changed = false;

            // --- Links: bis maximal -_extraScrollSamples ---
            if (e.X <= edgePixels)
            {
                int minStart = -_extraScrollSamples;
                int newStart = viewStart - scrollDeltaSamples;
                if (newStart < minStart) newStart = minStart;
                _visibleStartSample = newStart;

                if (_visibleSampleCount <= 0)
                    _visibleSampleCount = viewCount;

                changed = true;
            }
            // --- Rechts: bis maximal ClipEnde + _extraScrollSamples ---
            else if (e.X >= width - edgePixels)
            {
                int minStart = -_extraScrollSamples;
                int maxStart = totalSamples + _extraScrollSamples - viewCount;
                if (maxStart < minStart) maxStart = minStart;

                int newStart = viewStart + scrollDeltaSamples;
                if (newStart > maxStart) newStart = maxStart;
                _visibleStartSample = newStart;

                if (_visibleSampleCount <= 0)
                    _visibleSampleCount = viewCount;

                changed = true;
            }

            if (changed)
            {
                MarkPeaksDirty();
                MarkBitmapDirty();
                Invalidate();
            }

        }


        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // In der Zeit-Leiste: keine Resize- / Auswahl-Logik
            if (e.Y <= RULER_HEIGHT)
            {
                if (!_isMouseDown)
                {
                    Cursor = Cursors.Default;
                    _isHoveringEdge = false;
                }
                return;
            }

            if (_samples == null || _samples.Length == 0)
            {
                Cursor = Cursors.Default;
                _isHoveringEdge = false;
                return;
            }

            int totalSamples = _samples.Length;

            // HOVER-LOGIK FÜR KANTEN (wenn keine Maustaste gedrückt)
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
                    Cursor = Cursors.SizeWE;
                    _isHoveringEdge = true;
                }
                else if (_isHoveringEdge)
                {
                    Cursor = Cursors.Default;
                    _isHoveringEdge = false;
                }
            }

            // DRAGGEN (nur wenn Maus gedrückt)
            if (!_isMouseDown || _dragMode == DragMode.None)
                return;

            // Auto-Scroll, wenn wir beim Draggen an den Rand fahren
            AutoScrollWhileDragging(e, totalSamples);

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
                case DragMode.MoveSelection:
                    {
                        int length = _moveSelectionLengthSamples;
                        if (length <= 0)
                            break;

                        int total = totalSamples;

                        int newStart = idx - _moveSelectionOffsetSamples;

                        // innerhalb der Datei einklemmen
                        if (newStart < 0) newStart = 0;
                        if (newStart + length > total) newStart = total - length;
                        if (newStart < 0) newStart = 0;

                        _selectionStartSample = newStart;
                        _selectionEndSample = newStart + length;
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
                var sel = GetNormalizedSelection(_samples.Length);
                _selectionStartSample = sel.start;
                _selectionEndSample = sel.end;

                if (sel.start != sel.end)
                {
                    // gültige Selektion -> Event feuern
                    SelectionChanged?.Invoke(sel.start, sel.end);
                }
                else
                {
                    // Selektion wurde "zusammengeklappt" -> löschen und ebenfalls melden
                    ClearSelection();
                    SelectionChanged?.Invoke(0, 0);
                }
            }


            _dragMode = DragMode.None;
            Cursor = Cursors.Default;
            _isHoveringEdge = false;
        }
        //protected override void OnMouseWheel(MouseEventArgs e)
        //{
        //    base.OnMouseWheel(e);

        //    if (!AllowHorizontalScroll)
        //        return;

        //    if (_samples == null || _samples.Length == 0)
        //        return;

        //    int totalSamples = _samples.Length;
        //    int width = ClientSize.Width;
        //    if (width <= 0 || totalSamples <= 0)
        //        return;

        //    int viewStart = _visibleStartSample;
        //    int viewCount = _visibleSampleCount > 0
        //        ? _visibleSampleCount
        //        : Math.Max(totalSamples, 1);



        //    if (viewCount <= 0)
        //        return;

        //    // ~10 % des aktuellen Fensters pro Wheel-Tick
        //    int scrollDeltaSamples = Math.Max(1, viewCount / 10);

        //    int newStart = viewStart;
        //    int minStart = -_extraScrollSamples;
        //    int maxStart = totalSamples + _extraScrollSamples - viewCount;
        //    if (maxStart < minStart) maxStart = minStart;

        //    if (e.Delta > 0)
        //    {
        //        // Rad nach oben -> nach links
        //        newStart = viewStart - scrollDeltaSamples;
        //        if (newStart < minStart) newStart = minStart;
        //    }
        //    else if (e.Delta < 0)
        //    {
        //        // Rad nach unten -> nach rechts
        //        newStart = viewStart + scrollDeltaSamples;
        //        if (newStart > maxStart) newStart = maxStart;
        //    }

        //    if (newStart != viewStart)
        //    {
        //        _visibleStartSample = newStart;

        //        if (_visibleSampleCount <= 0)
        //            _visibleSampleCount = viewCount;

        //        MarkPeaksDirty();
        //        MarkBitmapDirty();
        //        Invalidate();
        //    }

        //}

        private void DrawEmptyTimeline(Graphics g, int width, int height)
        {
            // Gesamt-Hintergrund
            g.Clear(BackColor);

            int contentTop = RULER_HEIGHT;
            int contentHeight = height - contentTop;
            if (contentHeight < 0) contentHeight = 0;

            // Wellenform-Bereich einfärben
            using (var waveBg = new SolidBrush(_theme.Background))
            {
                g.FillRectangle(waveBg, 0, contentTop, width, contentHeight);
            }

            // horizontale Nulllinie in der Mitte
            if (contentHeight > 0)
            {
                float midY = contentTop + contentHeight / 2f;
                using var zeroPen = new Pen(_theme.ZeroLineColor, 1);
                g.DrawLine(zeroPen, 0, midY, width, midY);
            }

            // Zeit-Leiste oben
            using (var rulerBrush = new SolidBrush(GetRulerBackColor()))
            {
                g.FillRectangle(rulerBrush, 0, 0, width, RULER_HEIGHT);
            }

            // 0–5 Sekunden Achse
            double visibleSeconds = 5.0;
            double step = GetNiceTimeStep(visibleSeconds);
            int rulerHeight = 12;

            using var tickPen = new Pen(_theme.ZeroLineColor, 1);
            using var textBrush = new SolidBrush(_theme.TextColor);
            var font = this.Font;

            for (double t = 0.0; t <= visibleSeconds + 1e-6; t += step)
            {
                double rel = t / visibleSeconds; // 0..1
                float x = (float)(rel * width);
                if (x < 0 || x > width) continue;

                // Tick
                g.DrawLine(tickPen, x, 0, x, rulerHeight);

                // Beschriftung
                string label = (t < 10.0) ? $"{t:0.0}s" : $"{t:0}s";
                var size = g.MeasureString(label, font);
                float textX = x - size.Width / 2f;
                float textY = rulerHeight;

                if (textX + size.Width >= 0 && textX <= width)
                    g.DrawString(label, font, textBrush, textX, textY);
            }

            // Vertikaler „Meridian“ in der Mitte
            if (contentHeight > 0)
            {
                float centerX = width / 2f;
                using var meridianPen = new Pen(_theme.ZeroLineColor, 1)
                {
                    DashStyle = DashStyle.Dash
                };
                g.DrawLine(meridianPen, centerX, contentTop, centerX, height);
            }

            // Kleiner Hinweistext
            const string msg = "Keine Audiodatei geladen";
            var msgSize = g.MeasureString(msg, Font);
            g.DrawString(msg, Font, textBrush,
                (width - msgSize.Width) / 2f,
                contentTop + contentHeight / 2f - msgSize.Height / 2f);
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
            // ✅ reset "last delete" info
            LastDeletedStartSample = 0;
            LastDeletedSampleCount = 0;
            if (_samples == null || _samples.Length == 0 || !HasSelection)
                return;

            int totalSamples = _samples.Length;
            var (start, end) = GetNormalizedSelection(totalSamples);
            if (end <= start)
                return;

            int cutLength = end - start;
            // ✅ remember what got removed (important for video offset)
            LastDeletedStartSample = start;
            LastDeletedSampleCount = cutLength;

            int newLength = totalSamples - cutLength;
            if (newLength <= 0)
            {
                _samples = Array.Empty<float>();
                ClearSelection();
                PlaybackSample = 0;

                MarkPeaksDirty();
                MarkBitmapDirty();
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

            MarkPeaksDirty();
            MarkBitmapDirty();
            Invalidate();
        }

        private void DrawDbScale(Graphics g, int width, int height)
        {
            int contentTop = RULER_HEIGHT;
            int contentHeight = height - contentTop;
            if (contentHeight <= 0) return;

            int margin = DB_SCALE_MARGIN; // statt const int margin = 40;

            float midY = contentTop + contentHeight / 2f;
            float scaleY = GetAmplitudeScaleY(contentHeight);



            // Hintergrundstreifen für die Skala
            using (var bgBrush = new SolidBrush(Color.FromArgb(220, _theme.Background)))
            {
                g.FillRectangle(bgBrush, 0, contentTop, margin, contentHeight);
            }

            using var linePen = new Pen(_theme.ZeroLineColor, 1);
            using var textBrush = new SolidBrush(_theme.TextColor);
            var font = this.Font;

            // ein paar typische dB-Werte
            int[] dbTicks = new[] { 0, -3, -6, -9, -12, -18, -24 };

            foreach (int db in dbTicks)
            {
                double amp = Math.Pow(10.0, db / 20.0); // 0 dB -> 1.0, -6 dB -> ~0.5
                float dy = (float)(amp * scaleY);
                float y = midY - dy; // obere Hälfte

                if (y < contentTop || y > contentTop + contentHeight) continue;

                // Tick-Linie
                g.DrawLine(linePen, 0, y, margin - 6, y);

                // Label
                //string label = db == 0 ? "0 dB" : $"{db} dB";
                string label = db == 0 ? "0 dB" : $"{db}";
                var size = g.MeasureString(label, font);
                float textX = 2;
                float textY = y - size.Height / 2f;

                g.DrawString(label, font, textBrush, textX, textY);
            }

            // Null-Linie (zur Sicherheit nochmal, passt zu deiner Wave-Null)
            g.DrawLine(linePen, 0, midY, margin - 2, midY);
        }

    }
}

