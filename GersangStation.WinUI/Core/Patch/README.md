# Core.Patch README (복구본)

브랜치 병합 시 `README.md` 충돌이 자주 나는 지점을 줄이기 위해,
기존 "간단 메모"와 이전 "상세 가이드"를 한 파일로 병합해 둔 문서입니다.

---

## 1) 빠른 참고 (CDN 고정 주소 / suffix)

- Patch Base: `https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/`
- FullClient: `http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z`
- ReadMe suffix: `Client_Readme/readme.txt`
- Latest version archive suffix: `Client_Patch_File/Online/vsn.dat.gsz`

### DownloadReadMe

```csharp
string readMeText = await PatchClientApi.DownloadReadMeAsync();
```

- 내부적으로 `Patch Base + ReadMe suffix` 주소를 사용합니다.

---

## 2) 상위 계층 권장 API (PatchClientApi)

`PatchClientApi`는 WinUI 등 상위 계층에서 단순 호출을 목표로 만든 진입점입니다.

- `SetClientInstallRoot(string)`
- `GetClientInstallRoot()`
- `GetCurrentClientVersion()`
- `GetLatestServerVersionAsync()`
- `PatchAsync(currentClientVersion)`
- `DownloadReadMeAsync()`
- `InstallFullClientAsync(installRoot, progress?)` (비동기 `WriteToDirectoryAsync` 기반, 7z/solid 아카이브는 내부 순차 해제 최적화 사용)
- FullClient 설치는 사용자가 고른 루트 아래 `Gersang` 폴더를 설치 루트로 사용합니다. (예: `C:\A\B\C` 선택 시 `C:\A\B\C\Gersang`)

### 최소 호출 흐름

```csharp
PatchClientApi.SetClientInstallRoot(gameInstallPath);

int current = PatchClientApi.GetCurrentClientVersion();
int latest = await PatchClientApi.GetLatestServerVersionAsync();

if (latest > current)
{
    await PatchClientApi.PatchAsync(current);
}
```

---

## 3) 내부/하위 API 참고

하위 레벨 확장이 필요할 때는 기존 `PatchPipeline`, `PatchPlanBuilder`, `Downloader`, `DownloadManager`를 직접 사용할 수 있습니다.

- `PatchPipeline`: 메타 조회 + 다운로드 + 압축 해제 오케스트레이션
- `PatchPlanBuilder`: `Client_info_File` 행을 `PatchExtractPlan`으로 변환
- `PatchDownloaderStage`: 계획에 포함된 `.gsz` 병렬 다운로드
- `ZipCrcReader`: ZIP/GSZ CRC 읽기

> 참고: 상위 계층은 가능하면 `PatchClientApi`를 우선 사용하고,
> 하위 API는 Core 내부 동작/고급 제어가 필요한 경우에만 사용하는 것을 권장합니다.
