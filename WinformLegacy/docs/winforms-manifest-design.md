# WinForms Manifest Design

## 목적

레거시 WinForms 앱이 GitHub Releases 전체 목록이나 루트 `README.md`를 직접 해석하지 않고,
기계용 JSON 문서를 읽어 아래 기능을 처리하도록 설계한다.

- 최신 버전 표시
- 업데이트 안내 메시지 표시
- 다운로드 URL 결정
- 공지사항 표시 및 "새 공지" 팝업 판단
- 후원자 목록 표시

현재 설계는 `단일 manifest`가 아니라 `역할별 3개 manifest`를 사용한다.

## 분리 이유

1. 릴리즈, 공지, 후원자 목록의 변경 주기가 다르다.
2. 공지만 바꾸기 위해 릴리즈 metadata를 다시 입력할 필요가 없다.
3. 후원자 목록은 운영 방식이 다르므로 release workflow와 분리하는 편이 안전하다.
4. 앱도 섹션별 fallback을 걸기 쉬워진다.

## 권장 파일명과 경로

- release: `metadata/winforms-release-manifest.json`
- announcements: `metadata/winforms-announcements-manifest.json`
- sponsors: `metadata/winforms-sponsors-manifest.json`

기본 raw URL은 아래 3개를 사용한다.

- `https://raw.githubusercontent.com/byungmeo/GersangStation/master/metadata/winforms-release-manifest.json`
- `https://raw.githubusercontent.com/byungmeo/GersangStation/master/metadata/winforms-announcements-manifest.json`
- `https://raw.githubusercontent.com/byungmeo/GersangStation/master/metadata/winforms-sponsors-manifest.json`

## 공통 Top-level

세 문서 모두 공통으로 아래 필드를 가진다.

- `schema_version`
- `product`
- `channel`
- `generated_at`

값은 기본적으로 아래를 사용한다.

- `schema_version`: `1`
- `product`: `"winforms"`
- `channel`: `"stable"`
- `generated_at`: UTC ISO 8601

## Release Manifest

파일: `winforms-release-manifest.json`

### 필드

- `release.version`
- `release.tag`
- `release.compatibility_tag`
- `release.published_at`
- `release.is_mandatory`
- `release.title`
- `release.message`
- `release.notes_url`
- `release.download.asset_name`
- `release.download.url`
- `release.download.sha256`
- `release.download.size`

### 예시

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
    "notes_url": "https://github.com/byungmeo/GersangStation/releases/tag/1.6.4",
    "download": {
      "asset_name": "GersangStation_v.1.6.4.zip",
      "url": "https://github.com/byungmeo/GersangStation/releases/download/1.6.4/GersangStation_v.1.6.4.zip",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "size": 18350241
    }
  }
}
```

## Announcements Manifest

파일: `winforms-announcements-manifest.json`

### 필드

- `announcement`
  - `id`
  - `title`
  - `url`
  - `published_at`
  - `show_popup`

WinForms 공지는 한 번에 하나만 유지한다.
`id`는 Discussion 번호 문자열을 그대로 사용하는 것을 권장한다.

### 예시

```json
{
  "schema_version": 1,
  "product": "winforms",
  "channel": "stable",
  "generated_at": "2026-03-13T04:30:00Z",
  "announcement": {
    "id": "103",
    "title": "[26.03.13] 홈페이지 로그인 변경 안내",
    "url": "https://github.com/byungmeo/GersangStation/discussions/103",
    "published_at": "2026-03-13T03:40:00Z",
    "show_popup": true
  }
}
```

## Sponsors Manifest

파일: `winforms-sponsors-manifest.json`

### 필드

- `sponsors.last_updated_at`
- `sponsors.items`
  - 각 항목은 문자열 1개
  - 포맷: `{후원 날짜} [{후원자명}] {후원내용}`
  - README fallback과 같은 표기 유지

### 예시

```json
{
  "schema_version": 1,
  "product": "winforms",
  "channel": "stable",
  "generated_at": "2026-03-13T04:30:00Z",
  "sponsors": {
    "last_updated_at": "2026-03-13T04:30:00Z",
    "items": [
      "2026-03-13 [후원자A] 감사합니다."
    ]
  }
}
```

## 현재 앱 로딩 규칙

- `winforms_release_manifest_url` 우선, 비어 있으면 `winforms_manifest_url` fallback
- `winforms_announcement_manifest_url` 우선, 비어 있으면 `winforms_manifest_url` fallback
- `winforms_sponsors_manifest_url` 사용

섹션별로 로딩이 실패하면 기존 fallback을 탄다.

- release: GitHub Releases fallback
- announcements: fallback 없음, `winforms-announcements-manifest.json`만 사용
- sponsors: fallback 없음, `winforms-sponsors-manifest.json`만 사용

## Config 키

- `winforms_release_manifest_url`
- `winforms_announcement_manifest_url`
- `winforms_sponsors_manifest_url`
- `winforms_manifest_url`
  - 구 단일 manifest 호환용 fallback 키

## Workflow 구성

manifest도 3개로 나뉘므로 workflow도 역할별로 분리한다.

- `.github/workflows/publish-winforms-release-manifest.yml`
- `.github/workflows/publish-winforms-announcements-manifest.yml`
- `.github/workflows/publish-winforms-sponsors-manifest.yml`

### Release workflow

입력:

- `version`
- `title`
- `message`
- `is_mandatory`
- `asset_name`
- `dry_run`

자동 계산:

- release tag
- compatibility tag
- published_at
- asset download URL
- asset size
- sha256

출력:

- `metadata/winforms-release-manifest.json`

### Announcements workflow

입력:

- `discussion_number`
- `title`
- `show_popup`
- `dry_run`

출력:

- `metadata/winforms-announcements-manifest.json`

입력한 `discussion_number`로 URL을 자동 생성한다.
- `published_at`은 workflow 실행 시각 UTC로 자동 생성한다.

- `https://github.com/byungmeo/GersangStation/discussions/{discussion_number}`

### Sponsors workflow

입력:

- `sponsor_date`
  - `yyyy-mm-dd` 형식
- `sponsor_name`
- `sponsor_message`
- `dry_run`

출력:

- `metadata/winforms-sponsors-manifest.json`

기존 manifest가 있으면 기존 목록 뒤에 한 줄을 추가한다.
중복 문자열이면 다시 추가하지 않는다.
출력 포맷은 README fallback과 동일하다.
- `{후원 날짜} [{후원자명}] {후원내용}`

## 현재 UI 매핑

- 최신 버전 라벨: release manifest
- 업데이트 팝업: release manifest
- 공지 링크/새 공지 팝업: announcement manifest
- 후원 탭 목록: sponsors manifest

## 전환 메모

- 구버전 WinForms는 숫자 release tag를 가정한다.
- 따라서 브리지 릴리즈 이전 설치 사용자 보호를 위해 숫자형 non-prerelease release 호환 레이어는 계속 유지한다.
- manifest 구조는 분리되었고, release만 fallback을 유지한다. announcements와 sponsors는 manifest 전용으로 운영한다.
