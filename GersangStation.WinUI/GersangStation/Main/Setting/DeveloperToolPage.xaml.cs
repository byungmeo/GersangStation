using Core;
using Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

namespace GersangStation.Main.Setting;

public sealed partial class DeveloperToolPage : Page
{
    public DeveloperToolPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 선택한 서버의 메인 클라이언트 버전을 목표 값으로 강제로 기록합니다.
    /// </summary>
    private async void Button_Downgrade_Click(object sender, RoutedEventArgs e)
    {
        if (ComboBox_Server.SelectedItem is not GameServerOption serverOption)
        {
            await ShowDialogAsync("서버를 선택해 주세요.", "다운그레이드할 서버가 선택되지 않았습니다.");
            return;
        }

        if (!int.TryParse(TextBox_TargetDowngradeVersion.Text?.Trim(), out int targetVersion) || targetVersion < 0)
        {
            await ShowDialogAsync("버전 값이 올바르지 않습니다.", "0 이상의 숫자 버전을 입력해 주세요.");
            return;
        }

        ClientSettings clientSettings = AppDataManager.LoadServerClientSettings(serverOption.Server);
        string installPath = clientSettings.InstallPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(installPath))
        {
            await ShowDialogAsync("메인 클라이언트 경로가 없습니다.", "선택한 서버의 메인 클라이언트 경로를 먼저 설정해 주세요.");
            return;
        }

        string vsnPath = Path.Combine(installPath, "Online", "vsn.dat");

        try
        {
            PatchManager.WriteClientVersion(installPath, targetVersion);

            int? currentVersion = PatchManager.GetCurrentClientVersion(installPath);
            await ShowDialogAsync(
                "다운그레이드 완료",
                $"{GameServerHelper.GetServerDisplayName(serverOption.Server)} 메인 클라이언트 버전을 v{currentVersion ?? targetVersion}로 기록했습니다.\n\n{vsnPath}");
        }
        catch (Exception ex)
        {
            await ShowDialogAsync("다운그레이드 실패", ex.Message);
        }
    }

    /// <summary>
    /// 개발자 도구 작업 결과를 단순 확인 대화상자로 표시합니다.
    /// </summary>
    private async Task ShowDialogAsync(string title, string content)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = "확인",
            DefaultButton = ContentDialogButton.Primary
        };

        await dialog.ShowAsync();
    }
}
