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
using System.Threading.Tasks;

namespace GersangStation.Setup;

public sealed partial class SetupGameStepPage : Page, ISetupStepPage, INotifyPropertyChanged
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
    public bool IsEdit => State == UiState.Edit;

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

    // ---- 결과 ----
    private bool _installValid;
    private bool _starterValid;

    // ---- 확정 상태(단일) ----
    private bool _isConfirmed;

    // ✅ CanGoNext 로직 변경:
    // "확정"은 트리거 역할만, 실제 Next는 (둘다 유효 && 확정됨)
    public bool CanGoNext => _isConfirmed && _installValid && _starterValid;

    // ✅ 확정 상태에서는 스킵 비활성화
    public bool CanSkip => !CanGoNext;

    // 하단 "확정" 버튼: 유효하지만 아직 확정되지 않은 상태에서만 활성화
    public bool CanConfirmAll => _installValid && _starterValid && !_isConfirmed;

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
        _ = InitAsync();
    }

    public bool OnNext() => true;

    public void OnSkip() { }

    private async Task InitAsync()
    {
        State = UiState.Checking;

        var sw = Stopwatch.StartNew();

        InstallPath = ReadInstallPathFromRegistry() ?? "";
        StarterPath = ReadStarterFolderFromRegistry() ?? "";

        int remain = 1000 - (int)sw.ElapsedMilliseconds;
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

    private void OnInstallButtonClicked(object sender, RoutedEventArgs e)
    {
    }

    private void OnStarterInstallButtonClicked(object sender, RoutedEventArgs e)
    {
    }

    // ---- 확정(단일) ----
    private void OnConfirmAll(object sender, RoutedEventArgs e)
    {
        if (!_installValid || !_starterValid) return;
        if (_isConfirmed) return;

        _isConfirmed = true;

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
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        OnPropertyChanged(nameof(StarterSuggestVisibility));
    }

    private void RecomputeCommon()
    {
        OnPropertyChanged(nameof(CanConfirmAll));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Registry ----
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