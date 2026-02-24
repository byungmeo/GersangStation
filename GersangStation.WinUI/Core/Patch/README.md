# Core.Patch API 가이드

이 문서는 `Core.Patch` 라이브러리를 **처음 쓰는 WinUI 프론트 개발자**와, 내부 동작까지 알아야 하는 개발자를 모두 대상으로 작성했습니다.

---

## 1) 빠른 사용법 (프론트 개발자용)

"패치하고 싶다"면, 아래 메서드 하나만 호출하면 됩니다.

```csharp
await PatchPipeline.RunPatchAsync(
    currentClientVersion: currentVersion,
    patchBaseUri: new Uri("https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/"),
    installRoot: gameInstallPath,
    tempRoot: tempPatchPath,
    maxConcurrency: 2,
    maxExtractRetryCount: 2,
    cleanupTemp: true);
```

### 인자 설명 (쉽게)
- `currentClientVersion`: 현재 설치된 클라이언트 버전
- `patchBaseUri`: 패치 서버 루트 URL
- `installRoot`: 실제 게임 파일이 깔린 폴더
- `tempRoot`: 다운로드 중간 파일(.gsz) 임시 보관 폴더
- `maxConcurrency`: 동시 다운로드 개수 (보통 2 추천)
- `maxExtractRetryCount`: 압축 해제 실패 시 재시도 횟수
- `cleanupTemp`: 끝난 뒤 임시 폴더 삭제 여부

### CancellationToken(취소 토큰) 꼭 알아야 하나요?
아니요. 몰라도 됩니다.

- `RunPatchAsync(...)` / `RunPatchFromServerAsync(...)`에는 **토큰 없는 오버로드**가 있습니다.
- 그냥 위 예시처럼 호출하면 내부적으로 `CancellationToken.None`이 들어갑니다.
- "취소 버튼" 같은 기능이 필요할 때만 토큰 버전을 쓰면 됩니다.

---

## 2) 상세 문서 (개발자용)

## 주요 공개 타입

### `PatchPipeline`
패치 전체 오케스트레이션 엔트리입니다.

- `RunPatchAsync(...)`
  - 서버 메타(`vsn.dat.gsz`, `Client_info_File/{version}`)를 읽고
  - 다운로드 계획을 만들고
  - 필요한 `.gsz`를 병렬 다운로드 후
  - 버전 오름차순으로 압축 해제합니다.
- `RunPatchFromServerAsync(...)`
  - 현재는 `RunPatchAsync`의 의미상 별칭입니다.
- `DecodeLatestVersionFromVsnDat(ReadOnlySpan<byte>/Stream)`
  - `vsn.dat` 바이너리에서 최신 버전을 디코딩합니다.
- `ParseClientInfoRows(string)`
  - `Client_info_File` 텍스트를 탭 컬럼 배열(`string[]`) 리스트로 파싱합니다.

### `PatchPlanBuilder`
`Client_info_File` 행들을 `PatchExtractPlan`으로 바꿉니다.

- 중복 키는 `relativeDir + compressedFileName`
- 최신 버전 행이 우선
- 결과는 `SourceVersion` 기준 그룹

#### 레거시 호환
- `PatchPlanBuilder_StringRows`는 호환용 래퍼입니다.
- 신규 코드는 `PatchPlanBuilder` 사용을 권장합니다.

### `PatchExtractPlan` / `PatchFile`
다운로드/압축해제 대상 스냅샷 모델입니다.

- `PatchExtractPlan.ByVersion`: `SortedDictionary<int, List<PatchFile>>`
- 키(버전) 오름차순 순회 시, 안전한 적용 순서를 그대로 사용할 수 있습니다.

### `PatchDownloaderStage`
계획에 포함된 `.gsz`를 `tempRoot/{version}/` 하위로 다운로드합니다.

- 내부적으로 `Downloader + DownloadManager`를 사용
- 취소(`OperationCanceledException`) 발생 시 임시폴더 삭제(best-effort)

### `Downloader`
단일 파일 다운로드 담당.

- Range + If-Range 기반 resume 지원
- 서버가 200 전체 응답으로 되돌아오면 partial 폐기 후 재시작
- 재시도 + 백오프 내장

### `DownloadManager`
여러 다운로드를 큐잉/중복제거/동시성 제어합니다.

- `EnqueueAsync(url, path, options, progress, ct)`
- `EnqueueAsync(url, path, options, progress = null)` 간편 오버로드
- 키(`url||destinationPath`) 기준 중복 작업은 기존 Task를 반환

### `ZipCrcReader`
ZIP/GSZ Central Directory에서 엔트리 CRC를 읽는 유틸입니다.

- `ReadEntryCrcMap`
- `IsSupportedZipLikeFile`
- CRC 10진/16진 변환 보조 메서드

---

## 호출 패턴 예시 (취소 지원 버전)

```csharp
using var cts = new CancellationTokenSource();

// 예: 취소 버튼 클릭 시 cts.Cancel();
await PatchPipeline.RunPatchAsync(
    currentClientVersion: currentVersion,
    patchBaseUri: patchBaseUri,
    installRoot: installRoot,
    tempRoot: tempRoot,
    maxConcurrency: 2,
    maxExtractRetryCount: 2,
    ct: cts.Token,
    cleanupTemp: true);
```

---

## 동작/정책 요약

- 패치 적용은 **버전 오름차순**
- 같은 파일 경로 충돌 시 **최신 버전 메타 우선**
- 취소/실패 시 임시 데이터 정리(best-effort)
- 압축 해제 시 zip-slip 방어(루트 경로 이탈 차단)
