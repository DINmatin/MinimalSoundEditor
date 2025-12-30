using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using static MinimalSoundEditor.MainForm; // ThemeDefinition, ThemeMode, GetThemeDefaultsFilePath, SaveThemeDefaultsIfMissing

namespace MinimalSoundEditor
{
    /// <summary>
    /// Theme-Editor:
    /// - EIN ComboBox mit: Light, Dark, und benannten Themes aus themes.json ("Modes").
    /// - Light & Dark sind Default-Themes.
    /// - Copy… legt ein neues benanntes Theme in der JSON an.
    /// - Änderungen wirken sofort als Live-Preview im MainForm über ThemeChanged.
    /// </summary>
    public class ThemeSettingsForm : Form
    {
        // === JSON-Datenstrukturen =======================================

        private class ThemeColorSetModel
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

        /// <summary>
        /// JSON-Struktur:
        /// {
        ///   "Light": { ... },
        ///   "Dark":  { ... },
        ///   "Modes": {
        ///      "Warm":   { ... },
        ///      "Neon":   { ... },
        ///      "Retro":  { ... }
        ///   }
        /// }
        ///
        /// MainForm liest weiterhin nur Light/Dark über die bestehende ThemeDefaults-Klasse.
        /// "Modes" wird beim Deserialisieren dort einfach ignoriert (unbekannte Property).
        /// </summary>
        private class ThemeJsonModel
        {
            public ThemeColorSetModel Light { get; set; } = new ThemeColorSetModel();
            public ThemeColorSetModel Dark { get; set; } = new ThemeColorSetModel();

            public Dictionary<string, ThemeColorSetModel> Modes { get; set; } =
                new Dictionary<string, ThemeColorSetModel>(StringComparer.OrdinalIgnoreCase);
        }

        // === Felder ======================================================

        private readonly ThemeDefinition _lightTheme;
        private readonly ThemeDefinition _darkTheme;

        private ThemeMode _currentMode;           // Light oder Dark
        private ThemeJsonModel _jsonModel = new ThemeJsonModel();

        // Name aus dem DropDown: "Light", "Dark" oder benanntes Theme
        private string _currentSelectionName;

        private bool _isUpdatingUi;

        private const int SelectionFillAlpha = 110; // transparenter Selection-Background

        // UI
        private ComboBox _comboThemes;
        private Button _btnCopyTheme;
        private Button _btnDeleteTheme;
        private Button _btnOpenJson;
        private Button _btnDefaults;

        private Button _btnFormBack;
        private Button _btnWaveBack;
        private Button _btnWave;
        private Button _btnSelection;
        private Button _btnSelectionEdge;
        private Button _btnZeroLine;
        private Button _btnText;
        private Button _btnPlayhead;
        private Button _btnButtonBack;
        private Button _btnButtonFore;
        private Button _btnButtonBorder;

        // === Öffentliche API ============================================

        public ThemeMode SelectedMode => _currentMode;

        /// <summary>
        /// Wird vom MainForm abonniert, um Live-Preview zu machen.
        /// </summary>
        public event Action ThemeChanged;

        // === Konstruktor =================================================

        public ThemeSettingsForm(ThemeDefinition lightTheme, ThemeDefinition darkTheme, ThemeMode currentMode)
        {
            _lightTheme = lightTheme;
            _darkTheme = darkTheme;
            _currentMode = currentMode;

            Text = "Themes";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 440);

            BuildUi();

            // Datei mit Light/Dark Defaults anlegen, falls nicht vorhanden
            SaveThemeDefaultsIfMissing();

            LoadJsonModel();
            PopulateThemeCombo();

            _currentSelectionName = _currentMode == ThemeMode.Light ? "Light" : "Dark";
            _comboThemes.SelectedItem = _currentSelectionName;

            LoadThemeToUi(GetActiveTheme());
        }

        // === UI-Aufbau ===================================================

