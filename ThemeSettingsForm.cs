using System;
using System.Drawing;
using System.Windows.Forms;
using static MinimalSoundEditor.MainForm;   // für ThemeMode & ThemeDefinition

namespace MinimalSoundEditor
{
    public class ThemeSettingsForm : Form
    {
        private readonly ThemeDefinition _lightTheme;
        private readonly ThemeDefinition _darkTheme;
        private ThemeMode _mode;

        private ComboBox _comboMode;
        private Button _btnFormBack;
        private Button _btnWaveBack;
        private Button _btnWave;
        private Button _btnSelection;
        private Button _btnPlayhead;

        public ThemeMode SelectedMode => _mode;

        public ThemeSettingsForm(ThemeDefinition light, ThemeDefinition dark, ThemeMode currentMode)
        {
            _lightTheme = light;
            _darkTheme = dark;
            _mode = currentMode;

            Text = "Theme Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            Width = 380;
            Height = 260;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            BuildUi();
            LoadThemeToUi(GetActiveTheme());
        }

        private void BuildUi()
        {
            var lblMode = new Label
            {
                Text = "Mode:",
                Left = 10,
                Top = 15,
                AutoSize = true
            };

            _comboMode = new ComboBox
            {
                Left = 80,
                Top = 10,
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _comboMode.Items.Add("Light");
            _comboMode.Items.Add("Dark");
            _comboMode.SelectedIndex = _mode == ThemeMode.Light ? 0 : 1;
            _comboMode.SelectedIndexChanged += (s, e) =>
            {
                _mode = _comboMode.SelectedIndex == 0 ? ThemeMode.Light : ThemeMode.Dark;
                LoadThemeToUi(GetActiveTheme());
            };

            int rowY = 50;
            int rowDY = 30;

            _btnFormBack = CreateColorRow("Form background", rowY, OnFormBackClick);
            rowY += rowDY;
            _btnWaveBack = CreateColorRow("Wave background", rowY, OnWaveBackClick);
            rowY += rowDY;
            _btnWave = CreateColorRow("Wave color", rowY, OnWaveClick);
            rowY += rowDY;
            _btnSelection = CreateColorRow("Selection", rowY, OnSelectionClick);
            rowY += rowDY;
            _btnPlayhead = CreateColorRow("Playhead", rowY, OnPlayheadClick);

            var btnDefaults = new Button
            {
                Text = "Reset defaults",
                Left = 10,
                Top = rowY + 20,
                Width = 120
            };
            btnDefaults.Click += (s, e) => ResetCurrentThemeToDefaults();

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Left = 180,
                Top = rowY + 20,
                Width = 80
            };
            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Left = 270,
                Top = rowY + 20,
                Width = 80
            };

            Controls.AddRange(new Control[]
            {
                lblMode, _comboMode,
                _btnFormBack, _btnWaveBack, _btnWave, _btnSelection, _btnPlayhead,
                btnDefaults, btnOk, btnCancel
            });

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private Button CreateColorRow(string label, int y, EventHandler click)
        {
            var lbl = new Label
            {
                Text = label,
                Left = 10,
                Top = y + 4,
                AutoSize = true
            };
            var btn = new Button
            {
                Left = 150,
                Top = y,
                Width = 180
            };
            btn.Click += click;

            Controls.Add(lbl);
            Controls.Add(btn);
            return btn;
        }

        private ThemeDefinition GetActiveTheme() =>
            _mode == ThemeMode.Light ? _lightTheme : _darkTheme;

        private void LoadThemeToUi(ThemeDefinition t)
        {
            _btnFormBack.BackColor = t.FormBack;
            _btnWaveBack.BackColor = t.Waveform.Background;
            _btnWave.BackColor = t.Waveform.WaveColor;
            _btnSelection.BackColor = t.Waveform.SelectionFillColor;
            _btnPlayhead.BackColor = t.Waveform.PlayheadColor;
        }

        private void PickColor(Button btn, Action<ThemeDefinition, Color> apply)
        {
            var t = GetActiveTheme();
            using (var dlg = new ColorDialog { Color = btn.BackColor })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    apply(t, dlg.Color);
                    btn.BackColor = dlg.Color;
                }
            }
        }

        private void OnFormBackClick(object sender, EventArgs e) =>
            PickColor(_btnFormBack, (t, c) => t.FormBack = c);

        private void OnWaveBackClick(object sender, EventArgs e) =>
            PickColor(_btnWaveBack, (t, c) => t.Waveform.Background = c);

        private void OnWaveClick(object sender, EventArgs e) =>
            PickColor(_btnWave, (t, c) => t.Waveform.WaveColor = c);

        private void OnSelectionClick(object sender, EventArgs e) =>
            PickColor(_btnSelection, (t, c) => t.Waveform.SelectionFillColor = Color.FromArgb(80, c));

        private void OnPlayheadClick(object sender, EventArgs e) =>
            PickColor(_btnPlayhead, (t, c) => t.Waveform.PlayheadColor = c);

        private void ResetCurrentThemeToDefaults()
        {
            var t = GetActiveTheme();
            if (_mode == ThemeMode.Light)
                MainForm.SetDefaultLightTheme(t);
            else
                MainForm.SetDefaultDarkTheme(t);

            LoadThemeToUi(t);
        }
    }
}
