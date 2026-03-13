# WinForms Manifest Design

## 목적

레거시 WinForms 앱이 더 이상 GitHub Releases 전체 목록이나 루트 `README.md`를 직접 파싱하지 않고,
단일 JSON manifest만 읽어서 아래 기능을 처리하도록 설계한다.

- 최신 버전 표시
- 업데이트 안내 메시지 표시
- 다운로드 URL 결정
- 공지사항 표시 및 "새 공지" 팝업 판단

후원자 목록은 당분간 기존 `README.md` 파싱 방식을 유지한다.

이 문서는 `스키마 설계`만 다룬다.
호스팅 위치는 나중에 결정하되, 스키마는 아래 두 방식 모두 지원하도록 설계한다.

- GitHub Releases asset
- GitHub repository raw file

## 설계 원칙

1. 사람이 아닌 앱이 읽는 문서이므로 `README.md`처럼 느슨한 포맷을 쓰지 않는다.
2. `winforms` 전용 문서로 설계한다. WinUI와 공유 스키마로 억지 일반화하지 않는다.
3. v1은 단순성을 우선한다. 필드는 적지만 현재 기능을 모두 대체할 수 있어야 한다.
4. 사용자가 보는 문자열은 manifest에 직접 담을 수 있게 한다.
5. 앱이 "한 번만 보여줄 공지"를 안정적으로 판단할 수 있도록 `announcement.id`를 둔다.
6. 다운로드 자산 검증을 위해 `sha256`을 포함한다.
7. v1 적용 범위는 `release`와 `announcements`에 집중한다. 후원자 목록은 별도 정책 확정 전까지 `README.md` fallback을 유지한다.

## 권장 파일명

- `winforms-manifest.json`

## 권장 저장 경로

- `metadata/winforms-manifest.json`

레거시 앱은 장기적으로 GitHub raw URL 또는 동일 내용을 배포하는 다른 정적 엔드포인트에서 이 파일만 읽도록 한다.

## v1 스키마

### Top-level

- `schema_version`: manifest 스키마 버전. 현재는 `1`
- `product`: `"winforms"`
- `channel`: `"stable"` 고정 시작
- `generated_at`: manifest 생성 시각 (UTC ISO 8601)

### `release`

- `version`: 사용자에게 보여줄 앱 버전. 예: `"1.6.4"`
- `tag`: Git 태그. 예: `"winforms-v1.6.4"`
- `compatibility_tag`: 구버전 레거시 클라이언트와 호환되는 숫자 태그. 예: `"1.6.4"`
- `published_at`: 릴리즈 게시 시각 (UTC ISO 8601)
- `is_mandatory`: 강제 업데이트 여부
- `title`: 업데이트 다이얼로그 제목
- `message`: 업데이트 다이얼로그 본문
- `notes_url`: 릴리즈 노트 또는 공지 링크
- `download`
  - `asset_name`: 배포 zip 이름
  - `url`: 직접 다운로드 URL
  - `sha256`: zip SHA-256
  - `size`: 바이트 단위 크기

### `announcements`

배열로 둔다. 최신순 정렬을 전제로 한다.

각 항목 필드:

- `id`: 공지 고유 ID. 예: `"2026-03-13-homepage-login-change"`
- `title`: 목록/링크 라벨
- `summary`: 팝업 본문 또는 요약
- `url`: 열어볼 링크
- `published_at`: 게시 시각
- `expires_at`: 만료 시각. 없으면 null
- `show_popup`: 앱 시작 시 팝업 후보인지 여부
- `severity`: `"info" | "warning" | "critical"`

### `links`

앱에서 직접 열 수 있는 보조 링크 집합.

- `release_page`
- `support_discussion`
- `sponsor_page`

## 예시 JSON

