namespace Core.Models;

/// <summary>화면 배율과 독립적인 일반 창 크기와 마지막 최대화 상태입니다.</summary>
public readonly record struct WindowPlacementSettings(double WidthInDips, double HeightInDips, bool IsMaximized);
