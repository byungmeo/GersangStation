@echo off
Rem 작업 폴더를 현재 bat 파일이 있는 위치로 수정
cd /d "%~dp0"

echo # 원본 거상 경로 #
set /p "원본="
echo.
echo # 클라 거상 경로 #
set /p "클라="

Rem 입력값 절대경로 정규화
for %%I in ("%원본%") do set "원본=%%~fI"
for %%I in ("%클라%") do set "클라=%%~fI"

Rem 클라 거상 폴더가 존재하지 않으면 생성
if not exist "%클라%" mkdir "%클라%"

Rem 최상위 폴더 내 필수 파일들을 모두 복사/덮어쓰기
copy /Y "%원본%\*.exe" "%클라%"
copy /Y "%원본%\*.ini" "%클라%"
copy /Y "%원본%\*.gts" "%클라%"
copy /Y "%원본%\*.gcs" "%클라%"
copy /Y "%원본%\*.ico" "%클라%"
copy /Y "%원본%\*.dll" "%클라%"

Rem XIGNCODE 폴더 생성 및 내용 모두 복사/덮어쓰기
if not exist "%클라%\XIGNCODE" mkdir "%클라%\XIGNCODE"
xcopy /Y "%원본%\XIGNCODE\*.*" "%클라%\XIGNCODE\" /e /h /k

Rem Assets 폴더 내 Config 폴더는 복사/덮어쓰기하고, 나머지 폴더는 모두 심볼릭 생성
if not exist "%클라%\Assets" mkdir "%클라%\Assets"
if not exist "%클라%\Assets\Config" mkdir "%클라%\Assets\Config"
xcopy /Y "%원본%\Assets\Config\*.*" "%클라%\Assets\Config\" /e /h /k
for /d %%D in ("%원본%\Assets\*") do (
    if /i not "%%~nxD"=="Config" (
        if exist "%클라%\Assets\%%~nxD" rmdir "%클라%\Assets\%%~nxD"
        mklink /d "%클라%\Assets\%%~nxD" "%%~fD"
    )
)

Rem DLL 및 Online 폴더 심볼릭 생성
if exist "%클라%\DLL" rmdir "%클라%\DLL"
mklink /d "%클라%\DLL" "%원본%\DLL"
if exist "%클라%\Online" rmdir "%클라%\Online"
mklink /d "%클라%\Online" "%원본%\Online"

echo # 작업완료 #
pause