        private void BuildUi()
        {
            int margin = 12;

            var lblThemes = new Label
            {
                Left = margin,
                Top = margin,
                Text = "Theme",
                AutoSize = true
            };

            _comboThemes = new ComboBox
            {
                Left = margin,
                Top = lblThemes.Bottom + 2,
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _comboThemes.SelectedIndexChanged += OnThemeSelectionChanged;

            _btnCopyTheme = new Button
            {
                Left = _comboThemes.Right + 8,
                Top = _comboThemes.Top,
                Width = 80,
                Text = "Copy…"
            };
            _btnCopyTheme.Click += OnCopyThemeClick;

            _btnDeleteTheme = new Button
            {
                Left = _btnCopyTheme.Right + 4,
                Top = _comboThemes.Top,
                Width = 60,
                Text = "Del"
            };
            _btnDeleteTheme.Click += OnDeleteThemeClick;

            int rowY = _comboThemes.Bottom + 20;
            int rowDY = 30;

            _btnFormBack = CreateColorRow("Form background", rowY, OnFormBackClick);
            rowY += rowDY;
            _btnWaveBack = CreateColorRow("Wave background", rowY, OnWaveBackClick);
            rowY += rowDY;
            _btnWave = CreateColorRow("Wave color", rowY, OnWaveClick);
            rowY += rowDY;
            _btnSelection = CreateColorRow("Selection", rowY, OnSelectionClick);
            rowY += rowDY;
            _btnSelectionEdge = CreateColorRow("Selection edge", rowY, OnSelectionEdgeClick);
            rowY += rowDY;
            _btnZeroLine = CreateColorRow("Zero line", rowY, OnZeroLineClick);
            rowY += rowDY;
            _btnPlayhead = CreateColorRow("Playhead", rowY, OnPlayheadClick);
            rowY += rowDY;
            _btnText = CreateColorRow("Text", rowY, OnTextClick);
            rowY += rowDY;
            _btnButtonBack = CreateColorRow("Button background", rowY, OnButtonBackClick);
            rowY += rowDY;
            _btnButtonFore = CreateColorRow("Button text", rowY, OnButtonForeClick);
            rowY += rowDY;
            _btnButtonBorder = CreateColorRow("Button border", rowY, OnButtonBorderClick);
            rowY += rowDY;

            _btnDefaults = new Button
            {
                Text = "Reset Light/Dark",
                Left = margin,
                Top = rowY + 10,
                Width = 120
            };
            _btnDefaults.Click += (s, e) => ResetCurrentThemeToDefaults();

            _btnOpenJson = new Button
            {
                Text = "themes.json",
                Left = _btnDefaults.Right + 8,
                Top = rowY + 10,
                Width = 100
            };
            _btnOpenJson.Click += (s, e) => OpenThemeJson();

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = ClientSize.Width - 180,
                Top = ClientSize.Height - 40,
                Width = 80
            };
            btnOk.Click += (s, e) =>
            {
                // Beim OK: Light/Dark + Modes in JSON schreiben
                SaveJsonModel();
            };

            var btnCancel = new Button
            {
                Text = "Abbrechen",
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Left = ClientSize.Width - 90,
                Top = ClientSize.Height - 40,
                Width = 80
            };

            Controls.AddRange(new Control[]
            {
                lblThemes, _comboThemes,
                _btnCopyTheme, _btnDeleteTheme,
                _btnFormBack, _btnWaveBack, _btnWave,
                _btnSelection, _btnSelectionEdge, _btnZeroLine,
                _btnPlayhead, _btnText,
                _btnButtonBack, _btnButtonFore, _btnButtonBorder,
                _btnDefaults, _btnOpenJson,
                btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private Button CreateColorRow(string label, int top, EventHandler onClick)
        {
            int leftLabel = 12;
            int leftButton = 200;

            var lbl = new Label
            {
                Left = leftLabel,
                Top = top + 4,
                Text = label,
                AutoSize = true
            };

            var btn = new Button
            {
                Left = leftButton,
                Top = top,
                Width = 60,
                Height = 20
            };
            btn.Click += onClick;

            Controls.Add(lbl);
            Controls.Add(btn);
            return btn;
        }

        private ThemeDefinition GetActiveTheme()
        {
            return _currentMode == ThemeMode.Light ? _lightTheme : _darkTheme;
        }

        // === JSON laden / speichern =====================================

        private void LoadJsonModel()
        {
            string path = GetThemeDefaultsFilePath();
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var model = JsonSerializer.Deserialize<ThemeJsonModel>(json);
                    if (model != null)
                        _jsonModel = model;
                }
                catch
                {
                    _jsonModel = new ThemeJsonModel();
                }
            }

            if (_jsonModel.Modes == null)
                _jsonModel.Modes = new Dictionary<string, ThemeColorSetModel>(StringComparer.OrdinalIgnoreCase);

            if (_jsonModel.Light == null)
                _jsonModel.Light = ThemeToModel(_lightTheme);
            if (_jsonModel.Dark == null)
                _jsonModel.Dark = ThemeToModel(_darkTheme);
        }

        private void SaveJsonModel()
        {
            string path = GetThemeDefaultsFilePath();

            // Light/Dark aus den aktuellen ThemeDefinitions übernehmen
            _jsonModel.Light = ThemeToModel(_lightTheme);
            _jsonModel.Dark = ThemeToModel(_darkTheme);

            var json = JsonSerializer.Serialize(
                _jsonModel,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(path, json);
        }

        private void PopulateThemeCombo()
        {
            _isUpdatingUi = true;
            _comboThemes.Items.Clear();

            _comboThemes.Items.Add("Light");
            _comboThemes.Items.Add("Dark");

            foreach (var key in _jsonModel.Modes.Keys)
            {
                _comboThemes.Items.Add(key);
            }

            _isUpdatingUi = false;
        }

        // === Theme-Auswahl ==============================================

        private void OnThemeSelectionChanged(object sender, EventArgs e)
        {
            if (_isUpdatingUi) return;

            if (!(_comboThemes.SelectedItem is string name) || string.IsNullOrWhiteSpace(name))
                return;

            _currentSelectionName = name;

            if (string.Equals(name, "Light", StringComparison.OrdinalIgnoreCase))
            {
                _currentMode = ThemeMode.Light;
                LoadThemeToUi(_lightTheme);
                ThemeChanged?.Invoke();
                return;
            }

            if (string.Equals(name, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                _currentMode = ThemeMode.Dark;
                LoadThemeToUi(_darkTheme);
                ThemeChanged?.Invoke();
                return;
            }

            // benanntes Theme -> als Preset auf das aktuelle Basis-Theme anwenden
            if (_jsonModel.Modes.TryGetValue(name, out var set))
            {
                var t = GetActiveTheme();
                ApplyModelToTheme(set, t);
                LoadThemeToUi(t);
                ThemeChanged?.Invoke();
            }
        }

        private void OnCopyThemeClick(object sender, EventArgs e)
        {
            var t = GetActiveTheme();
            var model = ThemeToModel(t);

            string baseName = _currentSelectionName;
            if (string.IsNullOrWhiteSpace(baseName))
                baseName = _currentMode == ThemeMode.Light ? "LightVariant" : "DarkVariant";

            using (var prompt = new NamePromptForm("Neues Theme", "Name für das neue Theme:", baseName))
            {
                if (prompt.ShowDialog(this) != DialogResult.OK)
                    return;

                string newName = prompt.ResultName?.Trim();
                if (string.IsNullOrEmpty(newName))
                    return;

                if (_jsonModel.Modes.ContainsKey(newName))
                {
                    MessageBox.Show(this,
                        $"Es gibt bereits ein Theme mit dem Namen \"{newName}\".",
                        "Name bereits vorhanden",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                _jsonModel.Modes[newName] = model;

                PopulateThemeCombo();

                _comboThemes.SelectedItem = newName;
            }
        }

        private void OnDeleteThemeClick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentSelectionName) ||
                string.Equals(_currentSelectionName, "Light", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(_currentSelectionName, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this,
                    "Light und Dark sind Default-Themes und können nicht gelöscht werden.\n" +
                    "Wähle ein benanntes Theme zum Löschen.",
                    "Nichts zum Löschen",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!_jsonModel.Modes.ContainsKey(_currentSelectionName))
                return;

            var result = MessageBox.Show(this,
                $"Theme \"{_currentSelectionName}\" wirklich löschen?",
                "Theme löschen",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            _jsonModel.Modes.Remove(_currentSelectionName);
            _currentSelectionName = null;

            PopulateThemeCombo();
            // zurück auf aktuelles Basis-Theme
            _comboThemes.SelectedItem = _currentMode == ThemeMode.Light ? "Light" : "Dark";
            LoadThemeToUi(GetActiveTheme());
            ThemeChanged?.Invoke();
        }

        // === Mapping ThemeDefinition <-> Model ===========================

        private ThemeColorSetModel ThemeToModel(ThemeDefinition t)
        {
            return new ThemeColorSetModel
            {
                FormBack = ColorToHex(t.FormBack),
                ButtonBack = ColorToHex(t.ButtonBack),
                ButtonFore = ColorToHex(t.ButtonFore),
                ButtonBorder = ColorToHex(t.ButtonBorder),

                WaveBackground = ColorToHex(t.Waveform.Background),
                WaveColor = ColorToHex(t.Waveform.WaveColor),
                WaveZeroLine = ColorToHex(t.Waveform.ZeroLineColor),
                WaveSelectionFill = ColorToHex(Color.FromArgb(255, t.Waveform.SelectionFillColor)),
                WaveSelectionEdge = ColorToHex(t.Waveform.SelectionEdgeColor),
                WavePlayhead = ColorToHex(t.Waveform.PlayheadColor),
                WaveText = ColorToHex(t.Waveform.TextColor)
            };
        }

        private void ApplyModelToTheme(ThemeColorSetModel m, ThemeDefinition t)
        {
            if (m == null || t == null) return;

            t.FormBack = HexToColor(m.FormBack, t.FormBack);
            t.ButtonBack = HexToColor(m.ButtonBack, t.ButtonBack);
            t.ButtonFore = HexToColor(m.ButtonFore, t.ButtonFore);
            t.ButtonBorder = HexToColor(m.ButtonBorder, t.ButtonBorder);

            t.Waveform.Background = HexToColor(m.WaveBackground, t.Waveform.Background);
            t.Waveform.WaveColor = HexToColor(m.WaveColor, t.Waveform.WaveColor);
            t.Waveform.ZeroLineColor = HexToColor(m.WaveZeroLine, t.Waveform.ZeroLineColor);

            var selBase = HexToColor(m.WaveSelectionFill, t.Waveform.SelectionFillColor);
            t.Waveform.SelectionFillColor = Color.FromArgb(SelectionFillAlpha, selBase);

            t.Waveform.SelectionEdgeColor = HexToColor(m.WaveSelectionEdge, t.Waveform.SelectionEdgeColor);
            t.Waveform.PlayheadColor = HexToColor(m.WavePlayhead, t.Waveform.PlayheadColor);
            t.Waveform.TextColor = HexToColor(m.WaveText, t.Waveform.TextColor);
        }

        // === UI -> ThemeDefinition ======================================

        private void LoadThemeToUi(ThemeDefinition t)
        {
            _isUpdatingUi = true;

            _btnFormBack.BackColor = t.FormBack;
            _btnWaveBack.BackColor = t.Waveform.Background;
            _btnWave.BackColor = t.Waveform.WaveColor;
            _btnSelection.BackColor = t.Waveform.SelectionFillColor;
            _btnSelectionEdge.BackColor = t.Waveform.SelectionEdgeColor;
            _btnZeroLine.BackColor = t.Waveform.ZeroLineColor;
            _btnPlayhead.BackColor = t.Waveform.PlayheadColor;
            _btnText.BackColor = t.Waveform.TextColor;
            _btnButtonBack.BackColor = t.ButtonBack;
            _btnButtonFore.BackColor = t.ButtonFore;
            _btnButtonBorder.BackColor = t.ButtonBorder;

            _isUpdatingUi = false;
        }

        private void PickColor(Button btn, Action<ThemeDefinition, Color> apply)
        {
            using (var dlg = new ColorDialog())
            {
                dlg.Color = btn.BackColor;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    var t = GetActiveTheme();
                    apply(t, dlg.Color);
                    btn.BackColor = dlg.Color;

                    // Live-Preview im MainForm
                    ThemeChanged?.Invoke();

                    // Snapshot im JSON aktualisieren
                    if (!string.IsNullOrWhiteSpace(_currentSelectionName))
                    {
                        if (string.Equals(_currentSelectionName, "Light", StringComparison.OrdinalIgnoreCase))
                            _jsonModel.Light = ThemeToModel(_lightTheme);
                        else if (string.Equals(_currentSelectionName, "Dark", StringComparison.OrdinalIgnoreCase))
                            _jsonModel.Dark = ThemeToModel(_darkTheme);
                        else if (_jsonModel.Modes.ContainsKey(_currentSelectionName))
                            _jsonModel.Modes[_currentSelectionName] = ThemeToModel(t);
                    }
                }
            }
        }

        private void OnFormBackClick(object sender, EventArgs e)
            => PickColor(_btnFormBack, (t, c) => t.FormBack = c);

        private void OnWaveBackClick(object sender, EventArgs e)
            => PickColor(_btnWaveBack, (t, c) => t.Waveform.Background = c);

        private void OnWaveClick(object sender, EventArgs e)
            => PickColor(_btnWave, (t, c) => t.Waveform.WaveColor = c);

        private void OnSelectionClick(object sender, EventArgs e)
            => PickColor(_btnSelection, (t, c) =>
            {
                t.Waveform.SelectionFillColor = Color.FromArgb(SelectionFillAlpha, c);
            });

        private void OnSelectionEdgeClick(object sender, EventArgs e)
            => PickColor(_btnSelectionEdge, (t, c) => t.Waveform.SelectionEdgeColor = c);

        private void OnZeroLineClick(object sender, EventArgs e)
            => PickColor(_btnZeroLine, (t, c) => t.Waveform.ZeroLineColor = c);

        private void OnPlayheadClick(object sender, EventArgs e)
            => PickColor(_btnPlayhead, (t, c) => t.Waveform.PlayheadColor = c);

        private void OnTextClick(object sender, EventArgs e)
            => PickColor(_btnText, (t, c) => t.Waveform.TextColor = c);

        private void OnButtonBackClick(object sender, EventArgs e)
            => PickColor(_btnButtonBack, (t, c) => t.ButtonBack = c);

        private void OnButtonForeClick(object sender, EventArgs e)
            => PickColor(_btnButtonFore, (t, c) => t.ButtonFore = c);

        private void OnButtonBorderClick(object sender, EventArgs e)
            => PickColor(_btnButtonBorder, (t, c) => t.ButtonBorder = c);

        private void ResetCurrentThemeToDefaults()
        {
            // nur Basis-Themes haben "Defaults"
            if (_currentMode == ThemeMode.Light)
                SetDefaultLightTheme(_lightTheme);
            else
                SetDefaultDarkTheme(_darkTheme);

            LoadThemeToUi(GetActiveTheme());
            ThemeChanged?.Invoke();

            if (string.Equals(_currentSelectionName, "Light", StringComparison.OrdinalIgnoreCase))
                _jsonModel.Light = ThemeToModel(_lightTheme);
            else if (string.Equals(_currentSelectionName, "Dark", StringComparison.OrdinalIgnoreCase))
                _jsonModel.Dark = ThemeToModel(_darkTheme);
        }

        private void OpenThemeJson()
        {
            try
            {
                // vor dem Öffnen sicherstellen, dass JSON den aktuellen Stand hat
                SaveJsonModel();

                string path = GetThemeDefaultsFilePath();
                if (!File.Exists(path))
                {
                    MessageBox.Show(this,
                        "themes.json existiert noch nicht.",
                        "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var proc = new System.Diagnostics.Process())
                {
                    proc.StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    };
                    proc.Start();
                }
            }
            catch
            {
                // egal – nur Komfortfunktion
            }
        }

        // === Color <-> Hex Utilities ====================================

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
                    byte r = Convert.ToByte(s.Substring(0, 2), 16);
                    byte g = Convert.ToByte(s.Substring(2, 2), 16);
                    byte b = Convert.ToByte(s.Substring(4, 2), 16);
                    return Color.FromArgb(255, r, g, b);
                }
            }
            catch
            {
                // ignore
            }

            return fallback;
        }

        // === kleiner Name-Dialog ========================================

        private class NamePromptForm : Form
        {
            private TextBox _textBox;
            public string ResultName => _textBox.Text;

            public NamePromptForm(string title, string label, string defaultValue)
            {
                Text = title;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ClientSize = new Size(320, 140);

                var lbl = new Label
                {
                    Left = 10,
                    Top = 10,
                    Text = label,
                    AutoSize = true
                };

                _textBox = new TextBox
                {
                    Left = 10,
                    Top = 35,
                    Width = ClientSize.Width - 20,
                    Text = defaultValue ?? ""
                };

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Left = ClientSize.Width - 170,
                    Top = 80,
                    Width = 70
                };

                var btnCancel = new Button
                {
                    Text = "Abbrechen",
                    DialogResult = DialogResult.Cancel,
                    Left = ClientSize.Width - 90,
                    Top = 80,
                    Width = 70
                };

                Controls.AddRange(new Control[] { lbl, _textBox, btnOk, btnCancel });

                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }
        }
    }
}
