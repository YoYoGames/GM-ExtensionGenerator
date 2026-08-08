@echo off
REM ##### extgen :: user entrypoint (IfMissing — customize freely) #####
REM Regenerated core lives in scripts\extgen\ — this wrapper is yours.
call "%~dp0extgen\build_windows.bat" %*
