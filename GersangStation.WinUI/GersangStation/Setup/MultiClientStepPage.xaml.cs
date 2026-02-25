using Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace GersangStation.Setup;

public sealed partial class MultiClientStepPage : Page, ISetupStepPage, INotifyPropertyChanged
{
    private enum ManualMode
    {
        AlreadyCreated,
        WantCreate
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static readonly Brush BrushInvalid = new SolidColorBrush(Colors.IndianRed);
    private static readonly Brush BrushValid = new SolidColorBrush(Colors.SeaGreen);
    private static readonly Brush BrushNeutral = new SolidColorBrush(Colors.Transparent);

    private string _expectedFolderName = "";
    private string _installParentPath = "";

    private bool _isDriveCompatible;
    public bool IsDriveCompatible
    {
        get => _isDriveCompatible;
        private set { if (_isDriveCompatible == value) return; _isDriveCompatible = value; OnPropertyChanged(nameof(IsDriveCompatible)); }
    }

    private string _driveFormatText = "";
    public string DriveFormatText
    {
        get => _driveFormatText;
        private set { if (_driveFormatText == value) return; _driveFormatText = value; OnPropertyChanged(nameof(DriveFormatText)); }
    }

    private string _driveCompatibilityText = "";
    public string DriveCompatibilityText
    {
        get => _driveCompatibilityText;
        private set { if (_driveCompatibilityText == value) return; _driveCompatibilityText = value; OnPropertyChanged(nameof(DriveCompatibilityText)); }
    }

    private Brush _driveCompatibilityBrush = BrushInvalid;
    public Brush DriveCompatibilityBrush
    {
        get => _driveCompatibilityBrush;
        private set { _driveCompatibilityBrush = value; OnPropertyChanged(nameof(DriveCompatibilityBrush)); }
    }

    public bool ShowIncompatibleHelp => !IsDriveCompatible;

    private bool _useFastMultiClient;
    public bool UseFastMultiClient
    {
        get => _useFastMultiClient;
        set
        {
            if (_useFastMultiClient == value) return;
            _useFastMultiClient = value;
            OnPropertyChanged(nameof(UseFastMultiClient));
            OnPropertyChanged(nameof(IsManualMode));
            RecomputeCommon();
        }
    }

    public bool IsFastOptionEditable => IsDriveCompatible;
    public bool IsManualMode => !UseFastMultiClient;

    private string _multiClientFolderName1 = "Gersang2";
    public string MultiClientFolderName1
    {
        get => _multiClientFolderName1;
        set
        {
            if (_multiClientFolderName1 == value) return;
            _multiClientFolderName1 = value;
            RecomputeFolder1();
            RecomputeFolder2();
            RecomputePreviewPaths();
            RecomputeCommon();
            OnPropertyChanged(nameof(MultiClientFolderName1));
        }
    }

    private string _multiClientFolderName2 = "Gersang3";
    public string MultiClientFolderName2
    {
        get => _multiClientFolderName2;
        set
        {
            if (_multiClientFolderName2 == value) return;
            _multiClientFolderName2 = value;
            RecomputeFolder1();
            RecomputeFolder2();
            RecomputePreviewPaths();
            RecomputeCommon();
            OnPropertyChanged(nameof(MultiClientFolderName2));
        }
    }

    private string _folder1PreviewPath = "";
    public string Folder1PreviewPath
    {
        get => _folder1PreviewPath;
        private set { if (_folder1PreviewPath == value) return; _folder1PreviewPath = value; OnPropertyChanged(nameof(Folder1PreviewPath)); }
    }

    private string _folder2PreviewPath = "";
    public string Folder2PreviewPath
    {
        get => _folder2PreviewPath;
        private set { if (_folder2PreviewPath == value) return; _folder2PreviewPath = value; OnPropertyChanged(nameof(Folder2PreviewPath)); }
    }

