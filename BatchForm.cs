using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MinimalSoundEditor
{
    public sealed class BatchForm : Form
    {
        private readonly ListView _lv;
        private readonly CheckBox _chkNormalize;
        private readonly CheckBox _chkTrim;
        private readonly Button _btnStart;
        private readonly Button _btnRemove;
        private readonly Button _btnClear;
        private readonly Button _btnCancel;
        private readonly ProgressBar _progress;
        private readonly TextBox _txtLog;

        private CancellationTokenSource? _cts;

        private readonly CheckBox _chkOutWav;
        private readonly CheckBox _chkOutMp4;
        private readonly GroupBox _grpOutput;
        public BatchForm()
        {
            Text = "Batch";
            StartPosition = FormStartPosition.CenterParent;
            Width = 820;
            Height = 600;

            // ✅ no black background
            BackColor = SystemColors.Control;
            ForeColor = SystemColors.ControlText;

            AllowDrop = true;
            DragEnter += BatchForm_DragEnter;
            DragDrop += BatchForm_DragDrop;

            _lv = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                Dock = DockStyle.Top,
                Height = 260
            };
            _lv.Columns.Add("File", 520);
            _lv.Columns.Add("Type", 60);
            _lv.Columns.Add("Status", 180);

            _chkNormalize = new CheckBox { Text = "Normalize", Left = 12, Top = 275, Width = 120, Checked = true };
            _chkTrim = new CheckBox { Text = "Trim silence", Left = 140, Top = 275, Width = 120, Checked = true };

            _grpOutput = new GroupBox
            {
                Text = "mp4-Files",
                Left = 460,
                Top = 262,
                Width = 312,
                Height = 40
            };

            _chkOutWav = new CheckBox { Text = "WAV", Left = 12, Top = 25, Width = 90, Checked = true };
            _chkOutMp4 = new CheckBox { Text = "MP4-Video", Left = 110, Top = 25, Width = 140, Checked = true };
            _grpOutput.Controls.Add(_chkOutWav);
            _grpOutput.Controls.Add(_chkOutMp4);
            Controls.Add(_grpOutput);

            _btnStart = new Button { Text = "Start", Left = 12, Top = 320, Width = 120, Height = 32 };
            _btnCancel = new Button { Text = "Cancel", Left = 140, Top = 320, Width = 120, Height = 32, Enabled = false };
            _btnRemove = new Button { Text = "Remove selected", Left = 270, Top = 320, Width = 150, Height = 32 };
            _btnClear = new Button { Text = "Clear", Left = 430, Top = 320, Width = 120, Height = 32 };

            _btnStart.Click += BtnStart_Click;
            _btnCancel.Click += BtnCancel_Click;
            _btnRemove.Click += (_, __) => RemoveSelected();
            _btnClear.Click += (_, __) => _lv.Items.Clear();

            _progress = new ProgressBar { Left = 12, Top = 355, Width = 760, Height = 18 };

            _txtLog = new TextBox
            {
                Left = 12,
                Top = 385,
                Width = 760,
                Height = 160,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true
            };

            Controls.Add(_lv);
            Controls.Add(_chkNormalize);
            Controls.Add(_chkTrim);
            Controls.Add(_btnStart);
            Controls.Add(_btnCancel);
            Controls.Add(_btnRemove);
            Controls.Add(_btnClear);
            Controls.Add(_progress);
            Controls.Add(_txtLog);
        }

        private void RemoveSelected()
        {
            foreach (ListViewItem it in _lv.SelectedItems)
                _lv.Items.Remove(it);
        }

        private void BatchForm_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }

        private void BatchForm_DragDrop(object? sender, DragEventArgs e)
        {
            var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            AddFiles(files);
        }

        private void AddFiles(IEnumerable<string> paths)
        {
            foreach (var p in paths)
            {
                if (Directory.Exists(p))
                {
                    // optional: add directory contents
                    var all = Directory.GetFiles(p);
                    AddFiles(all);
                    continue;
                }

                if (!File.Exists(p)) continue;

                if (!BatchFileValidator.IsSupported(p, out string type))
                {
                    Log($"Skip (unsupported): {p}");
                    continue;
                }

                if (_lv.Items.Cast<ListViewItem>().Any(it => string.Equals(it.Tag as string, p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var item = new ListViewItem(Path.GetFileName(p));
                item.SubItems.Add(type);
                item.SubItems.Add("Queued");
                item.Tag = p;
                _lv.Items.Add(item);
            }
        }

        private async void BtnStart_Click(object? sender, EventArgs e)
        {
            if ( !_chkOutWav.Checked && !_chkOutMp4.Checked)
            {
                MessageBox.Show(this, "Please choose at least one output: WAV and/or MP4.", "Batch",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }


            if (_lv.Items.Count == 0) return;
            if (!_chkNormalize.Checked && !_chkTrim.Checked )
            {
                MessageBox.Show(this, "Nothing to do. Enable at least one checkbox.", "Batch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var options = new BatchOptions(
    normalize: _chkNormalize.Checked,
    trimSilence: _chkTrim.Checked,
    outputWav: _chkOutWav.Checked,
    outputMp4: _chkOutMp4.Checked
);

            ToggleUi(isRunning: true);

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

           

            try
            {
                _progress.Minimum = 0;
                _progress.Maximum = _lv.Items.Count;
                _progress.Value = 0;

                for (int i = 0; i < _lv.Items.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();

                    var it = _lv.Items[i];
                    string path = (string)it.Tag!;
                    SetStatus(it, "Processing…");

                    try
                    {
                        await BatchProcessor.ProcessOneAsync(path, options, ct, Log);
                        SetStatus(it, "Done");
                    }
                    catch (OperationCanceledException)
                    {
                        SetStatus(it, "Canceled");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        SetStatus(it, "Error");
                        Log($"ERROR: {Path.GetFileName(path)} → {ex.Message}");
                    }

                    _progress.Value = Math.Min(_progress.Maximum, i + 1);
                }

                Log("Batch finished.");
            }
            catch (OperationCanceledException)
            {
                Log("Batch canceled.");
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                ToggleUi(isRunning: false);
            }
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private void ToggleUi(bool isRunning)
        {
            _btnStart.Enabled = !isRunning;
            _btnCancel.Enabled = isRunning;
            _btnRemove.Enabled = !isRunning;
            _btnClear.Enabled = !isRunning;

            _chkNormalize.Enabled = !isRunning;
            _chkTrim.Enabled = !isRunning;

            _chkOutWav.Enabled = !isRunning;
            _chkOutMp4.Enabled = !isRunning;
        }

        private void SetStatus(ListViewItem it, string status)
        {
            it.SubItems[2].Text = status;
        }

        private void Log(string s)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(s)));
                return;
            }

            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {s}{Environment.NewLine}");
        }
    }
}
