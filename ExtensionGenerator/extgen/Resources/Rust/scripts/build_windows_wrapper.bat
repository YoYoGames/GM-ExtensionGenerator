@echo off
REM ##### extgen :: user entrypoint (IfMissing — customize freely) #####
REM Regenerated core lives in scripts\extgen\ — this wrapper is yours.
REM Core deploys to targets.windows.output (same as CMake EXT_OUTPUT_DIR).
REM Put durable extras here (alternate ProxyFile DLL names, extra copy targets).

REM Pin local target before calling the generated core (core also sets this).
set "CARGO_TARGET_DIR=%~dp0..\rust\target"

call "%~dp0extgen\build_windows.bat" %*
if errorlevel 1 exit /b 1

REM Example: if GM ProxyFiles origname is not ExtensionName.dll (e.g. rusty_sdf.dll),
REM also copy that alias next to the deployed DLL:
REM set SRC=%CARGO_TARGET_DIR%\release\${EXTGEN_CRATE_NAME}.dll
REM set DEST=...same folder as targets.windows.output...
REM copy /Y "%SRC%" "%DEST%\proxy_origname.dll" >nul
