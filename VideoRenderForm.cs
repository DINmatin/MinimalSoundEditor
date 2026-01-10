using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

public class VideoRenderForm : Form
{
    private readonly string _videoPath;
    private readonly double _seconds;
    private readonly PictureBox _pb;

    private double _targetSeconds;
    private System.Windows.Forms.Timer _debounce;
    private CancellationTokenSource _renderCts;
    public VideoRenderForm(string videoPath, double seconds)
    {
        _videoPath = videoPath;
        _seconds = Math.Max(0, seconds);

        Text = "Video Preview";
        Width = 160*3;
        Height = 90*3;

        _pb = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };
        Controls.Add(_pb);
        _debounce = new System.Windows.Forms.Timer();
        _debounce.Interval = 150; // Debounce: nicht bei jedem Tick sofort ffmpeg
        _debounce.Tick += async (_, __) =>
        {
            _debounce.Stop();
            await RenderFrameAsync();
        };

        Shown += async (_, __) => await LoadFrameAsync();

        FormClosed += VideoRenderForm_FormClosed;
    }
    public void SetTime(double seconds)
    {
        if (seconds < 0) seconds = 0;

        // Kleine Änderungen ignorieren (reduziert ffmpeg Calls)
        if (Math.Abs(seconds - _targetSeconds) < 0.05)
            return;

        _targetSeconds = seconds;

        // debounce neu starten
        _debounce.Stop();
        _debounce.Start();
    }
    private async Task RenderFrameAsync()
    {
        try
        {
            // laufendes Render abbrechen
            _renderCts?.Cancel();
            _renderCts?.Dispose();
            _renderCts = new CancellationTokenSource();
            var ct = _renderCts.Token;

            UseWaitCursor = true;
            Text = "Video Preview (lädt...)";

            string imgPath = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                return ExtractFrameToTempPng(_videoPath, _targetSeconds);
            }, ct);

            if (ct.IsCancellationRequested)
                return;

            _pb.Image?.Dispose();
            _pb.Image = Image.FromFile(imgPath);
            _pb.Tag = imgPath;

            Text = "Video Preview";
        }
        catch (OperationCanceledException)
        {
            // ok
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Video Preview Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Text = "Video Preview";
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task LoadFrameAsync()
    {
        try
        {
            UseWaitCursor = true;
          
            Text = "Video Preview (lädt...)";

            string imgPath = await Task.Run(() => ExtractFrameToTempPng(_videoPath, _seconds));

            // wieder UI-Thread
            _pb.Image?.Dispose();
            _pb.Image = Image.FromFile(imgPath);
            _pb.Tag = imgPath;

            Text = "Video Preview";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Video Preview Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void VideoRenderForm_Shown(object? sender, EventArgs e)
    {
        try
        {
            string imgPath = ExtractFrameToTempPng(_videoPath, _seconds);
            _pb.Image?.Dispose();
            _pb.Image = Image.FromFile(imgPath);

            // Temp-Datei merken, damit wir sie später löschen können:
            _pb.Tag = imgPath;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Video Preview Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void VideoRenderForm_FormClosed(object? sender, FormClosedEventArgs e)
    {
        try
        {
            if (_pb.Image != null)
            {
                _pb.Image.Dispose();
                _pb.Image = null;
            }

            if (_pb.Tag is string img && File.Exists(img))
                File.Delete(img);
        }
        catch { /* ignore */ }
    }

    private static string ExtractFrameToTempPng(string videoPath, double seconds)
    {
        // ffmpeg neben der exe:
        string ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");

        if (!File.Exists(ffmpeg))
            throw new FileNotFoundException("ffmpeg.exe not found next to the app. Put ffmpeg.exe in the same folder as your .exe.");

        string outPng = Path.Combine(Path.GetTempPath(), "mse_frame_" + Guid.NewGuid().ToString("N") + ".png");

        // ffmpeg: -ss BEFORE -i ist schnell (keyframe-genauigkeit kann minimal abweichen)
        // Wenn du frame-genau willst: -ss nach -i (langsamer).
        string ss = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        string args =
     $"-hide_banner -loglevel error -nostdin -y -ss {ss} -i \"{videoPath}\" -frames:v 1 -vf scale=960:-1 \"{outPng}\"";


        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = false,
            RedirectStandardOutput = false
        };


        using var p = Process.Start(psi);
        if (p == null)
            throw new Exception("Could not start ffmpeg.");

        if (!p.WaitForExit(15000)) // 15s
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new Exception("ffmpeg timeout (15s).");
        }


        if (p.ExitCode != 0 || !File.Exists(outPng))
        {
            throw new Exception("ffmpeg failed. ExitCode=" + p.ExitCode);
        }

        return outPng;
    }
}
