@echo off
REM tools\verify.bat - OVERDRAFT verification loop, Windows mirror of verify.sh
REM Usage: tools\verify.bat [--release] [--static-only]
setlocal enabledelayedexpansion

set FAIL=0
if "%UNITY_PATH%"=="" set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe
set PROJ=%~dp0..
set SCRIPTS=%PROJ%\Assets\_Project\Scripts
if not exist "%PROJ%\build\logs" mkdir "%PROJ%\build\logs"

set STATIC_ONLY=0
set RELEASE=0
for %%A in (%*) do (
  if "%%A"=="--static-only" set STATIC_ONLY=1
  if "%%A"=="--release"     set RELEASE=1
)

echo.
echo == Static analysis

findstr /S /N /C:"UnityEngine" "%SCRIPTS%\Generation\*.cs" >nul 2>&1
if !errorlevel! equ 0 (
  echo FAIL: UnityEngine referenced in Generation/ - breaks determinism ^(ARCH section 1^)
  set FAIL=1
)

findstr /S /N /R /C:"FindObjectOfType" /C:"GameObject\.Find" /C:"SendMessage" "%SCRIPTS%\*.cs" >nul 2>&1
if !errorlevel! equ 0 (
  echo FAIL: Banned lookup API in Scripts/ ^(CLAUDE.md section 4^)
  set FAIL=1
)

for %%D in (Player Enemies Economy Generation) do (
  findstr /S /N /C:"using System.Linq" "%SCRIPTS%\%%D\*.cs" >nul 2>&1
  if !errorlevel! equ 0 (
    echo FAIL: LINQ in hot-path folder %%D ^(CLAUDE.md section 4^)
    set FAIL=1
  )
)

findstr /S /N /C:"Instantiate(" "%SCRIPTS%\*.cs" 2>nul | findstr /V /C:"ObjectPool" /C:"Bootstrap" /C:"Editor" >nul 2>&1
if !errorlevel! equ 0 (
  echo FAIL: Runtime Instantiate outside the pool ^(C3^)
  set FAIL=1
)

findstr /S /N /C:"UnityEngine.Random" "%SCRIPTS%\Generation\*.cs" "%SCRIPTS%\Core\RunController.cs" >nul 2>&1
if !errorlevel! equ 0 (
  echo FAIL: UnityEngine.Random in a generation path ^(NFR-07^)
  set FAIL=1
)

findstr /S /N /C:"Camera.main" "%SCRIPTS%\*.cs" 2>nul | findstr /V /C:"Awake" /C:"Start" >nul 2>&1
if !errorlevel! equ 0 (
  echo FAIL: Camera.main outside Awake/Start
  set FAIL=1
)

echo.
echo == Compile + EditMode tests
if "%STATIC_ONLY%"=="1" (
  echo SKIPPED ^(--static-only^). Compilation and tests were NOT run.
) else if not exist "%UNITY_PATH%" (
  echo FAIL: Unity not found at: %UNITY_PATH%
  echo        Install Unity 2022.3 LTS or set UNITY_PATH to your editor binary.
  echo        Compilation and tests were NOT run - do not report this task verified.
  set FAIL=1
) else (
  "%UNITY_PATH%" -batchmode -nographics -projectPath "%PROJ%" -runTests -testPlatform EditMode -testResults "%PROJ%\build\logs\results.xml" -logFile "%PROJ%\build\logs\tests.log" -quit
  if !errorlevel! neq 0 (
    echo FAIL: Unity tests failed - see build\logs\tests.log
    set FAIL=1
  )
  findstr /C:"result=\"Failed\"" "%PROJ%\build\logs\results.xml" >nul 2>&1
  if !errorlevel! equ 0 (
    echo FAIL: One or more EditMode tests failed
    set FAIL=1
  )
  findstr /R /C:"error CS[0-9]" "%PROJ%\build\logs\tests.log" >nul 2>&1
  if !errorlevel! equ 0 (
    echo FAIL: Compile errors
    set FAIL=1
  )
)

if "%RELEASE%"=="1" (
  echo.
  echo == Release checks
  if exist "%PROJ%\steam_appid.txt" (
    echo FAIL: steam_appid.txt must not ship ^(STEAM section 2^)
    set FAIL=1
  )
  findstr /S /N /C:"Debug.Log(" "%SCRIPTS%\*.cs" >nul 2>&1
  if !errorlevel! equ 0 (
    echo FAIL: Debug.Log left in shipping code
    set FAIL=1
  )
  findstr /S /N /C:"TODO" /C:"FIXME" "%SCRIPTS%\*.cs" >nul 2>&1
  if !errorlevel! equ 0 (
    echo FAIL: TODO/FIXME left in code
    set FAIL=1
  )
)

echo.
echo == RESULT
if "%FAIL%"=="0" (
  echo PASS
  exit /b 0
) else (
  echo FAIL
  exit /b 1
)
