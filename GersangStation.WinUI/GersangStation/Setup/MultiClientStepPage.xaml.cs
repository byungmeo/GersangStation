using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace GersangStation.Setup;

public sealed partial class MultiClientStepPage : Page, ISetupStepPage, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static readonly Brush BrushInvalid = new SolidColorBrush(Colors.IndianRed);
    private static readonly Brush BrushValid = new SolidColorBrush(Colors.SeaGreen);

    private string _expectedFolderName = "";

    private string _multiClientFolderName1 = "";
    public string MultiClientFolderName1
    {
        get => _multiClientFolderName1;
        set
        {
            if (_multiClientFolderName1 == value) return;
            _multiClientFolderName1 = value;
            // 두 입력은 상호 제약(서로 달라야 함)이 있어서 같이 재검증합니다.
            RecomputeFolder1();
            RecomputeFolder2();
            RecomputeCommon();
            OnPropertyChanged(nameof(MultiClientFolderName1));
        }
    }

    private string _multiClientFolderName2 = "";
    public string MultiClientFolderName2
    {
        get => _multiClientFolderName2;
        set
        {
            if (_multiClientFolderName2 == value) return;
            _multiClientFolderName2 = value;
            // 두 입력은 상호 제약(서로 달라야 함)이 있어서 같이 재검증합니다.
            RecomputeFolder1();
            RecomputeFolder2();
            RecomputeCommon();
            OnPropertyChanged(nameof(MultiClientFolderName2));
        }
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

    private Brush _folder1BorderBrush = BrushInvalid;
    public Brush Folder1BorderBrush
    {
        get => _folder1BorderBrush;
        private set { _folder1BorderBrush = value; OnPropertyChanged(nameof(Folder1BorderBrush)); }
    }

    private Brush _folder2BorderBrush = BrushInvalid;
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

    public bool CanGoNext => _folder1Valid && _folder2Valid;

    public bool CanSkip => true;

    public string NextHintText =>
        CanGoNext
            ? "✅ 두 폴더명이 유효해요. 완료 버튼으로 진행할 수 있어요."
            : "두 폴더명을 모두 유효하게 입력하면 완료 버튼이 활성화돼요.";

    public event EventHandler? StateChanged;

    public MultiClientStepPage()
    {
        InitializeComponent();

        _expectedFolderName = ExtractLastFolderName(SetupFlowState.InstallPath);

        RecomputeFolder1();
        RecomputeFolder2();
        RecomputeCommon();
    }

    public bool OnNext() => true;

    public void OnSkip() { }

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

    private void RecomputeCommon()
    {
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

        isValid = true;
        description = "✅ 유효해요.";
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

    private static bool IsValidFolderName(string folderName)
    {
        if (folderName.EndsWith(' ') || folderName.EndsWith('.'))
            return false;

        if (folderName.Any(char.IsControl))
            return false;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        return folderName.IndexOfAny(invalidChars) < 0;
    }
}
