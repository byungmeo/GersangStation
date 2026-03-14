using Microsoft.UI.Windowing;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace GersangStation.Diagnostics;

/// <summary>
/// 처리되지 않은 예외 상세 정보를 사용자에게 보여주고 복사할 수 있게 하는 창입니다.
/// </summary>
public sealed class ExceptionDisplayWindow : Window
{
    private readonly TaskCompletionSource<object?> _closeCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TextBlock _copyStatusTextBlock;
    private readonly TextBlock _logPathTextBlock;
    private readonly TextBox _detailsTextBox;
    private readonly Button _closeButton;
    private readonly Button _openLogFolderButton;
    private readonly string? _logPath;

    /// <summary>
    /// 예외 창을 초기화하고 표시할 정보를 설정합니다.
    /// </summary>
    public ExceptionDisplayWindow(Exception exception, string context, string details, string? logPath, bool isFatal)
    {
        Title = isFatal ? "치명적인 오류" : "오류";
        _logPath = logPath;
        _copyStatusTextBlock = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as Brush,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        _logPathTextBlock = new TextBlock
        {
            Text = logPath is null ? "로그 파일: 저장 실패" : $"로그 파일: {logPath}",
            TextWrapping = TextWrapping.WrapWholeWords
        };
        _detailsTextBox = new TextBox
        {
            MinHeight = 420,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            Text = details
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(_detailsTextBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(_detailsTextBox, ScrollBarVisibility.Auto);
        _closeButton = new Button
        {
            Content = isFatal ? "앱 종료" : "닫기"
        };
        _closeButton.Click += CloseButton_Click;
        _openLogFolderButton = new Button
        {
            Content = "로그 폴더 열기",
            IsEnabled = !string.IsNullOrWhiteSpace(logPath)
        };
        _openLogFolderButton.Click += OpenLogFolderButton_Click;

        Content = BuildContent(context, isFatal);
        Closed += OnClosed;
        TryResizeWindow();
    }

    /// <summary>
    /// 창을 띄우고 사용자가 닫을 때까지 기다립니다.
    /// </summary>
    public Task ShowAndWaitAsync()
    {
        Activate();
        return _closeCompletionSource.Task;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        DataPackage package = new();
        package.SetText(_detailsTextBox.Text);
        Clipboard.SetContent(package);
        _copyStatusTextBlock.Text = "상세 정보가 클립보드에 복사되었습니다.";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_logPath))
            return;

        try
        {
            string? directoryPath = Path.GetDirectoryName(_logPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                _copyStatusTextBlock.Text = "로그 폴더 경로를 확인할 수 없습니다.";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directoryPath}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _copyStatusTextBlock.Text = $"로그 폴더 열기 실패: {ex.Message}";
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnClosed;
        _closeCompletionSource.TrySetResult(null);
    }

    private void TryResizeWindow()
    {
        try
        {
            AppWindow.Resize(new SizeInt32(1100, 800));
            AppWindow.SetPresenter(AppWindowPresenterKind.Default);
        }
        catch
        {
            // 창 크기 조정 실패는 예외 표시 자체를 막지 않도록 무시합니다.
        }
    }

    private UIElement BuildContent(string context, bool isFatal)
    {
        Grid root = new()
        {
            Padding = new Thickness(20),
            RowSpacing = 12
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock headlineTextBlock = new()
        {
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = isFatal
                ? "처리되지 않은 예외가 발생했습니다."
                : "예외가 발생했습니다."
        };
        root.Children.Add(headlineTextBlock);

        TextBlock summaryTextBlock = new()
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = isFatal
                ? $"앱을 계속 실행하면 상태가 불안정할 수 있습니다. 아래 상세 정보를 복사한 뒤 창을 닫으면 앱이 종료됩니다.{Environment.NewLine}Context: {context}"
                : $"아래 상세 정보를 복사해 전달할 수 있습니다.{Environment.NewLine}Context: {context}"
        };
        Grid.SetRow(summaryTextBlock, 1);
        root.Children.Add(summaryTextBlock);

        StackPanel logPanel = new()
        {
            Spacing = 8
        };
        logPanel.Children.Add(_logPathTextBlock);
        logPanel.Children.Add(_openLogFolderButton);
        Grid.SetRow(logPanel, 2);
        root.Children.Add(logPanel);

        Grid.SetRow(_detailsTextBox, 3);
        root.Children.Add(_detailsTextBox);

        Grid footerGrid = new()
        {
            ColumnSpacing = 12
        };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        footerGrid.Children.Add(_copyStatusTextBlock);

        Button copyButton = new()
        {
            Content = "상세 정보 복사"
        };
        copyButton.Click += CopyButton_Click;
        Grid.SetColumn(copyButton, 1);
        footerGrid.Children.Add(copyButton);

        Grid.SetColumn(_closeButton, 2);
        footerGrid.Children.Add(_closeButton);

        Grid.SetRow(footerGrid, 4);
        root.Children.Add(footerGrid);

        return root;
    }
}
