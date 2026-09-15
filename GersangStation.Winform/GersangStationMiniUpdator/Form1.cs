namespace GersangStationMiniUpdator;

internal partial class Form1 : Form
{
    private readonly UpdateArguments options;
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool updateCompleted;

    public Form1(UpdateArguments options)
    {
        InitializeComponent();
        this.options = options;
        Shown += Form1_Shown;
        FormClosing += Form1_FormClosing;
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
        labelStatusValue.Text = progress.Message;
        progressBarMain.Value = Math.Clamp(progress.Percent, progressBarMain.Minimum, progressBarMain.Maximum);
        AppendLog(progress.Message);
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
