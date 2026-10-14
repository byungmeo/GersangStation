# 앱 내 브라우저 설정 및 데이터 정리

## 설정

- 주소 인식 실패 시 Google(기본값), Bing, 네이버, 다음 중 저장된 검색 서비스를 사용한다.
- `about:blank`는 대소문자와 앞뒤 공백에 관계없이 허용한다. HTTP/HTTPS와 일반 호스트 외의 스킴은 실행하지 않고 검색한다.
- 그래픽 가속은 기본적으로 허용한다. 해제하면 다음 WebView2 환경 생성 시 `--disable-gpu`를 전달한다. 앱 재실행이 필요하며 GPU 사용을 강제하지는 않는다.
- 검색 서비스와 그래픽 가속은 앱 LocalSettings이므로 브라우저 프로필 초기화로 지우지 않는다.

## 삭제 범위

| 작업 | WebView2 삭제 범위 |
| --- | --- |
| 쿠키 및 캐시 정리 | `AllSite` + `DiskCache` + `BrowsingHistory` + `DownloadHistory` |
| 브라우저 데이터 초기화 | `AllProfile` + 사이트 권한의 명시적 기본값 복원 + 서버 인증서 오류 허용 결정 초기화 |

`AllSite`에는 쿠키, DOM 저장소(IndexedDB/LocalStorage 등), CacheStorage, 서비스 워커가 포함된다. `AllProfile`에는 자동완성과 웹사이트 저장 암호 및 프로필 설정도 포함된다. 두 작업 모두 전체 기간을 대상으로 하며 현재 WebView의 뒤로/앞으로 탐색 목록도 비운다.

초기화는 공식 프로필 삭제 API의 범위이다. UDF 폴더 자체, 외부 브라우저, OS/클라우드 계정의 데이터는 삭제하지 않는다. 앱의 계정 목록, PasswordVault, 즐겨찾기 및 그 아이콘, 게임 설정/파일, 완료된 다운로드 파일도 보존한다. 프로필 API가 실패했다고 폴더 강제 삭제로 대체하지 않는다.

## 실행 순서와 수명

1. 사용자에게 범위를 안내하고 확인한다. 취소가 기본 버튼이다.
2. MainWindow 소유 WebViewManager의 정리 상태를 켜고 새로운 로그인·게임 실행 준비·탐색·새로고침 요청을 막는다.
3. 실행 준비 슬롯을 취소한다. 이미 실행된 게임은 중단하지 않는다. 외부 런처를 이미 실행했다면 뒤늦게 게임이 켜지는 것까지 강제 종료하지 않는다.
4. BrowserSession의 작업 세대를 취소한다. 로그인/OTP/스크립트 대화상자, 스크립트 대기, 로컬 소켓을 정리하고 작업들이 빠져나오는 것을 기다린다. 다운로드도 중단한다. 취소된 세대의 콜백은 이후에도 재사용할 수 없다.
5. `about:blank`의 일치하는 탐색 ID가 성공한 것을 확인한다. 20초 내 성공하지 않으면 프로필 삭제를 시작하지 않는다. 정리 중 beforeunload는 승인하고 다른 탐색과 새 다운로드는 막는다.
6. 뒤로/앞으로 탐색 목록과 선택한 범위의 프로필 데이터를 삭제한다. 삭제 API 자체는 완료까지 기다리며, 임의 시간 초과로 gate를 먼저 풀지 않는다.
7. 성공/실패 모두 로그인 스냅샷과 보류 요청을 버리고 새로운 작업 세대를 연다. 성공 후 빈 페이지를 유지하며 자동 재로그인하지 않는다.

정리 작업은 페이지가 아닌 창 소유이므로, 확인 후 다른 설정 페이지로 이동해도 이어진다. 페이지를 떠나면 그 페이지의 결과 TeachingTip은 띄우지 않는다. 실패는 기존 공용 예외 처리로 원본 예외를 기록/표시한다. 부분 실패는 원자적 롤백을 보장하지 않는다.

## 검증 시 주의

사용자 동의 없이 실제 프로필 정리, 저장 설정 변경, 로그인 또는 게임 실행을 검증 목적으로 수행하지 않는다. 실제 UI/런타임 확인은 별도 테스트 데이터로 아래를 확인한다.

- 네 검색 서비스 저장/재실행 및 주소창 fallback, `about:blank`.
- 작은 창에서 카드 재배치와 키보드 탐색, 삭제 확인 창의 취소/승인.
- 로그인·OTP·다운로드·실행 준비 중 정리, 정리 중 반복 클릭/페이지 이동/창 종료.
- 두 삭제 범위의 차이, 재로그인, 뒤로가기 비활성화, 사이트 저장소/서비스 워커/권한 초기화.
- GPU 토글 후 앱 완전 종료/재실행 및 실제 GPU 사용 여부(기기/드라이버/런타임에 의존).

## API 근거

- [WebView2 삭제 종류](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2browsingdatakinds?view=webview2-winrt-1.0.3719.77)
- [WebView2 프로필 API](https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core/corewebview2profile?view=webview2-winrt-1.0.3719.77)
- [Page.resetNavigationHistory](https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-resetNavigationHistory)
- [WebView2 브라우저 플래그](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/webview-features-flags): `--disable-gpu`는 전용 안정 API가 아닌 플래그이므로 런타임 업데이트 때 재검증해야 한다. Microsoft는 배포 앱의 브라우저 플래그 의존을 권장하지 않는다. 즉시 재생성이나 레지스트리 변경 대신 기본값 유지 + 명시적 선택 + 다음 시작 적용으로 제한한다.
