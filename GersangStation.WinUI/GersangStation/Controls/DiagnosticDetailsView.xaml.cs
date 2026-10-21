using GersangStation.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using Windows.ApplicationModel.DataTransfer;

namespace GersangStation.Controls;

public sealed partial class DiagnosticDetailsView : UserControl
{
    private string? _logPath;
    private string _originalDetails = string.Empty;

    public DiagnosticDetailsView() => InitializeComponent();

    internal void DisplayDetails(string details, string? logPath)
    {
        _logPath = logPath;
        _originalDetails = details;
        ActionInfoBar.IsOpen = false;
        DetailsTextBox.Text = string.Empty;
        DetailsTextBox.Text = DiagnosticDetailsFormatter.FormatForDisplay(details, logPath);
        DetailsTextBox.Select(0, 0);
        LogPathTextBlock.Text = logPath is null ? "로그 파일을 저장하지 못했습니다. 표시된 기록은 복사할 수 있습니다." : $"로그 파일: {logPath}";
    }

    internal void ShowStatus(string message, InfoBarSeverity severity)
    {
        ActionInfoBar.Message = message;
        ActionInfoBar.Severity = severity;
        ActionInfoBar.IsOpen = true;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DataPackage package = new();
            package.SetText(_originalDetails);
            Clipboard.SetContent(package);
            ShowStatus("오류 기록을 클립보드에 복사했습니다.", InfoBarSeverity.Success);
        }
        catch (Exception ex) { ReportFailure(ex, "복사하지 못했습니다. 잠시 후 다시 시도해 주세요."); }
    }

    private void LogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_logPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                ShowStatus("로그 폴더를 찾을 수 없습니다.", InfoBarSeverity.Error);
                return;
            }
            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            Process.Start(new ProcessStartInfo(explorer, $"\"{directory}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { ReportFailure(ex, "로그 폴더를 열지 못했습니다."); }
    }

    private void ReportFailure(Exception exception, string message)
    {
        App.ExceptionHandler.RecordRecoverable(exception, "DiagnosticDetailsView");
        try { ShowStatus(message, InfoBarSeverity.Error); }
        catch (Exception presentationException)
        {
            App.ExceptionHandler.RecordRecoverable(presentationException, "DiagnosticDetailsView.ShowStatus");
        }
    }
}
