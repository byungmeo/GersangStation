# 브라우저 세션 수명과 호출 규칙

`MainWindow`가 단 하나의 WebView2와 `BrowserSession`, `WebViewManager`를 생성합니다.
WebView2는 창의 `BrowserSurface` 안에 계속 붙어 있고 섹션 전환 시 표시 여부만 바뀝니다.
`BrowserPage`는 첫 브라우저 진입 시 생성되는 도구 모음/즐겨찾기 UI입니다.
페이지 생성 여부는 로그인·게임 실행·WebView2 초기화의 전제 조건이 아닙니다.

## 책임

- `BrowserSession`: UI 스레드 접근, 공유 초기화 Task, Core 준비 상태, 문서 준비 상태,
  크기 조정, 메모리 목표, 프로세스 실패 감지, 취소, 컨트롤 Close.
- `WebViewManager`: 공식 홈페이지 로그인/OTP/게임 실행, 초기 홈페이지 동기화,
  URL/HTML 탐색 요청과 탐색 실패 시 실행 슬롯 정리.
- `BrowserPage`: 창의 매니저 참조를 연결하고 UI 이벤트 구독만 해제합니다.
- `MainWindow.OnClosed`: 페이지 구독 해제 → 매니저 해제 → 세션 해제 → GameStarter 해제.
  트레이 숨김과 섹션 전환은 종료가 아닙니다.

## 상태와 비동기 작업

`Created → Initializing → Ready` 또는 `Faulted`; 최종 종료는 `Disposed`입니다.
초기화는 생성자에서 시작하지 않으며, 시작 시 또는 명시적 브라우저 명령에서 동일한 Task를 기다립니다.
초기화 실패 원인은 보존하고 기존 상세 오류 처리기로 보고합니다. 실패한 초기화를 암묵적으로 반복하지 않습니다.

Core 준비와 DOM 준비는 다릅니다. 로그인 스크립트는 DOM이 준비된 후에만 실행합니다.
쿠키/로그인 스크립트/OTP dialog await 뒤에는 NavigationId를 검사해 이전 문서 결과로 현재 상태를 덮어쓰지 않습니다.
탐색을 발생시키는 최종 스크립트는 정상적으로 문서를 바꿀 수 있으므로 재실행하지 않습니다.

초기 홈페이지 동기화 전 URL/HTML 이동은 마지막 요청 하나를 보관합니다.
홈페이지 탐색 실패 시에도 요청을 풀어 도움말과 로컬 HTML 표시가 막히지 않게 합니다.
로그인/게임 실행 명령은 화면 탐색 요청처럼 덮어쓰거나 장애 후 자동 재생하면 안 됩니다.
이벤트 기반 로그인 흐름은 자기 자신을 다시 호출하므로 전체 흐름을 하나의 semaphore로 잠그면 안 됩니다.

자동 활성화 새로고침은 준비된 유휴 문서에만 적용하고, 수동 새로고침은 초기화 Task를 기다립니다.
메모리 목표는 초기화 전에도 저장하며 Core가 준비된 뒤 적용합니다.
세션 종료/프로세스 실패 시 소켓과 스크립트 대기를 취소합니다.

## 복구 범위

GPU/utility 장애는 WebView2 런타임의 자동 복구에 맡깁니다.
브라우저/주 렌더러 프로세스 종료는 한 번 보고하고 진행 중 로그인/게임 실행을 취소합니다.
현재는 앱 재시작으로 복구합니다. 컨트롤 자동 재생성이나 로그인 자동 재시도는 구현하지 않았습니다.
렌더러 무응답은 진단 로그만 남깁니다.

## 회귀 확인

- 브라우저 페이지를 열지 않은 상태에서 게임 실행과 도움말 이동.
- 초기화 중 Alt-Tab, 초기화 중 종료, 트레이 숨김/복원.
- 연속 URL/HTML 요청, 초기 홈페이지 네트워크 실패 후 로컬 안내 표시.
- 로그인/OTP 중 취소 또는 다른 문서 이동; 실행 슬롯과 소켓 정리.
- 창 너비 변경 시 1152 CSS px 배율, 즐겨찾기/주소창 동기화.
- WebView2 프로세스 종료 후 오류 1회 보고, 실행 자동 재시도 없음.

실제 WebView2/로그인/게임 실행 검증은 앱 실행 환경에서 별도로 수행해야 합니다.

참조: [초기화](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/webview2),
[UI 스레드 및 재진입](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/threading-model),
[프로세스 실패](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/process-related-events).