    private string _folder1Description = "";
    public string Folder1Description
    {
        get => _folder1Description;
        private set { if (_folder1Description == value) return; _folder1Description = value; OnPropertyChanged(nameof(Folder1Description)); }
    }

    private string _folder2Description = "";
    public string Folder2Description
    {
        get => _folder2Description;
        private set { if (_folder2Description == value) return; _folder2Description = value; OnPropertyChanged(nameof(Folder2Description)); }
    }

    private Brush _folder1BorderBrush = BrushNeutral;
    public Brush Folder1BorderBrush
    {
        get => _folder1BorderBrush;
        private set { _folder1BorderBrush = value; OnPropertyChanged(nameof(Folder1BorderBrush)); }
    }

    private Brush _folder2BorderBrush = BrushNeutral;
    public Brush Folder2BorderBrush
    {
        get => _folder2BorderBrush;
        private set { _folder2BorderBrush = value; OnPropertyChanged(nameof(Folder2BorderBrush)); }
    }

    private Thickness _folder1BorderThickness = new(1);
    public Thickness Folder1BorderThickness
    {
        get => _folder1BorderThickness;
        private set { _folder1BorderThickness = value; OnPropertyChanged(nameof(Folder1BorderThickness)); }
    }

    private Thickness _folder2BorderThickness = new(1);
    public Thickness Folder2BorderThickness
    {
        get => _folder2BorderThickness;
        private set { _folder2BorderThickness = value; OnPropertyChanged(nameof(Folder2BorderThickness)); }
    }

    private bool _folder1Valid;
    private bool _folder2Valid;

    private ManualMode _manualMode = ManualMode.AlreadyCreated;
    public bool IsManualAlreadyCreated
    {
        get => _manualMode == ManualMode.AlreadyCreated;
        set
        {
            if (!value || _manualMode == ManualMode.AlreadyCreated) return;
            _manualMode = ManualMode.AlreadyCreated;
            OnPropertyChanged(nameof(IsManualAlreadyCreated));
            OnPropertyChanged(nameof(IsManualWantCreate));
            RecomputeCommon();
        }
    }

    public bool IsManualWantCreate
    {
        get => _manualMode == ManualMode.WantCreate;
        set
        {
            if (!value || _manualMode == ManualMode.WantCreate) return;
            _manualMode = ManualMode.WantCreate;
            OnPropertyChanged(nameof(IsManualAlreadyCreated));
            OnPropertyChanged(nameof(IsManualWantCreate));
            RecomputeCommon();
        }
    }

    public bool ShowManualCreatedInputs => IsManualMode && IsManualAlreadyCreated;
    public bool ShowManualCreateNameInputs => UseFastMultiClient || (IsManualMode && IsManualWantCreate);

    private string _manualClient2Path = "";
    public string ManualClient2Path
    {
        get => _manualClient2Path;
        set
        {
            if (_manualClient2Path == value) return;
            _manualClient2Path = value;
            RecomputeManualPath1();
            RecomputeCommon();
            OnPropertyChanged(nameof(ManualClient2Path));
        }
    }

    private string _manualClient3Path = "";
    public string ManualClient3Path
    {
        get => _manualClient3Path;
        set
        {
            if (_manualClient3Path == value) return;
            _manualClient3Path = value;
            RecomputeManualPath2();
            RecomputeCommon();
            OnPropertyChanged(nameof(ManualClient3Path));
        }
    }

    private string _manualPath1Description = "";
    public string ManualPath1Description
    {
        get => _manualPath1Description;
        private set { if (_manualPath1Description == value) return; _manualPath1Description = value; OnPropertyChanged(nameof(ManualPath1Description)); }
    }

    private string _manualPath2Description = "";
    public string ManualPath2Description
    {
        get => _manualPath2Description;
        private set { if (_manualPath2Description == value) return; _manualPath2Description = value; OnPropertyChanged(nameof(ManualPath2Description)); }
    }

