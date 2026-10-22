# 앱 내 브라우저 설정 및 데이터 정리

## 삭제 범위

| 작업 | WebView2 삭제 범위 |
| --- | --- |
| 쿠키 및 캐시 정리 | `AllSite` + `DiskCache` + `BrowsingHistory` + `DownloadHistory` |
| 브라우저 데이터 초기화 | `AllProfile` + 사이트 권한의 명시적 기본값 복원 + 서버 인증서 오류 허용 결정 초기화 |

`AllSite`에는 쿠키, DOM 저장소(IndexedDB/LocalStorage 등), CacheStorage, 서비스 워커가 포함된다. `AllProfile`에는 자동완성과 웹사이트 저장 암호 및 프로필 설정도 포함된다. 두 작업 모두 전체 기간을 대상으로 하며 현재 WebView의 뒤로/앞으로 탐색 목록도 비운다.

초기화는 공식 프로필 삭제 API의 범위이다. UDF 폴더 자체, 외부 브라우저, OS/클라우드 계정의 데이터는 삭제하지 않는다. 앱 LocalSettings, 계정 목록, PasswordVault, 즐겨찾기 및 그 아이콘, 게임 설정/파일, 완료된 다운로드 파일도 보존한다. 프로필 API가 실패했다고 폴더 강제 삭제로 대체하지 않는다.

## 실행 순서와 수명

1. 사용자에게 삭제 범위를 안내하고 확인한다.
2. MainWindow 소유 WebViewManager의 정리 상태를 켜고 새로운 로그인·게임 실행 준비·탐색·새로고침 요청을 막는다.
3. 실행 준비 슬롯을 취소한다. 이미 실행된 게임은 중단하지 않는다. 외부 런처를 이미 실행했다면 뒤늦게 게임이 켜지는 것까지 강제 종료하지 않는다.
4. BrowserSession의 작업 세대를 취소한다. 로그인/OTP/스크립트 대화상자, 스크립트 대기, 로컬 소켓을 정리하고 작업들이 빠져나오는 것을 기다린다. 다운로드도 중단한다. 취소된 세대의 콜백은 이후에도 재사용할 수 없다.
5. `about:blank`의 일치하는 탐색 ID가 성공한 것을 확인한다. 20초 내 성공하지 않으면 프로필 삭제를 시작하지 않는다. 정리 중 beforeunload는 승인하고 다른 탐색과 새 다운로드는 막는다.
6. 뒤로/앞으로 탐색 목록과 선택한 범위의 프로필 데이터를 삭제한다. 삭제 API 자체는 완료까지 기다리며, 임의 시간 초과로 gate를 먼저 풀지 않는다.
7. 성공/실패 모두 로그인 스냅샷과 보류 요청을 버리고 새로운 작업 세대를 연다. 성공 후 빈 페이지를 유지하며 자동 재로그인하지 않는다.

정리 작업은 페이지가 아닌 창 소유이므로 다른 설정 페이지로 이동해도 이어진다. 실패는 원본 원인과 함께 기록하며, 부분 실패는 원자적 롤백을 보장하지 않는다.

## API 근거

- [WebView2 삭제 종류](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2browsingdatakinds?view=webview2-winrt-1.0.3719.77)
- [WebView2 프로필 API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2profile?view=webview2-winrt-1.0.3719.77)
- [Page.resetNavigationHistory](https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-resetNavigationHistory)
