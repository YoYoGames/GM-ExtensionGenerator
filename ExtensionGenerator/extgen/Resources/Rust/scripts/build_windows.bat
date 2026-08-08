@echo off
REM ##### extgen :: generated core (scripts\extgen) — do not edit; customize scripts\build_windows.bat #####
setlocal
cd /d "%~dp0\..\..\rust"
cargo build --release
if errorlevel 1 exit /b 1

set EXT=${EXTGEN_EXTENSION_NAME}
set CRATE=${EXTGEN_CRATE_NAME}
set DEST=%~dp0..\..\source\%EXT%_gml\extensions\%EXT%
if not exist "%DEST%" mkdir "%DEST%"

copy /Y "target\release\%CRATE%.dll" "%DEST%\%EXT%.dll"
echo Deployed %EXT%.dll to %DEST%