```json
{
  "schema_version": 1,
  "product": "winforms",
  "channel": "stable",
  "generated_at": "2026-03-13T04:30:00Z",
  "release": {
    "version": "1.6.4",
    "tag": "winforms-v1.6.4",
    "compatibility_tag": "1.6.4",
    "published_at": "2026-03-13T04:00:00Z",
    "is_mandatory": false,
    "title": "업데이트 안내",
    "message": "거상 홈페이지 로그인 구조 변경 대응 버전입니다.",
    "notes_url": "https://github.com/byungmeo/GersangStation/releases/tag/winforms-v1.6.4",
    "download": {
      "asset_name": "GersangStation-winforms-v1.6.4.zip",
      "url": "https://github.com/byungmeo/GersangStation/releases/download/winforms-v1.6.4/GersangStation-winforms-v1.6.4.zip",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "size": 18350241
    }
  },
  "announcements": [
    {
      "id": "2026-03-13-homepage-login-change",
      "title": "[26.03.13] 홈페이지 로그인 변경 안내",
      "summary": "거상 홈페이지 구조가 변경되어 로그인 처리 로직이 수정되었습니다.",
      "url": "https://github.com/byungmeo/GersangStation/discussions/103",
      "published_at": "2026-03-13T03:40:00Z",
      "expires_at": null,
      "show_popup": true,
      "severity": "warning"
    }
  ],
  "links": {
    "release_page": "https://github.com/byungmeo/GersangStation/releases/tag/winforms-v1.6.4",
    "support_discussion": "https://github.com/byungmeo/GersangStation/discussions",
    "sponsor_page": "https://github.com/byungmeo/GersangStation/discussions/26"
  }
}
```

## 수동 게시 워크플로 입력안

릴리즈를 먼저 게시한 뒤, GitHub Actions 수동 실행으로 manifest를 갱신한다.

권장 입력 필드:

- `version`
- `title`
- `message`
- `is_mandatory`
- `asset_name`
- `dry_run`

직접 입력하지 않고 workflow가 Release에서 자동으로 채우는 필드:

- `release.tag`
- `release.compatibility_tag`
- `release.published_at`
- `release.notes_url`
- `release.download.url`
- `release.download.sha256`
- `release.download.size`

즉, 운영자는 "이 버전에 대해 앱이 보여줄 메시지"만 입력하고,
실제 GitHub Release metadata는 workflow가 읽어 manifest에 넣는 구조를 권장한다.

## 현재 UI와의 매핑

현재 레거시 UI 기준으로는 아래처럼 매핑하면 된다.

- `label_version_current`: 로컬 실행 파일 버전
- `label_version_latest`: `release.version`
- 업데이트 메시지박스 제목: `release.title`
- 업데이트 메시지박스 본문: `release.message`
- 업데이트 이동 URL: `release.download.url` 또는 `links.release_page`
- 공지 링크 라벨: `announcements[0].title`
- 새 공지 팝업 조건:
  - `announcements[0].show_popup == true`
  - 로컬 저장값 `last_seen_announcement_id != announcements[0].id`
- 후원 목록:
  - 현재는 계속 `README.md` fallback 파싱 결과를 사용

## 저장값 변경 제안

기존:

- `prev_announcement`

권장 변경:

- `last_seen_announcement_id`
- 필요 시 하위 호환용으로 `prev_announcement`를 읽되, 새 값 저장은 `last_seen_announcement_id`만 사용

현재 브리지 구현은 더 보수적으로 간다.

- manifest 공지를 읽을 수 있으면 `last_seen_announcement_id`와 `prev_announcement`를 함께 갱신한다.
- manifest 공지를 못 읽으면 기존 `prev_announcement` 기반 README 공지 파싱으로 fallback 한다.

## 후원자 목록 정책 메모

- 현재 적용 범위에서는 후원자 목록을 manifest로 옮기지 않는다.
- 앱은 계속 루트 `README.md`의 `<summary>후원해주신 분들</summary>` 블록을 파싱한다.
- 추후 GitHub Discussions 본문/댓글 또는 별도 sponsor manifest로 옮길 수 있지만, 그 전까지는 현행 방식 유지가 운영상 가장 단순하다.

## 전환 메모

- 현재 배포된 WinForms 버전은 `README.md`와 숫자 태그를 가정한다.
- 따라서 manifest 기반 전환 전에는 `README.md`를 바로 제거하면 안 된다.
- 브리지 릴리즈 1회가 필요하다.
- 브리지 릴리즈는 아래를 포함해야 한다.
  - manifest 읽기
  - `winforms-v*` 태그 이해 또는 태그 직접 파싱 제거
  - `README.md` 파싱 실패 시에도 동작 유지
- 전환기에는 `winforms-v{version}` 태그와 별도로 숫자 태그도 유지할 수 있다.
- 새 workflow는 manifest용 태그는 `winforms-v{version}`로 기록하되, 실제 GitHub Release metadata와 asset은 숫자 compatibility tag (`{version}`) 기준 non-draft, non-prerelease Release에서 읽어온다.

## 아직 결정하지 않은 항목

- manifest의 최종 호스팅 위치
- 강제 업데이트 정책 사용 여부
- 후원자 목록 최종 데이터 소스
- 다운로드 URL을 manifest에 고정할지, 릴리즈 페이지 URL만 둘지 여부
