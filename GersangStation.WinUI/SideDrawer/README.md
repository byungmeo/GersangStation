# SideDrawer

앱에 종속되지 않는 WinUI 3 컨트롤 라이브러리입니다. `Core`/`GersangStation`/Shared를 참조하지 않습니다.
.NET 10, Windows 10 1809+, x64와 저장소의 중앙 Windows App SDK 버전 정책을 사용합니다.

## 선언하고 열기

TeachingTip처럼 기존 XAML의 자식으로 선언합니다. `DrawerHost` 래퍼나 Resources 등록은 필요 없습니다.
선언 자체는 레이아웃 공간을 차지하지 않고, 열릴 때 내부 표시 부분만 별도 Popup으로 표시합니다.
선언 위치를 옮기거나 기존 화면의 자식 컨트롤을 재배치하지 않습니다.

```xml
<Grid x:Name="Root" xmlns:ui="using:SideDrawer.Controls">
    <!-- 기존 사이드바/페이지 UI -->
    <ui:Drawer x:Name="Editor"
               Placement="Right" PanelLength="440" Modal="True"
               Closing="Editor_Closing" InteractionFailed="Editor_InteractionFailed">
        <StackPanel Spacing="16">
            <TextBlock Text="프로필 편집" Style="{StaticResource TitleTextBlockStyle}" />
            <TextBox Header="표시 이름" AutomationProperties.AutomationId="EditorDisplayName" />
            <Button Content="닫기" Click="CloseEditor_Click" AutomationProperties.AutomationId="CloseEditor" />
        </StackPanel>
    </ui:Drawer>
</Grid>
```

```csharp
Editor.IsOpen = true;
Editor.IsOpen = false; // Closing에서 취소하면 true로 복구

// 완료를 기다려야 하는 코드에는 비동기 API도 제공
await Editor.OpenAsync();
bool closed = await Editor.CloseAsync();
```

`IsOpen`은 TwoWay 바인딩도 가능합니다. `IsOpen="True"`를 Loaded 이전에 설정하면 Loaded 후 엽니다.
`OpenAsync()`는 선언과 Host가 로드된 다음 호출합니다. 닫았다 다시 열어도 콘텐츠/입력값은 유지합니다.
폼 데이터 초기화·저장·폐기는 컨트롤이 아닌 사용 측의 책임입니다.

## Host와 모달 범위

- `Host`를 지정하면 해당 FrameworkElement의 표시 영역을 사용합니다: `Host="{x:Bind Root}"`.
- 생략하면 선언 위치의 가장 가까운 Panel(Grid/StackPanel 등), Border, ContentControl(Page/UserControl 포함)을 탐색합니다.
- 중첩 Drawer에서는 기본적으로 부모 Drawer의 Host를 상속합니다. 자식에 Host를 지정하면 그 범위를 사용합니다.
- 패널 크기/등장 위치, 흐림 처리, 배경 입력 차단은 모두 같은 Host 범위를 사용합니다.
- 창 전체를 차단하려면 사이드바와 페이지를 모두 담는 최상위 Grid를 Host로 지정하거나 그 안에 선언합니다.
- 작은 Host 밖의 UI는 흐려지거나 차단되지 않으며, Host 밖 클릭은 닫기 요청으로 처리하지 않습니다.
- `Modal=false`이면 흐림/배경 입력 차단 없이 표시하고, Host 내부의 패널 밖 클릭은 입력을 통과시키면서 닫기를 요청합니다.

Host는 같은 UI 스레드/XamlRoot의 로드된 컨테이너여야 합니다. 열린 동안 Host/ParentDrawer/Template을 변경하지 마세요.
회전/기울임을 적용하지 않은 일반 레이아웃 컨테이너가 대상입니다. 창 경계 밖 부분은 클리핑합니다.
Host의 이동/크기 변경은 표시 중에 추적합니다. 기존 콘텐츠의 IsEnabled 값이나 바인딩을 덮어쓰지 않습니다.

## 닫기 취소와 비동기 확인

`IsOpen=false`, `CloseAsync()`, 패널 밖 클릭, Escape 모두 같은 `Closing` 이벤트를 거칩니다.
`Handled=true`이면 닫지 않으며 `IsOpen`을 true로 복원합니다. 중복 요청은 진행 중인 동일 Task를 반환합니다.

```csharp
private async void Editor_Closing(object? sender, DrawerClosingEventArgs e)
{
    if (!Draft.HasChanges) return;
    using var deferral = e.GetDeferral(); // 첫 await 이전에 획득
    e.Handled = true; // 확인/저장이 실패하면 열린 상태 유지
    try
    {
        var decision = await ConfirmUnsavedChangesAsync();
        if (decision == Decision.Save) await SaveDraftAsync();
        if (decision is Decision.Save or Decision.Discard) e.Handled = false;
    }
    catch (Exception exception) { await ReportSaveFailureAsync(exception); }
}
```

