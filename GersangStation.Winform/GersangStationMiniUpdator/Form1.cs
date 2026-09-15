namespace GersangStationMiniUpdator;

internal partial class Form1 : Form
{
    private readonly UpdateArguments options;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool updateCompleted;
    private readonly System.Windows.Forms.Timer closeTimer;
    private readonly System.Diagnostics.Stopwatch completedElapsed = new();

    public Form1(UpdateArguments options)
    {
        InitializeComponent();
        components ??= new System.ComponentModel.Container();
        closeTimer = new System.Windows.Forms.Timer(components) { Interval = 200 };
        closeTimer.Tick += (_, _) => UpdateCloseCountdown();
        this.options = options;
        Shown += Form1_Shown;
        FormClosing += Form1_FormClosing;
        FormClosed += (_, _) => cancellationTokenSource.Dispose();
    }

    private async void Form1_Shown(object? sender, EventArgs e)
    {
        labelTargetVersionValue.Text = string.IsNullOrWhiteSpace(options.TargetVersion) ? "-" : options.TargetVersion;
        labelTargetPathValue.Text = options.TargetDirectory;
        AppendLog($"패키지 원본: {options.PackageSource}");
        AppendLog($"대상 폴더: {options.TargetDirectory}");

        Progress<UpdateProgressInfo> progress = new(HandleProgress);

        try
        {
            UpdateRunner runner = new(options, progress, cancellationTokenSource.Token);
            await runner.RunAsync();
            updateCompleted = true;
            buttonClose.Enabled = true;
            buttonClose.Text = "닫기";
            labelStatusValue.Text = "업데이트 완료";
            AppendLog("업데이트가 완료되었습니다.");
            AppendLog("10초 후 업데이트 창이 자동으로 닫힙니다.");
            completedElapsed.Start();
            UpdateCloseCountdown();
            closeTimer.Start();
        }
        catch (OperationCanceledException)
        {
            labelStatusValue.Text = "업데이트 취소";
            AppendLog("업데이트가 취소되었습니다.");
            buttonClose.Enabled = true;
            buttonClose.Text = "닫기";
        }
        catch (Exception ex)
        {
            labelStatusValue.Text = "업데이트 실패";
            AppendLog($"오류: {ex.Message}");
            MessageBox.Show(
                ex.Message,
                "거상 스테이션 미니 업데이트 실패",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            buttonClose.Enabled = true;
            buttonClose.Text = "닫기";
        }
    }

    private void HandleProgress(UpdateProgressInfo progress)
    {
        if (updateCompleted || IsDisposed) return;
        labelStatusValue.Text = progress.Message;
        progressBarMain.Value = Math.Clamp(progress.Percent, progressBarMain.Minimum, progressBarMain.Maximum);
        AppendLog(progress.Message);
    }

    private void UpdateCloseCountdown()
    {
        int remaining = Math.Max(0, (int)Math.Ceiling(10 - completedElapsed.Elapsed.TotalSeconds));
        labelStatusValue.Text = $"업데이트 완료 · {remaining}초 후 자동으로 닫힙니다.";
        buttonClose.Text = $"닫기 ({remaining})";
        if (remaining == 0)
        {
            closeTimer.Stop();
            Close();
        }
    }

    private void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        textBoxLog.AppendText(line + Environment.NewLine);
    }

    private void buttonClose_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (updateCompleted)
        {
            return;
        }

        if (buttonClose.Enabled)
        {
            return;
        }

        e.Cancel = true;
    }
}
