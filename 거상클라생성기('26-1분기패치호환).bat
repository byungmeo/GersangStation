@echo off
echo # 원본 거상 경로 #
set /p 원본=
echo.
echo # 클라 거상 경로 #
set /p 클라=

Rem 클라 거상 폴더가 존재하지 않으면 생성
if not exist "%클라%" mkdir "%클라%"

Rem 최상위 폴더 내 필수 파일들을 모두 복사
copy "%원본%\*.exe" "%클라%"
copy "%원본%\*.ini" "%클라%"
copy "%원본%\*.gts" "%클라%"
copy "%원본%\*.gcs" "%클라%"
copy "%원본%\*.ico" "%클라%"
copy "%원본%\*.dll" "%클라%"

Rem XIGNCODE 폴더 생성 및 내용 모두 복사
if not exist "%클라%\XIGNCODE" mkdir "%클라%\XIGNCODE"
xcopy "%원본%\XIGNCODE\*.*" "%클라%\XIGNCODE\" /e /h /k

Rem Assets 폴더 내 Config 폴더는 복사하고, 나머지 폴더는 모두 심볼릭 생성
if not exist "%클라%\Assets" mkdir "%클라%\Assets"
if not exist "%클라%\Assets\Config" mkdir "%클라%\Assets\Config"
xcopy "%원본%\Assets\Config\*.*" "%클라%\Assets\Config\" /e /h /k
for /d %%D in ("%원본%\Assets\*") do (
    if /i not "%%~nxD"=="Config" (
        mklink /d "%클라%\Assets\%%~nxD" "%%~fD"
    )
)

Rem DLL 및 Online 폴더 심볼릭 생성
mklink /d "%클라%\DLL" "%원본%\DLL"
mklink /d "%클라%\Online" "%원본%\Online"

echo # 작업완료 #