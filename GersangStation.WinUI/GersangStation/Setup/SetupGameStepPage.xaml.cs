using Core;
using Core.Extractor;
using Core.Patch;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;

namespace GersangStation.Setup;

public sealed partial class SetupGameStepPage : Page, ISetupStepPage, IAsyncSetupStepPage, IBusySetupStepPage, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // --- UI text/decoration ---
    private string _installDescription = "";
    public string InstallDescription
    {
        get => _installDescription;
        private set { if (_installDescription == value) return; _installDescription = value; OnPropertyChanged(nameof(InstallDescription)); }
    }

    private string _starterDescription = "";
    public string StarterDescription
    {
        get => _starterDescription;
        private set { if (_starterDescription == value) return; _starterDescription = value; OnPropertyChanged(nameof(StarterDescription)); }
    }

    private Brush _installBorderBrush = new SolidColorBrush(Colors.Transparent);
    public Brush InstallBorderBrush
    {
        get => _installBorderBrush;
        private set { _installBorderBrush = value; OnPropertyChanged(nameof(InstallBorderBrush)); }
    }

    private Brush _starterBorderBrush = new SolidColorBrush(Colors.Transparent);
    public Brush StarterBorderBrush
    {
        get => _starterBorderBrush;
        private set { _starterBorderBrush = value; OnPropertyChanged(nameof(StarterBorderBrush)); }
    }

    private Thickness _installBorderThickness = new Thickness(1);
    public Thickness InstallBorderThickness
    {
        get => _installBorderThickness;
        private set { _installBorderThickness = value; OnPropertyChanged(nameof(InstallBorderThickness)); }
    }

    private Thickness _starterBorderThickness = new Thickness(1);
    public Thickness StarterBorderThickness
    {
        get => _starterBorderThickness;
        private set { _starterBorderThickness = value; OnPropertyChanged(nameof(StarterBorderThickness)); }
    }

    private static readonly Brush BrushInvalid = new SolidColorBrush(Colors.IndianRed);
    private static readonly Brush BrushValid = new SolidColorBrush(Colors.SeaGreen);
    private static readonly Brush BrushNeutral = new SolidColorBrush(Colors.Transparent);

    private enum UiState { Checking, Edit }

    private UiState _uiState;
    private UiState State
    {
        get => _uiState;
        set
        {
            if (_uiState == value) return;
            _uiState = value;
            OnPropertyChanged(nameof(IsChecking));
            OnPropertyChanged(nameof(IsEdit));
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsChecking => State == UiState.Checking;
    public bool IsEdit => State == UiState.Edit && !IsSavingTransition;

    private bool _isSavingTransition;
    public bool IsSavingTransition
    {
        get => _isSavingTransition;
        private set
        {
            if (_isSavingTransition == value) return;
            _isSavingTransition = value;
            OnPropertyChanged(nameof(IsSavingTransition));
            OnPropertyChanged(nameof(IsEdit));
            RecomputeCommon();
        }
    }

    // ---- 입력 ----
    private string _installPath = "";
    public string InstallPath
    {
        get => _installPath;
        set
        {
            if (_installPath == value) return;
            _installPath = value;

            // 확정 이후 경로가 바뀌면 확정 해제
            if (_isConfirmed)
            {
                _isConfirmed = false;
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanSkip));
            }

            RecomputeInstall();
            RecomputeCommon();

            OnPropertyChanged(nameof(InstallPath));
        }
    }

    private string _starterPath = "";
    public string StarterPath
    {
        get => _starterPath;
        set
        {
            if (_starterPath == value) return;
            _starterPath = value;

            // 확정 이후 경로가 바뀌면 확정 해제
            if (_isConfirmed)
            {
                _isConfirmed = false;
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(CanSkip));
            }

            RecomputeStarter();
            RecomputeCommon();

            OnPropertyChanged(nameof(StarterPath));
        }
    }


    private bool _isInstallingClient;
    public bool IsInstallingClient
    {
        get => _isInstallingClient;
        private set
        {
            if (_isInstallingClient == value) return;
            _isInstallingClient = value;
            OnPropertyChanged(nameof(IsInstallingClient));
            OnPropertyChanged(nameof(IsInstallButtonVisible));
            OnPropertyChanged(nameof(IsInstallProgressVisible));
            OnPropertyChanged(nameof(IsInstallIdle));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanConfirmAllAndIdle));
            NotifyInstallComputed();
            NotifyStarterComputed();
        }
    }

    public bool IsInstallButtonVisible => !IsInstallingClient;
    public bool IsInstallProgressVisible => IsInstallingClient;
    public bool IsInstallIdle => !IsInstallingClient;
    public bool IsBusy => IsInstallingClient;

    private double _installProgressPercent;
    public double InstallProgressPercent
    {
        get => _installProgressPercent;
        private set { if (Math.Abs(_installProgressPercent - value) < 0.001) return; _installProgressPercent = value; OnPropertyChanged(nameof(InstallProgressPercent)); }
    }

    private string _installProgressText = "";
    public string InstallProgressText
    {
        get => _installProgressText;
        private set { if (_installProgressText == value) return; _installProgressText = value; OnPropertyChanged(nameof(InstallProgressText)); }
    }

    private string _installRemainingCapacityText = "";
    public string InstallRemainingCapacityText
    {
        get => _installRemainingCapacityText;
        private set { if (_installRemainingCapacityText == value) return; _installRemainingCapacityText = value; OnPropertyChanged(nameof(InstallRemainingCapacityText)); }
    }

    private string _installFailureReason = "";
    public string InstallFailureReason
    {
        get => _installFailureReason;
        private set
        {
            if (_installFailureReason == value) return;
            _installFailureReason = value;
            OnPropertyChanged(nameof(InstallFailureReason));
            OnPropertyChanged(nameof(InstallFailureVisibility));
        }
    }

    public Visibility InstallFailureVisibility =>
        string.IsNullOrWhiteSpace(InstallFailureReason) ? Visibility.Collapsed : Visibility.Visible;

    private CancellationTokenSource? _installClientCts;

    // ---- 결과 ----
    private bool _installValid;
    private bool _starterValid;

    // ---- 확정 상태(단일) ----
    private bool _isConfirmed;

    // ✅ CanGoNext 로직 변경:
    // "확정"은 트리거 역할만, 실제 Next는 (둘다 유효 && 확정됨)
    public bool CanGoNext => !IsSavingTransition && _isConfirmed && _installValid && _starterValid;

    // ✅ 확정 상태에서는 스킵 비활성화
    public bool CanSkip => !IsSavingTransition && !CanGoNext;

    // 하단 "확정" 버튼: 유효하지만 아직 확정되지 않은 상태에서만 활성화
    public bool CanConfirmAll => _installValid && _starterValid && !_isConfirmed;
    public bool CanConfirmAllAndIdle => CanConfirmAll && IsInstallIdle;

    public Visibility InstallSuggestVisibility =>
        _installValid ? Visibility.Collapsed : Visibility.Visible;

    public Visibility StarterSuggestVisibility =>
    _starterValid ? Visibility.Collapsed : Visibility.Visible;

    public string NextHintText =>
        CanGoNext ? "확정됐어요. 다음으로 진행할 수 있어요."
                  : "두 경로를 유효하게 만든 뒤 ‘확정’해야 다음 버튼이 활성화돼요.";

    public event EventHandler? StateChanged;

    public SetupGameStepPage()
    {
        InitializeComponent();
        Unloaded += OnPageUnloaded;
        _ = InitAsync();
    }

    public bool OnNext()
    {
        SetupFlowState.InstallPath = InstallPath;
        SetupFlowState.ShouldAutoSkipMultiClient = false;
        return true;
    }

    public async Task<bool> OnNextAsync()
    {
        IsSavingTransition = true;

        try
        {
            await Task.Delay(1000);
            return OnNext();
        }
        finally
        {
            IsSavingTransition = false;
        }
    }

    public void OnSkip()
    {
        SetupFlowState.InstallPath = InstallPath;
        SetupFlowState.ShouldAutoSkipMultiClient = true;
    }

    private async Task InitAsync()
    {
        State = UiState.Checking;

        var sw = Stopwatch.StartNew();

        var clientSettings = AppDataManager.LoadClientSettings();
        string savedInstallPath = clientSettings.InstallPath;
        if (!string.IsNullOrWhiteSpace(savedInstallPath))
        {
            InstallPath = savedInstallPath;
            if (!_installValid)
                InstallPath = ReadInstallPathFromRegistry() ?? "";
        }
        else
        {
            InstallPath = ReadInstallPathFromRegistry() ?? "";
        }

        StarterPath = ReadStarterFolderFromRegistry() ?? "";

        int remain = 1500 - (int)sw.ElapsedMilliseconds;
        if (remain > 0)
            await Task.Delay(remain);

        RecomputeInstall();
        RecomputeStarter();
        RecomputeCommon();

        State = UiState.Edit;
    }

    // ---- Picker ----
    private async void OnPickInstallFolder(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        button.IsEnabled = false;
        string? path = await PickFolderAsync(button);
        if (!string.IsNullOrWhiteSpace(path))
            InstallPath = path;
        button.IsEnabled = true;
    }

    private async void OnPickStarterFolder(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        button.IsEnabled = false;
        string? path = await PickFolderAsync(button);
        if (!string.IsNullOrWhiteSpace(path))
            StarterPath = path;
        button.IsEnabled = true;
    }

    private static async Task<string?> PickFolderAsync(Button button)
    {
        var picker = new FolderPicker(button.XamlRoot.ContentIslandEnvironment.AppWindowId)
        {
            CommitButtonText = "선택",
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List
        };

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    // ---- 자동 검사 ----
    private void OnAutoDetectInstall(object sender, RoutedEventArgs e)
    {
        InstallPath = ReadInstallPathFromRegistry() ?? "";
    }

    private void OnAutoDetectStarter(object sender, RoutedEventArgs e)
    {
        StarterPath = ReadStarterFolderFromRegistry() ?? "";
    }

    private async void OnInstallButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (IsInstallingClient) return;

        button.IsEnabled = false;
        InstallFailureReason = "";

        try
        {
            string? selectedRoot = await PickFolderAsync(button);
            if (string.IsNullOrWhiteSpace(selectedRoot))
                return;

            string installRoot = selectedRoot.Trim();
            string installRootWithGameFolder = PatchClientApi.NormalizeFullClientInstallRoot(installRoot);
            string parentRoot = Directory.GetParent(installRootWithGameFolder)?.FullName ?? installRootWithGameFolder;
            string archivePath = Path.Combine(installRootWithGameFolder, "Gersang_Install.7z");
            bool skipDownloadIfArchiveExists = false;

            if (File.Exists(archivePath))
            {
                var archiveExistsDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "이미 게임 압축파일이 존재합니다. 다시 다운로드 하시겠습니까?",
                    PrimaryButtonText = "예",
                    CloseButtonText = "건너뛰기",
                    DefaultButton = ContentDialogButton.Close
                };

                ContentDialogResult dialogResult = await archiveExistsDialog.ShowAsync();
                skipDownloadIfArchiveExists = dialogResult == ContentDialogResult.None;
            }

            UpdateRemainingCapacityText(parentRoot);

            IsInstallingClient = true;
            InstallProgressPercent = 0;
            InstallProgressText = "다운로드 준비 중...";

            _installClientCts?.Cancel();
            _installClientCts?.Dispose();
            _installClientCts = new CancellationTokenSource();

            var downloadProgress = new Progress<DownloadProgress>(p =>
            {
                long received = p.BytesReceived;
                long total = p.TotalBytes ?? 0;

                if (total > 0)
                {
                    InstallProgressPercent = Math.Clamp(received * 100.0 / total, 0, 100);
                    InstallProgressText = $"다운로드 중... {FormatBytes(received)} / {FormatBytes(total)}";
                }
                else
                {
                    InstallProgressPercent = 0;
                    InstallProgressText = $"다운로드 중... {FormatBytes(received)}";
                }
            });

            var extractionProgress = new Progress<ExtractionProgress>(p =>
            {
                InstallProgressPercent = Math.Clamp(p.Percentage, 0, 100);

                string processedText = p.TotalEntries is > 0
                    ? $"{p.ProcessedEntries}/{p.TotalEntries}"
                    : p.ProcessedEntries.ToString();

                if (!string.IsNullOrWhiteSpace(p.CurrentEntry))
                    InstallProgressText = $"압축 해제 중... {processedText} - {p.CurrentEntry}";
                else
                    InstallProgressText = $"압축 해제 중... {processedText}";
            });

            await PatchClientApi.InstallFullClientAsync(
                installRoot: installRoot,
                progress: downloadProgress,
                extractionProgress: extractionProgress,
                skipDownloadIfArchiveExists: skipDownloadIfArchiveExists,
                ct: _installClientCts.Token);

            InstallProgressPercent = 100;
            InstallProgressText = "설치가 완료됐어요.";

            InstallPath = installRootWithGameFolder;
            UpdateRemainingCapacityText(parentRoot);
        }
        catch (OperationCanceledException)
        {
            InstallFailureReason = "설치가 취소되었습니다.";
        }
        catch (Exception ex)
        {
            InstallFailureReason = $"설치 실패: {ex.Message}";
        }
        finally
        {
            IsInstallingClient = false;
            button.IsEnabled = true;
        }
    }


    private async void OnCancelInstallButtonClicked(object sender, RoutedEventArgs e)
    {
        await ConfirmCancelBusyWorkAsync(
            title: "설치 취소",
            message: "현재 다운로드 또는 압축 해제가 진행 중입니다. 정말 취소하시겠습니까?",
            primaryButtonText: "예",
            closeButtonText: "아니요");
    }

    public async Task<bool> ConfirmCancelBusyWorkAsync(string title, string message, string primaryButtonText, string closeButtonText)
    {
        if (!IsInstallingClient || _installClientCts is null)
            return false;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryButtonText,
            CloseButtonText = closeButtonText,
            DefaultButton = ContentDialogButton.Close
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
            return false;

        Debug.WriteLine("[InstallFullClient] User confirmed cancellation request.");
        _installClientCts.Cancel();
        return true;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        // 예기치 않은 종료/페이지 교체 시에도 진행 중 작업을 안전하게 취소합니다.
        if (_installClientCts is null)
            return;

        try
        {
            _installClientCts.Cancel();
        }
        catch
        {
            // best-effort
        }
    }

    private async void OnStarterInstallButtonClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            await Launcher.LaunchUriAsync(new Uri("https://www.gersang.co.kr/dataroom/client.gs"));
        }
        catch
        {
            // 브라우저 실행 실패 시에는 조용히 무시합니다.
        }
    }

    // ---- 확정(단일) ----
    private void OnConfirmAll(object sender, RoutedEventArgs e)
    {
        if (!_installValid || !_starterValid) return;
        if (_isConfirmed) return;

        _isConfirmed = true;

        // 확정 시 InstallPath만 LocalFolder 파일에 저장합니다.
        AppDataManager.SaveInstallPath(InstallPath.Trim());

        // 확정 표현(테두리 두께) 업데이트
        RecomputeInstall();
        RecomputeStarter();
        RecomputeCommon();
    }

    // ---- 유효성: InstallPath ----
    private void RecomputeInstall()
    {
        _installValid = false;

        string path = (InstallPath ?? "").Trim();

        InstallBorderBrush = BrushNeutral;
        InstallBorderThickness = new Thickness(1);

        if (path.Length == 0)
        {
            InstallDescription = "❌ 설치 경로가 비어있어요.";
            InstallBorderBrush = BrushInvalid;
            NotifyInstallComputed();
            return;
        }

        if (!Directory.Exists(path))
        {
            InstallDescription = "❌ 폴더가 존재하지 않아요.";
            InstallBorderBrush = BrushInvalid;
            NotifyInstallComputed();
            return;
        }

        string runExe = Path.Combine(path, "Run.exe");
        if (!File.Exists(runExe))
        {
            InstallDescription = "❌ Run.exe를 찾지 못했어요.";
            InstallBorderBrush = BrushInvalid;
            NotifyInstallComputed();
            return;
        }

        string charDir = Path.Combine(path, "char");
        if (Directory.Exists(charDir))
        {
            if (IsSymlinkCheckSupported(path))
            {
                if (IsReparsePointDirectory(charDir))
                {
                    // ✅ (...\Gersang3\ -> ...\Gersang\) 후보 검사
                    string? parent2 = null;
                    try { parent2 = Directory.GetParent(path)?.Parent?.FullName; } catch { parent2 = null; }

                    if (!string.IsNullOrWhiteSpace(parent2))
                    {
                        string candidate = Path.Combine(parent2, "Gersang");

                        if (Directory.Exists(candidate))
                        {
                            string candRunExe = Path.Combine(candidate, "Run.exe");
                            if (File.Exists(candRunExe))
                            {
                                string candCharDir = Path.Combine(candidate, "char");

                                if (!Directory.Exists(candCharDir) ||
                                    !IsSymlinkCheckSupported(candidate) ||
                                    !IsReparsePointDirectory(candCharDir))
                                {
                                    if (!string.Equals(InstallPath, candidate, StringComparison.OrdinalIgnoreCase))
                                    {
                                        InstallPath = candidate;
                                        return;
                                    }
                                }
                            }
                        }
                    }

                    InstallDescription = "❌ char 폴더가 심볼릭 링크(또는 정션)로 보입니다.";
                    InstallBorderBrush = BrushInvalid;
                    NotifyInstallComputed();
                    return;
                }
            }
        }

        _installValid = true;

        InstallBorderBrush = BrushValid;
        InstallDescription = _isConfirmed ? "✅ 유효해요. (확정됨)" : "✅ 유효해요.";
        InstallBorderThickness = _isConfirmed ? new Thickness(2) : new Thickness(1);

        NotifyInstallComputed();
    }

    private void NotifyInstallComputed()
    {
        OnPropertyChanged(nameof(CanConfirmAll));
        OnPropertyChanged(nameof(CanConfirmAllAndIdle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(InstallSuggestVisibility));
        OnPropertyChanged(nameof(NextHintText));
    }

    private static bool IsSymlinkCheckSupported(string anyPathInDrive)
    {
        try
        {
            string root = Path.GetPathRoot(anyPathInDrive) ?? "";
            if (root.Length == 0) return false;

            var di = new DriveInfo(root);
            string fmt = (di.DriveFormat ?? "").ToUpperInvariant();
            return fmt == "NTFS" || fmt == "UDF";
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReparsePointDirectory(string dirPath)
    {
        try
        {
            var di = new DirectoryInfo(dirPath);
            return (di.Attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    // ---- 유효성: StarterPath ----
    private void RecomputeStarter()
    {
        _starterValid = false;

        string path = (StarterPath ?? "").Trim();

        StarterBorderBrush = BrushNeutral;
        StarterBorderThickness = new Thickness(1);

        if (path.Length == 0)
        {
            StarterDescription = "❌ 스타터 설치 경로가 비어있어요.";
            StarterBorderBrush = BrushInvalid;
            NotifyStarterComputed();
            return;
        }

        if (!Directory.Exists(path))
        {
            StarterDescription = "❌ 폴더가 존재하지 않아요.";
            StarterBorderBrush = BrushInvalid;
            NotifyStarterComputed();
            return;
        }

        string exe = Path.Combine(path, "GersangGameStarter.exe");
        if (!File.Exists(exe))
        {
            StarterDescription = "❌ GersangGameStarter.exe를 찾지 못했어요.";
            StarterBorderBrush = BrushInvalid;
            NotifyStarterComputed();
            return;
        }

        _starterValid = true;

        StarterBorderBrush = BrushValid;
        StarterDescription = _isConfirmed ? "✅ 유효해요. (확정됨)" : "✅ 유효해요.";
        StarterBorderThickness = _isConfirmed ? new Thickness(2) : new Thickness(1);

        NotifyStarterComputed();
    }

    private void NotifyStarterComputed()
    {
        OnPropertyChanged(nameof(CanConfirmAll));
        OnPropertyChanged(nameof(CanConfirmAllAndIdle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        OnPropertyChanged(nameof(StarterSuggestVisibility));
    }

    private void RecomputeCommon()
    {
        OnPropertyChanged(nameof(CanConfirmAll));
        OnPropertyChanged(nameof(CanConfirmAllAndIdle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Registry ----

    private void UpdateRemainingCapacityText(string anyPathInDrive)
    {
        try
        {
            string root = Path.GetPathRoot(anyPathInDrive) ?? "";
            if (string.IsNullOrWhiteSpace(root))
            {
                InstallRemainingCapacityText = "";
                return;
            }

            var drive = new DriveInfo(root);
            InstallRemainingCapacityText = $"남은 용량: {FormatBytes(drive.AvailableFreeSpace)}";
        }
        catch
        {
            InstallRemainingCapacityText = "";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = Math.Max(0, bytes);
        int unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private static string? ReadInstallPathFromRegistry()
    {
        using RegistryKey? k = Registry.CurrentUser.OpenSubKey(@"Software\JOYON\Gersang\Korean", false);
        return k?.GetValue("InstallPath")?.ToString();
    }

    private static string? ReadStarterFolderFromRegistry()
    {
        using RegistryKey? k = Registry.ClassesRoot.OpenSubKey(@"Gersang\shell\open\command", false);
        string? command = k?.GetValue("")?.ToString();
        if (string.IsNullOrWhiteSpace(command)) return null;

        string exePath = ExtractExePathFromCommand(command);
        if (string.IsNullOrWhiteSpace(exePath)) return null;

        try { return Path.GetDirectoryName(exePath); }
        catch { return null; }
    }

    private static string ExtractExePathFromCommand(string command)
    {
        command = command.Trim();
        if (command.Length == 0) return "";

        if (command[0] == '"')
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command.Substring(1, end - 1) : "";
        }

        int space = command.IndexOf(' ');
        return space > 0 ? command.Substring(0, space) : command;
    }
}