    private Brush _manualPath1BorderBrush = BrushNeutral;
    public Brush ManualPath1BorderBrush
    {
        get => _manualPath1BorderBrush;
        private set { _manualPath1BorderBrush = value; OnPropertyChanged(nameof(ManualPath1BorderBrush)); }
    }

    private Brush _manualPath2BorderBrush = BrushNeutral;
    public Brush ManualPath2BorderBrush
    {
        get => _manualPath2BorderBrush;
        private set { _manualPath2BorderBrush = value; OnPropertyChanged(nameof(ManualPath2BorderBrush)); }
    }

    private bool _manualPath1Valid;
    private bool _manualPath2Valid;

    public bool CanGoNext
    {
        get
        {
            if (UseFastMultiClient)
                return _folder1Valid && _folder2Valid;

            if (IsManualAlreadyCreated)
                return _manualPath1Valid || _manualPath2Valid;

            return _folder1Valid && _folder2Valid;
        }
    }

    public bool CanSkip => true;

    public string NextHintText
    {
        get
        {
            if (UseFastMultiClient)
            {
                return CanGoNext
                    ? "✅ 빠른 다클라 생성에 필요한 입력이 완료됐어요."
                    : "2/3클라 폴더명을 유효하게 입력하면 완료 버튼이 활성화돼요.";
            }

            if (IsManualAlreadyCreated)
            {
                return CanGoNext
                    ? "✅ 기존 다클라 경로가 확인됐어요."
                    : "기존 다클라 경로를 최소 1개 이상 유효하게 입력하면 완료 버튼이 활성화돼요.";
            }

            return CanGoNext
                ? "✅ 생성할 폴더명이 유효해요."
                : "생성할 2/3클라 폴더명을 유효하게 입력하면 완료 버튼이 활성화돼요.";
        }
    }

    public event EventHandler? StateChanged;

    public MultiClientStepPage()
    {
        InitializeComponent();

        var clientSettings = AppDataManager.LoadClientSettings();
        string installPath = SetupFlowState.InstallPath?.Trim() ?? "";

        _expectedFolderName = ExtractLastFolderName(installPath);
        _installParentPath = ExtractParentFolderPath(installPath);

        if (!string.IsNullOrWhiteSpace(clientSettings.Client2Path))
        {
            _manualClient2Path = clientSettings.Client2Path;
            _multiClientFolderName1 = ExtractLastFolderName(clientSettings.Client2Path);
        }

        if (!string.IsNullOrWhiteSpace(clientSettings.Client3Path))
        {
            _manualClient3Path = clientSettings.Client3Path;
            _multiClientFolderName2 = ExtractLastFolderName(clientSettings.Client3Path);
        }

        ResolveDriveCompatibility(installPath);

        bool savedUseSymbol = AppDataManager.LoadUseSymbol();
        _useFastMultiClient = IsDriveCompatible && savedUseSymbol;

        if (!string.IsNullOrWhiteSpace(_manualClient2Path) || !string.IsNullOrWhiteSpace(_manualClient3Path))
            _manualMode = ManualMode.AlreadyCreated;
        else
            _manualMode = ManualMode.WantCreate;

        RecomputeFolder1();
        RecomputeFolder2();
        RecomputePreviewPaths();
        RecomputeManualPath1();
        RecomputeManualPath2();
        RecomputeCommon();
    }

    public bool OnNext()
    {
        AppDataManager.SaveUseSymbol(UseFastMultiClient);

        string client2Path = "";
        string client3Path = "";

        if (UseFastMultiClient || IsManualWantCreate)
        {
            client2Path = Folder1PreviewPath;
            client3Path = Folder2PreviewPath;
        }
        else if (IsManualAlreadyCreated)
        {
            client2Path = ManualClient2Path.Trim();
            client3Path = ManualClient3Path.Trim();
        }

        AppDataManager.SaveClientSettings(new AppDataManager.ClientSettingsProfile
        {
            InstallPath = SetupFlowState.InstallPath?.Trim() ?? "",
            Client2Path = client2Path,
            Client3Path = client3Path
        });

        return true;
    }

