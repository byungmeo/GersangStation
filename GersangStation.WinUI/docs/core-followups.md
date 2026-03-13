# Core Follow-ups

`Core` 예외/실패 문맥 정리는 여기서 일단 멈춘다. 아래는 다음 큰 작업 전에 다시 보면 되는 체크 메모다.

## 우선순위 높음

- `GersangStation` 호출부 전환
  - `Core`가 이제 예외/결과형을 더 많이 드러내므로, 페이지/서비스에서 `Load*`, `GetPassword`, `GetCurrentClientVersion` 같은 legacy 편의 래퍼 대신 `Try*`/구조화 예외를 실제로 소비하도록 바꿔야 함.
- 사용자 정책 분리
  - `Core`에서 제거한 silent fallback 정책을 `GersangStation` 쪽 UX로 옮겨야 함.
  - 예: 패치 정보 없음, 설정 파일 손상, 자격 증명 누락, 클라이언트 버전 판별 실패.

## 우선순위 중간

- `AppDataManager`
  - legacy 동기 래퍼(`LoadAccounts`, `LoadPresetList`, `LoadBrowserFavorites` 등)가 아직 빈 기본값 호출 패턴을 쉽게 만들 수 있음.
  - 호출부가 `Try*` 결과를 쓰도록 바꾸기 전까지는 silent fallback 재유입 위험이 남아 있음.
- `PatchManager`
  - `DownloadVersionInfo(...): VersionInfo?`의 `404 => null`은 현재 의도된 의미지만, 향후 호출부가 더 많아지면 결과형으로 바꿀지 재검토.
  - `GetCurrentClientVersion`, `WriteClientVersion` legacy 래퍼는 아직 유지 중.
- `PasswordVaultHelper`
  - `GetPassword`, `Move`, `Delete` 같은 legacy 래퍼는 여전히 상태를 축약함.
  - 현재는 호환 목적 유지, 새 코드에서는 `Try*` 우선.
- `GameClientHelper`
  - 복사/심볼릭 링크 helper 내부는 여전히 broad catch가 많음.
  - 다만 현재는 단계 문맥이 이미 충분히 올라오므로, 실제 이슈가 나오기 전까진 추가 세분화 필요도는 낮음.

## 우선순위 낮음

- crawler helper 내부 `return null` / `return false`
  - 대부분 "후보 아님" 성격의 파싱 helper라 지금 당장 정책 문제는 아님.
- `Debug.WriteLine`
  - `Core` 전반에 진단 로그가 아직 많음.
  - 릴리즈 로깅 정책을 정할 때 한 번에 정리하는 편이 효율적.
- `ZipCrcReader`
  - 현재 미사용.
  - 실제 사용 시작 전까지는 손대지 않아도 됨.

## 다음 큰 작업 추천

1. `GersangStation` 앱 계층에서 `Core` 결과형 소비로 전환
2. 패치/설정/계정 관련 사용자 메시지 정책 통일
3. 남은 legacy convenience API를 실제 사용처 기준으로 점진 제거
