using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace GersangStation.Diagnostics;

/// <summary>
/// 예외 상세 정보를 앱 전역 ContentDialog로 표시하고 복사 또는 로그 폴더 열기를 지원합니다.
/// </summary>
internal static class ExceptionDisplayDialog
{
    /// <summary>
    /// 예외 상세 정보를 ContentDialog로 표시하고 사용자가 닫을 때까지 기다립니다.
    /// </summary>
    public static async Task ShowAsync(XamlRoot xamlRoot, string context, string details, string? logPath, bool isFatal)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);

        TextBlock copyStatusTextBlock = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Application.Current.Resources["AccentTextFillColorPrimaryBrush"] as Brush,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        TextBlock logPathTextBlock = new()
        {
            Text = logPath is null ? "로그 파일: 저장 실패" : $"로그 파일: {logPath}",
            TextWrapping = TextWrapping.WrapWholeWords
        };

        TextBox detailsTextBox = new()
        {
            MinHeight = 360,
            AcceptsReturn = true,
            FontFamily = new FontFamily("Consolas"),
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            Text = details
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(detailsTextBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(detailsTextBox, ScrollBarVisibility.Auto);

        void CopyDetails()
        {
            DataPackage package = new();
            package.SetText(detailsTextBox.Text);
            Clipboard.SetContent(package);
            copyStatusTextBlock.Text = "상세 정보가 클립보드에 복사되었습니다.";
        }

        Button openLogFolderButton = new()
        {
            Content = "로그 폴더 열기",
            IsEnabled = !string.IsNullOrWhiteSpace(logPath)
        };
        openLogFolderButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(logPath))
                return;

            try
            {
                string? directoryPath = Path.GetDirectoryName(logPath);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    copyStatusTextBlock.Text = "로그 폴더 경로를 확인할 수 없습니다.";
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directoryPath}\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                copyStatusTextBlock.Text = $"로그 폴더 열기 실패: {ex.Message}";
            }
        };

        Grid contentGrid = new()
        {
            RowSpacing = 12
        };
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        TextBlock headlineTextBlock = new()
        {
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = isFatal
                ? "처리되지 않은 예외가 발생했습니다."
                : "예외가 발생했습니다."
        };
        contentGrid.Children.Add(headlineTextBlock);

        TextBlock summaryTextBlock = new()
        {
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = isFatal
                ? $"앱을 계속 실행하면 상태가 불안정할 수 있습니다. 아래 상세 정보를 복사한 뒤 닫기를 누르면 앱이 종료됩니다.{Environment.NewLine}Context: {context}"
                : $"아래 상세 정보를 복사해 전달할 수 있습니다.{Environment.NewLine}Context: {context}"
        };
        Grid.SetRow(summaryTextBlock, 1);
        contentGrid.Children.Add(summaryTextBlock);

        Grid.SetRow(logPathTextBlock, 2);
        contentGrid.Children.Add(logPathTextBlock);

        Grid.SetRow(openLogFolderButton, 3);
        contentGrid.Children.Add(openLogFolderButton);

        ScrollViewer detailsScrollViewer = new()
        {
            Content = detailsTextBox,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 360,
            MaxHeight = 520
        };
        Grid.SetRow(detailsScrollViewer, 4);
        contentGrid.Children.Add(detailsScrollViewer);

        Grid.SetRow(copyStatusTextBlock, 5);
        contentGrid.Children.Add(copyStatusTextBlock);

        AppContentDialog dialog = new()
        {
            XamlRoot = xamlRoot,
            Title = isFatal ? "치명적인 오류" : "오류",
            Content = contentGrid,
            PrimaryButtonText = "상세 정보 복사",
            CloseButtonText = isFatal ? "앱 종료" : "닫기",
            DefaultButton = ContentDialogButton.Primary
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            CopyDetails();
            args.Cancel = true;
        };

        await dialog.ShowManagedAsync();
    }
}
