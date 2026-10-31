@echo off
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"

rem Junction variant of GersangClientCreator_260325.bat.
rem Run normally: this script does not request administrator privileges.
rem As in the original, existing copied files (including Config) are overwritten.
echo # Original Gersang folder #
set "SOURCE="
set /p "SOURCE="
if not defined SOURCE goto invalid_path

rem Remove a trailing separator before appending the sibling folder suffix.
for %%I in ("%SOURCE%\.") do set "SOURCE=%%~fI"
for %%I in ("%SOURCE%") do if /i "%%~fI"=="%%~dI\" goto invalid_path
if not exist "%SOURCE%\Run.exe" goto invalid_path
if not exist "%SOURCE%\Online\" goto invalid_path
if not exist "%SOURCE%\DLL\" goto invalid_path

echo.
echo # Creating clients 2 and 3 next to the original folder #
for %%N in (2 3) do (
    set "DEST=%SOURCE%%%N_Junction"
    call :create_client
    if errorlevel 1 goto failed
)

echo.
echo # Completed: both junction clients created. #
echo "%SOURCE%2_Junction"
echo "%SOURCE%3_Junction"
pause
exit /b 0

:create_client
echo.
echo # Destination: "%DEST%" #

if not exist "%DEST%\" mkdir "%DEST%"
if errorlevel 1 exit /b 1

rem Copy the same top-level file types as the original script.
for %%E in (exe ini gts gcs ico dll) do (
    if exist "%SOURCE%\*.%%E" (
        copy /Y "%SOURCE%\*.%%E" "%DEST%\"
        if errorlevel 1 exit /b 1
    )
)

rem Copy XIGNCODE, including hidden files and empty subdirectories.
if not exist "%DEST%\XIGNCODE\" mkdir "%DEST%\XIGNCODE"
if errorlevel 1 exit /b 1
xcopy /Y "%SOURCE%\XIGNCODE\*.*" "%DEST%\XIGNCODE\" /e /h /k
if errorlevel 2 exit /b 1

rem Config is a real, copied folder, not a junction (same as the original).
if not exist "%DEST%\Assets\Config\" mkdir "%DEST%\Assets\Config"
if errorlevel 1 exit /b 1
xcopy /Y "%SOURCE%\Assets\Config\*.*" "%DEST%\Assets\Config\" /e /h /k
if errorlevel 2 exit /b 1

rem Link every other Assets subdirectory using junctions instead of /D symlinks.
rem Never recursively delete an existing directory: a nonempty real folder stops the script.
for /d %%D in ("%SOURCE%\Assets\*") do (
    if /i not "%%~nxD"=="Config" (
        if exist "%DEST%\Assets\%%~nxD" (
            rmdir "%DEST%\Assets\%%~nxD"
            if errorlevel 1 exit /b 1
        )
        mklink /J "%DEST%\Assets\%%~nxD" "%%~fD"
        if errorlevel 1 exit /b 1
    )
)

rem Preserve the original layout: link DLL and the ENTIRE Online folder.
if exist "%DEST%\DLL" (
    rmdir "%DEST%\DLL"
    if errorlevel 1 exit /b 1
)
mklink /J "%DEST%\DLL" "%SOURCE%\DLL"
if errorlevel 1 exit /b 1
if exist "%DEST%\Online" (
    rmdir "%DEST%\Online"
    if errorlevel 1 exit /b 1
)
mklink /J "%DEST%\Online" "%SOURCE%\Online"
if errorlevel 1 exit /b 1
exit /b 0

:invalid_path
echo.
echo ERROR: Enter a valid original Gersang client folder, not a drive root.
pause
exit /b 1

:failed
echo.
echo ERROR: Copy or junction creation failed. Check the paths and access permissions.
echo Existing nonempty folders are not recursively deleted. Partial output may remain.
pause
exit /b 1
