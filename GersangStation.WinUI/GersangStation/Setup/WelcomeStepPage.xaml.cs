using Microsoft.UI.Xaml.Controls;
using System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GersangStation.Setup;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class WelcomeStepPage : Page, ISetupStepPage
{
    public bool CanGoNext => true;

    public bool CanSkip => false;

    public event EventHandler? StateChanged;

    public WelcomeStepPage()
    {
        InitializeComponent();
    }

    public bool OnNext()
    {
        // 이 페이지를 떠난 뒤 해야 할 일
        return true;
    }

    public void OnSkip()
    {
        // 이 페이지를 스킵한 뒤 해야 할 일
    }
}