    public void OnSkip() { }

    private void ResolveDriveCompatibility(string installPath)
    {
        string format = "알 수 없음";

        try
        {
            string root = Path.GetPathRoot(installPath) ?? "";
            if (!string.IsNullOrWhiteSpace(root))
            {
                var driveInfo = new DriveInfo(root);
                format = driveInfo.DriveFormat;
            }
        }
        catch
        {
            format = "알 수 없음";
        }

        DriveFormatText = $"현재 드라이브 포맷: {format}";

        bool compatible = string.Equals(format, "NTFS", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(format, "UDF", StringComparison.OrdinalIgnoreCase);

        IsDriveCompatible = compatible;
        OnPropertyChanged(nameof(IsFastOptionEditable));
        OnPropertyChanged(nameof(ShowIncompatibleHelp));

        if (compatible)
        {
            DriveCompatibilityText = "✅ 빠른 다클라 생성 호환 드라이브입니다.";
            DriveCompatibilityBrush = BrushValid;
        }
        else
        {
            DriveCompatibilityText = "❌ 빠른 다클라 생성이 불가능한 드라이브입니다.";
            DriveCompatibilityBrush = BrushInvalid;
        }
    }

    private void RecomputeFolder1()
    {
        ComputeFolderValidation(MultiClientFolderName1, MultiClientFolderName2, out _folder1Valid, out string description);

        Folder1Description = description;
        Folder1BorderBrush = _folder1Valid ? BrushValid : BrushInvalid;
        Folder1BorderThickness = new Thickness(_folder1Valid ? 2 : 1);
    }

    private void RecomputeFolder2()
    {
        ComputeFolderValidation(MultiClientFolderName2, MultiClientFolderName1, out _folder2Valid, out string description);

        Folder2Description = description;
        Folder2BorderBrush = _folder2Valid ? BrushValid : BrushInvalid;
        Folder2BorderThickness = new Thickness(_folder2Valid ? 2 : 1);
    }

    private void RecomputePreviewPaths()
    {
        string name1 = (MultiClientFolderName1 ?? "").Trim();
        string name2 = (MultiClientFolderName2 ?? "").Trim();

        Folder1PreviewPath = _folder1Valid && !string.IsNullOrWhiteSpace(_installParentPath)
            ? Path.Combine(_installParentPath, name1)
            : "";

        Folder2PreviewPath = _folder2Valid && !string.IsNullOrWhiteSpace(_installParentPath)
            ? Path.Combine(_installParentPath, name2)
            : "";
    }

    private void RecomputeManualPath1()
    {
        ValidateManualClientPath(ManualClient2Path, out _manualPath1Valid, out string description);
        ManualPath1Description = description;
        ManualPath1BorderBrush = _manualPath1Valid ? BrushValid : BrushInvalid;
    }

    private void RecomputeManualPath2()
    {
        ValidateManualClientPath(ManualClient3Path, out _manualPath2Valid, out string description);
        ManualPath2Description = description;
        ManualPath2BorderBrush = _manualPath2Valid ? BrushValid : BrushInvalid;
    }

    private void RecomputeCommon()
    {
        OnPropertyChanged(nameof(IsManualMode));
        OnPropertyChanged(nameof(ShowManualCreatedInputs));
        OnPropertyChanged(nameof(ShowManualCreateNameInputs));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanSkip));
        OnPropertyChanged(nameof(NextHintText));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ComputeFolderValidation(string input, string otherInput, out bool isValid, out string description)
    {
        string folderName = (input ?? "").Trim();

        if (string.IsNullOrWhiteSpace(folderName))
        {
            isValid = false;
            description = "❌ 폴더명이 비어있어요.";
            return;
        }

        if (!IsValidFolderName(folderName))
        {
            isValid = false;
            description = "❌ 폴더명으로 사용할 수 없는 문자 또는 형식이에요.";
            return;
        }

        string otherFolderName = (otherInput ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(otherFolderName) &&
            string.Equals(folderName, otherFolderName, StringComparison.OrdinalIgnoreCase))
        {
            isValid = false;
            description = "❌ 2클라/3클라 폴더명은 서로 같으면 안돼요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_expectedFolderName))
        {
            isValid = false;
            description = "❌ 이전 단계 설치 경로를 확인할 수 없어요.";
            return;
        }

        if (string.Equals(folderName, _expectedFolderName, StringComparison.OrdinalIgnoreCase))
        {
            isValid = false;
            description = $"❌ 설치 경로의 마지막 폴더명 '{_expectedFolderName}' 과 같으면 안돼요.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_installParentPath))
        {
            isValid = false;
            description = "❌ 설치 경로 상위 폴더를 확인할 수 없어요.";
            return;
        }

        isValid = true;
        description = "✅ 유효해요.";
    }

    private static void ValidateManualClientPath(string inputPath, out bool isValid, out string description)
    {
        string path = (inputPath ?? "").Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            isValid = false;
            description = "❌ 경로가 비어있어요.";
            return;
        }

        if (!Directory.Exists(path))
        {
            isValid = false;
            description = "❌ 폴더가 존재하지 않아요.";
            return;
        }

        string runExe = Path.Combine(path, "Run.exe");
        if (!File.Exists(runExe))
        {
            isValid = false;
            description = "❌ Run.exe를 찾지 못했어요.";
            return;
        }

        string charDir = Path.Combine(path, "char");
        if (Directory.Exists(charDir) && IsSymlinkCheckSupported(path) && IsReparsePointDirectory(charDir))
        {
            isValid = false;
            description = "❌ char 폴더가 심볼릭 링크(또는 정션)로 보입니다.";
            return;
        }

        isValid = true;
        description = "✅ 유효한 경로예요.";
    }

    private static string ExtractLastFolderName(string installPath)
    {
        string path = (installPath ?? "").Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(path)) return "";

        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return "";
        }
    }

