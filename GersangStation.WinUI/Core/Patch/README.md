# Core.Patch 참고 메모

## CDN 고정 주소 / suffix

- Patch Base: `https://akgersang.xdn.kinxcdn.com/Gersang/Patch/Gersang_Server/`
- FullClient: `http://ak-gersangkr.xcache.kinxcdn.com/FullClient/Gersang_Install.7z`
- ReadMe suffix: `Client_Readme/readme.txt`
- Latest version archive suffix: `Client_Patch_File/Online/vsn.dat.gsz`

## DownloadReadMe

```csharp
string readMeText = await PatchClientApi.DownloadReadMeAsync();
```

- 내부적으로 `Patch Base + ReadMe suffix` 주소를 사용합니다.