Deferral은 반드시 Complete/Dispose하세요. 이벤트가 반환된 뒤 새 deferral을 얻는 것은 허용하지 않습니다.
Closing 안에서 같은 Drawer의 CloseAsync를 await하면 자기 자신을 기다리므로 사용하지 마세요.
비동기 API의 오류는 반환 Task로, IsOpen 변경/외부 클릭/Escape에서 시작된 오류는 Drawer의
`InteractionFailed` 이벤트와 `LastInteractionException`으로 전달합니다. 앱의 오류 창/로깅에 연결하세요.

## Nested

부모 콘텐츠 안에 자식을 직접 선언하면 부모를 자동으로 찾습니다.
같은 Grid에 나란히 선언하려면 자식에 `ParentDrawer="{x:Bind Editor}"`를 지정하세요.

```xml
<ui:Drawer x:Name="Editor" Host="{x:Bind Root}">
    <Grid>
        <!-- 부모 폼 -->
        <ui:Drawer x:Name="Confirmation" PanelLength="320" Placement="Bottom">
            <!-- 확인 폼 -->
        </ui:Drawer>
    </Grid>
</ui:Drawer>
```

부모를 연 뒤 `Confirmation.IsOpen=true`로 자식을 엽니다. 부모는 콘텐츠와 입력값을 유지합니다.
자식이 열려 있으면 부모의 닫기는 false로 거절되고 IsOpen=true로 복원됩니다.
자식을 닫은 뒤 부모를 닫으세요. 부모 Closing deferral 중 확인용 자식을 여는 것도 가능합니다.
모달 자식은 자기 Host 범위의 부모 입력을 막고, non-modal 자식은 부모 입력을 허용합니다.
닫기 승인 후 애니메이션 동안은 폼 편집을 막되 오버레이 입력 차단은 종료까지 유지합니다.
닫힌 뒤 이전 포커스를 복원하며, non-modal 외부 클릭으로 닫을 때는 클릭한 배경의 포커스를 유지합니다.

## 설정

| 속성 | 기본값 | 의미 |
| --- | --- | --- |
| Host | null | 패널/흐림/입력 제한 영역 자동 탐색 |
| ParentDrawer | null | 콘텐츠 안의 활성 부모 자동 탐색 |
| IsOpen | false | 열기/닫기 요청, TwoWay 바인딩 가능 |
| State | Closed | 실제 Closed/Opening/Open/ClosePending/Closing 상태 관찰 |
| Placement | Right | Left/Right/Top/Bottom |
| PanelLength | 400 DIP | 여백 포함 좌우: 너비, 위아래: 높이. 다른 축은 Host 전체 |
| PanelMargin | 8 DIP | 패널의 사방 바깥 여백. 오버레이와 모달 입력 범위는 Host 전체 유지 |
| CornerRadius | 4 DIP | 패널의 모서리 반경 |
| Modal | true | Host 배경 입력 차단 및 Acrylic 흐림 |
| CloseOnOutsideClick / CloseOnEscape | true | 외부 클릭/내부에서 소비되지 않은 Escape로 닫기 요청 |
| AnimationDuration | 240 ms | 0이면 즉시 전환, Windows 애니메이션 설정 존중 |
| OverlayBrush | 테마 Acrylic | 모달 배경 |

Content/ContentTemplate/ContentTemplateSelector로 임의 콘텐츠를 표시합니다.
Padding/Background/Foreground/BorderBrush/BorderThickness/CornerRadius는 패널 외관에 적용됩니다.
Light/Dark/HighContrast를 지원합니다. OS 투명 효과가 꺼져 있으면 Acrylic은 단색 fallback을 사용합니다.
Swipe Handle, Snap Points, Responsive 방향 전환은 구현하지 않습니다.
[shadcn Drawer](https://ui.shadcn.com/docs/components/base/drawer)는 동작 참고 자료이며 웹 라이브러리 의존성은 없습니다.

Host/선언이 Unloaded되면 자식부터 제거하고 애니메이션과 대기 중인 deferral을 중단합니다.
이 수명 종료에는 Closing 취소가 적용되지 않으며 Closed.Reason은 HostUnloaded입니다.
창 종료 시 데이터 보존은 사용 앱에서 처리해야 합니다.

입력 차단은 Host 영역의 일반 포인터/키보드 UI에 적용됩니다. OS 제목 표시줄, 전역 핫키, UI 자동화로 직접
호출하는 명령, 코드에서 직접 실행하는 탐색까지 가로채는 보안 경계는 아닙니다.
그러한 명령은 `Drawer.IsInputBlockedFor(대상요소)`로 확인하세요.
ComboBox/Flyout/ContentDialog의 별도 팝업은 WinUI 자체 입력 처리를 사용합니다.

## 검증

전체 솔루션의 Visual Studio MSBuild 빌드 및 임시 검사 코드를 사용합니다.
모의 UI 요소로 하는 상태/수명/배치 검사는 실제 WinUI 렌더링 검증을 대신하지 않습니다.
실제 창의 Acrylic, 포인터/Tab/별도 팝업, DPI/이동/애니메이션 및 WebView2 위 표시에는 실행 검증이 필요합니다.
