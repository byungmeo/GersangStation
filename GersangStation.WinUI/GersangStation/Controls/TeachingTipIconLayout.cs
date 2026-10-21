using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace GersangStation.Controls;

/// <summary>기본 TeachingTip 템플릿을 유지하면서 상태 아이콘을 제목 행에 정렬합니다.</summary>
public static class TeachingTipIconLayout
{
    public static readonly DependencyProperty AlignWithTitleProperty = DependencyProperty.RegisterAttached(
        "AlignWithTitle", typeof(bool), typeof(TeachingTipIconLayout),
        new PropertyMetadata(false, OnAlignWithTitleChanged));

    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(object), typeof(TeachingTipIconLayout), new PropertyMetadata(null));

    public static bool GetAlignWithTitle(DependencyObject element) => (bool)element.GetValue(AlignWithTitleProperty);
    public static void SetAlignWithTitle(DependencyObject element, bool value) => element.SetValue(AlignWithTitleProperty, value);

    private static void OnAlignWithTitleChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is not TeachingTip tip) return;
        if (tip.GetValue(StateProperty) is AlignmentState previous) previous.Detach();
        tip.SetValue(StateProperty, (bool)args.NewValue ? new AlignmentState(tip) : null);
    }

    private sealed class AlignmentState
    {
        private readonly TeachingTip _tip;
        private readonly long _iconToken;
        private readonly long _fontToken;
        private IconElement? _icon;

        public AlignmentState(TeachingTip tip)
        {
            _tip = tip;
            _iconToken = tip.TemplateSettings.RegisterPropertyChangedCallback(
                TeachingTipTemplateSettings.IconElementProperty, (_, _) => Update());
            _fontToken = tip.RegisterPropertyChangedCallback(Control.FontSizeProperty, (_, _) => Update());
            Update();
        }

        public void Detach()
        {
            _tip.TemplateSettings.UnregisterPropertyChangedCallback(TeachingTipTemplateSettings.IconElementProperty, _iconToken);
            _tip.UnregisterPropertyChangedCallback(Control.FontSizeProperty, _fontToken);
            if (_icon is not null) _icon.Loaded -= OnIconLoaded;
        }

        private void OnIconLoaded(object sender, RoutedEventArgs args) => Update();

        private void Update()
        {
            IconElement? icon = _tip.TemplateSettings.IconElement;
            if (!ReferenceEquals(_icon, icon))
            {
                if (_icon is not null) _icon.Loaded -= OnIconLoaded;
                _icon = icon;
                if (icon is not null) icon.Loaded += OnIconLoaded;
            }
            if (icon is null) return;

            // 20px 슬롯은 유지해 본문·버튼의 기존 들여쓰기를 보존합니다.
            // 글리프만 제목과 동일한 크기(기본 14px)로 표시합니다.
            icon.MinWidth = 20;
            icon.MinHeight = 20;
            if (icon is FontIcon fontIcon) fontIcon.FontSize = _tip.FontSize;

            // IconElement는 팝업 안에 있으므로, 실제로 로드된 아이콘에서 부모를 찾습니다.
            for (DependencyObject? parent = VisualTreeHelper.GetParent(icon); parent is not null;
                parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ContentPresenter { Name: "IconPresenter" } presenter)
                {
                    presenter.VerticalAlignment = VerticalAlignment.Top;
                    break;
                }
            }
        }
    }
}
