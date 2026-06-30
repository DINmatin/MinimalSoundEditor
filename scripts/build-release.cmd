@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "VERSION=%~1"
if not defined VERSION set "VERSION=1.0.0"

set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"

set "PROJECT=%ROOT%\MinimalSoundEditor.csproj"
set "FFMPEG=%ROOT%\Tools\ffmpeg.exe"
set "ARTIFACTS=%ROOT%\artifacts"
set "PUBLISH=%ARTIFACTS%\publish\win-x64"
set "PORTABLE=%ARTIFACTS%\portable"
set "INSTALLER=%ARTIFACTS%\installer"
set "ZIP=%PORTABLE%\MinimalSoundEditor_Portable_%VERSION%_win-x64.zip"
set "ISS=%ROOT%\installer\MinimalSoundEditor.iss"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: dotnet wurde nicht gefunden. Installiere das .NET 8 SDK.
    exit /b 1
)

if not exist "%PROJECT%" (
    echo ERROR: Projektdatei nicht gefunden: "%PROJECT%"
    exit /b 1
)

if not exist "%FFMPEG%" (
    echo ERROR: FFmpeg fehlt: "%FFMPEG%"
    echo Lege eine passende ffmpeg.exe in Tools ab.
    exit /b 1
)

set "ISCC="
for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC set "ISCC=%%I"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if not defined ISCC (
    echo ERROR: Inno Setup 6 wurde nicht gefunden.
    echo Installation: winget install --id JRSoftware.InnoSetup -e
    exit /b 1
)

if exist "%ARTIFACTS%" rmdir /s /q "%ARTIFACTS%"
mkdir "%PUBLISH%" || exit /b 1
mkdir "%PORTABLE%" || exit /b 1
mkdir "%INSTALLER%" || exit /b 1

echo.
echo === FFmpeg pruefen ===
"%FFMPEG%" -version > "%TEMP%\mse_ffmpeg_version.txt" 2>&1
if errorlevel 1 (
    echo ERROR: ffmpeg.exe konnte nicht gestartet werden.
    exit /b 1
)

type "%TEMP%\mse_ffmpeg_version.txt"
findstr /i /c:"--enable-nonfree" "%TEMP%\mse_ffmpeg_version.txt" >nul
if not errorlevel 1 (
    echo.
    echo ERROR: Dieser FFmpeg-Build enthaelt --enable-nonfree und darf nicht automatisch veroeffentlicht werden.
    exit /b 1
)

echo.
echo === .NET Release publish ===
dotnet publish "%PROJECT%" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:Version=%VERSION% ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%PUBLISH%"
if errorlevel 1 exit /b 1

if not exist "%PUBLISH%\MinimalSoundEditor.exe" (
    echo ERROR: MinimalSoundEditor.exe fehlt im Publish-Ordner.
    exit /b 1
)

echo.
echo === FFmpeg in Release kopieren ===
if not exist "%PUBLISH%\Tools" mkdir "%PUBLISH%\Tools"
if errorlevel 1 (
    echo ERROR: Tools-Ordner konnte im Publish-Verzeichnis nicht erstellt werden.
    exit /b 1
)

copy /Y "%FFMPEG%" "%PUBLISH%\Tools\ffmpeg.exe" >nul
if errorlevel 1 (
    echo ERROR: Tools\ffmpeg.exe konnte nicht in den Publish-Ordner kopiert werden.
    exit /b 1
)

if not exist "%PUBLISH%\Tools\ffmpeg.exe" (
    echo ERROR: Tools\ffmpeg.exe fehlt nach dem Kopieren im Publish-Ordner.
    exit /b 1
)

copy /Y "%ROOT%\README.md" "%PUBLISH%\README.md" >nul
copy /Y "%ROOT%\CHANGELOG.md" "%PUBLISH%\CHANGELOG.md" >nul
copy /Y "%ROOT%\LICENSE" "%PUBLISH%\LICENSE.txt" >nul
copy /Y "%ROOT%\THIRD_PARTY_NOTICES.md" "%PUBLISH%\THIRD_PARTY_NOTICES.md" >nul
copy /Y "%TEMP%\mse_ffmpeg_version.txt" "%PUBLISH%\FFMPEG_BUILD_INFO.txt" >nul
xcopy /E /I /Y "%ROOT%\third-party-licenses" "%PUBLISH%\third-party-licenses" >nul

echo.
echo === Portable ZIP ===
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Compress-Archive -Path '%PUBLISH%\*' -DestinationPath '%ZIP%' -CompressionLevel Optimal -Force"
if errorlevel 1 exit /b 1

echo.
echo === Inno Setup Installer ===
"%ISCC%" /DMyAppVersion=%VERSION% "%ISS%"
if errorlevel 1 exit /b 1

echo.
echo ============================================================
echo Release erfolgreich erstellt:
echo   Installer: "%INSTALLER%\MinimalSoundEditor_Setup_%VERSION%.exe"
echo   Portable:  "%ZIP%"
echo ============================================================
exit /b 0