    private static string ExtractParentFolderPath(string installPath)
    {
        string path = (installPath ?? "").Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(path)) return "";

        try
        {
            return Directory.GetParent(path)?.FullName ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static bool IsValidFolderName(string folderName)
    {
        if (folderName.EndsWith(' ') || folderName.EndsWith('.'))
            return false;

        if (folderName.Any(char.IsControl))
            return false;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        return folderName.IndexOfAny(invalidChars) < 0;
    }

    private static bool IsSymlinkCheckSupported(string anyPathInDrive)
    {
        try
        {
            string root = Path.GetPathRoot(anyPathInDrive) ?? "";
            if (string.IsNullOrWhiteSpace(root))
                return false;

            var drive = new DriveInfo(root);
            return !string.Equals(drive.DriveFormat, "FAT32", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(drive.DriveFormat, "exFAT", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReparsePointDirectory(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch
        {
            return false;
        }
    }

    private async void OnPickClient2Path(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        button.IsEnabled = false;
        string? path = await PickFolderAsync(button);
        if (!string.IsNullOrWhiteSpace(path))
            ManualClient2Path = path;
        button.IsEnabled = true;
    }

    private async void OnPickClient3Path(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;

        button.IsEnabled = false;
        string? path = await PickFolderAsync(button);
        if (!string.IsNullOrWhiteSpace(path))
            ManualClient3Path = path;
        button.IsEnabled = true;
    }

    private static async System.Threading.Tasks.Task<string?> PickFolderAsync(Button button)
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

    private void OnHelpLinkClicked(object sender, RoutedEventArgs e)
    {
        // TODO: 도움말 문서가 준비되면 연결할 URL로 교체합니다.
    }
}
