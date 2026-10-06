using Microsoft.UI.Xaml.Controls;
using System;
using Windows.Foundation;

namespace GersangStation.Controls;

/// <summary>
/// 첫 자식(헤더 없는 ComboBox)이 높이를 결정하고, 나머지 액션을 같은 높이에 배치합니다.
/// 버튼은 자연 너비만 차지하며 남은 너비를 ComboBox에 할당합니다.
/// </summary>
public sealed class AccountActionPanel : Panel
{
    private const double Spacing = 4;
    private double[] _actionWidths = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0) return default;

        if (_actionWidths.Length != Children.Count - 1)
            _actionWidths = new double[Children.Count - 1];

        double actionsWidth = Spacing * (Children.Count - 1);
        for (int i = 1; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _actionWidths[i - 1] = Children[i].DesiredSize.Width;
            actionsWidth += _actionWidths[i - 1];
        }

        var input = Children[0];
        input.Measure(new Size(Math.Max(0, availableSize.Width - actionsWidth), availableSize.Height));
        double height = input.DesiredSize.Height;

        // 버튼의 내용/스타일은 입력의 높이를 늘릴 수 없습니다.
        for (int i = 1; i < Children.Count; i++)
            Children[i].Measure(new Size(_actionWidths[i - 1], height));

        return new Size(input.DesiredSize.Width + actionsWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0) return finalSize;

        double actionsWidth = Spacing * (Children.Count - 1);
        for (int i = 1; i < Children.Count; i++)
            actionsWidth += _actionWidths[i - 1];

        double inputWidth = Math.Max(0, finalSize.Width - actionsWidth);
        Children[0].Arrange(new Rect(0, 0, inputWidth, finalSize.Height));

        double x = inputWidth;
        for (int i = 1; i < Children.Count; i++)
        {
            x += Spacing;
            double width = _actionWidths[i - 1];
            Children[i].Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }
}
