@echo off
REM ##### extgen :: generated core (scripts\extgen) — do not edit; customize scripts\build_windows.bat #####
setlocal
set "ROOT=%~dp0..\.."
cd /d "%ROOT%\rust"

set "CARGO_TARGET_DIR=%CD%\target"

cargo build --release
if errorlevel 1 exit /b 1

set EXT=${EXTGEN_EXTENSION_NAME}
set CRATE=${EXTGEN_CRATE_NAME}
set "DEST_REL=${EXTGEN_WINDOWS_OUTPUT_FOLDER}"
pushd "%ROOT%"
for %%I in ("%DEST_REL%") do set "DEST=%%~fI"
popd
if not exist "%DEST%" mkdir "%DEST%"

set SRC=%CARGO_TARGET_DIR%\release\%CRATE%.dll
if not exist "%SRC%" (
  echo ERROR: missing build output: %SRC%
  exit /b 1
)

copy /Y "%SRC%" "%DEST%\%EXT%.dll"
echo Deployed %EXT%.dll to %DEST%
