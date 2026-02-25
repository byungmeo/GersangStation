using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace GersangStation.Setup;

public sealed partial class SetupWindow : Window
{
    public event EventHandler? SetupCompleted;

    private enum SetupStep
    {
        Welcome = 0,
        PathSelect = 1,
        MultiClient = 2,
        AccountSetting = 3,
    }

    private SetupStep _currentStep = SetupStep.Welcome;

    // ✅ 현재 페이지의 PropertyChanged 구독 관리
    private INotifyPropertyChanged? _currentNotify;
    private bool _allowForceClose;

    public SetupWindow()
    {
        InitializeComponent();

        SetupFlowState.Reset();

        Frame_SetupStep.Navigated += Frame_SetupStep_Navigated;
        AppWindow.Closing += OnAppWindowClosing;

        NavigateToStep(SetupStep.Welcome, isForward: true);
    }

    private void Frame_SetupStep_Navigated(object sender, NavigationEventArgs e)
    {
        // ✅ 이전 페이지 구독 해제
        if (_currentNotify != null)
        {
            _currentNotify.PropertyChanged -= StepPage_PropertyChanged;
            _currentNotify = null;
        }

        // ✅ 새 페이지 구독
        if (Frame_SetupStep.Content is INotifyPropertyChanged npc)
        {
            _currentNotify = npc;
            _currentNotify.PropertyChanged += StepPage_PropertyChanged;
        }

        // 페이지가 실제로 바뀐 뒤 버튼 상태 재평가
        UpdateStepActionButtons();
    }

    private void StepPage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ✅ 공통 버튼에 영향 있는 값만 갱신
        if (e.PropertyName == nameof(ISetupStepPage.CanGoNext) ||
            e.PropertyName == nameof(ISetupStepPage.CanSkip) ||
            e.PropertyName == nameof(IBusySetupStepPage.IsBusy) ||
            string.IsNullOrEmpty(e.PropertyName))
        {
            UpdateStepActionButtons();
        }
    }

    private void NavigateToStep(SetupStep step, bool isForward)
    {
        _currentStep = step;

        Type pageType = step switch
        {
            SetupStep.Welcome => typeof(WelcomeStepPage),
            SetupStep.PathSelect => typeof(SetupGameStepPage),
            SetupStep.MultiClient => typeof(MultiClientStepPage),
            SetupStep.AccountSetting => typeof(AccountSettingPage),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
        };

        NavigationTransitionInfo transition = isForward
            ? new DrillInNavigationTransitionInfo()
            : new SlideNavigationTransitionInfo
            {
                Effect = SlideNavigationTransitionEffect.FromLeft
            };

        Frame_SetupStep.Navigate(pageType, null, transition);

        UpdateShellUi();
    }

    private void UpdateShellUi()
    {
        switch (_currentStep)
        {
            case SetupStep.Welcome:
                TextBlock_StepTitle.Text = "환영합니다";
                Button_Back.IsEnabled = false;
                Button_Skip.Visibility = Visibility.Collapsed;
                Button_Next.Content = "시작하기";
                break;

            case SetupStep.PathSelect:
                TextBlock_StepTitle.Text = "거상 설치 위치 확인";
                Button_Back.IsEnabled = true;
                Button_Skip.Visibility = Visibility.Visible;
                Button_Next.Content = "다음";
                break;

            case SetupStep.MultiClient:
                TextBlock_StepTitle.Text = "다클 폴더명 설정 (선택)";
                Button_Back.IsEnabled = true;
                Button_Skip.Visibility = Visibility.Visible;
                Button_Next.Content = "다음";
                break;

            case SetupStep.AccountSetting:
                TextBlock_StepTitle.Text = "계정 설정 (선택)";
                Button_Back.IsEnabled = true;
                Button_Skip.Visibility = Visibility.Visible;
                Button_Next.Content = "완료";
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void UpdateStepActionButtons()
    {
        if (Frame_SetupStep.Content is ISetupStepPage stepPage)
        {
            bool isBusy = stepPage is IBusySetupStepPage busyStepPage && busyStepPage.IsBusy;

            Button_Back.IsEnabled = _currentStep != SetupStep.Welcome && !isBusy;
            Button_Next.IsEnabled = stepPage.CanGoNext && !isBusy;
            Button_Skip.IsEnabled = stepPage.CanSkip && !isBusy;
            return;
        }

        // 인터페이스 미구현 페이지 대비 기본값
        Button_Back.IsEnabled = _currentStep != SetupStep.Welcome;
        Button_Next.IsEnabled = true;
        Button_Skip.IsEnabled = Button_Skip.Visibility == Visibility.Visible;
    }

    private void Button_Back_Click(object sender, RoutedEventArgs e)
    {
        switch (_currentStep)
        {
            case SetupStep.Welcome:
                return;

            case SetupStep.PathSelect:
                NavigateToStep(SetupStep.Welcome, isForward: false);
                return;

            case SetupStep.MultiClient:
                NavigateToStep(SetupStep.PathSelect, isForward: false);
                return;

            case SetupStep.AccountSetting:
                NavigateToStep(SetupStep.MultiClient, isForward: false);
                return;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void Button_Skip_Click(object sender, RoutedEventArgs e)
    {
        if (Frame_SetupStep.Content is ISetupStepPage stepPage)
        {
            if (!stepPage.CanSkip)
                return;

            stepPage.OnSkip();
        }

        HandleNext();
    }

    private async void Button_Next_Click(object sender, RoutedEventArgs e)
    {
        if (Frame_SetupStep.Content is ISetupStepPage stepPage)
        {
            if (!stepPage.CanGoNext)
                return;

            bool canProceed;
            if (stepPage is IAsyncSetupStepPage asyncStepPage)
                canProceed = await asyncStepPage.OnNextAsync();
            else
                canProceed = stepPage.OnNext();

            if (!canProceed)
                return;
        }

        HandleNext();
    }

    private void HandleNext()
    {
        switch (_currentStep)
        {
            case SetupStep.Welcome:
                NavigateToStep(SetupStep.PathSelect, isForward: true);
                return;

            case SetupStep.PathSelect:
                if (SetupFlowState.ShouldAutoSkipMultiClient)
                {
                    SetupCompleted?.Invoke(this, EventArgs.Empty);
                    return;
                }

                NavigateToStep(SetupStep.MultiClient, isForward: true);
                return;

            case SetupStep.MultiClient:
                NavigateToStep(SetupStep.AccountSetting, isForward: true);
                return;

            case SetupStep.AccountSetting:
                SetupCompleted?.Invoke(this, EventArgs.Empty);
                return;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowForceClose)
            return;

        if (Frame_SetupStep.Content is not IBusySetupStepPage busyStepPage || !busyStepPage.IsBusy)
            return;

        args.Cancel = true;
        var deferral = args.GetDeferral();

        try
        {
            bool cancelled = await busyStepPage.ConfirmCancelBusyWorkAsync(
                title: "프로그램 종료",
                message: "현재 다운로드/압축 해제가 진행 중입니다. 정말 프로그램을 종료하시겠습니까?",
                primaryButtonText: "종료",
                closeButtonText: "취소");

            if (cancelled)
            {
                _allowForceClose = true;
                this.Close();
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

}
