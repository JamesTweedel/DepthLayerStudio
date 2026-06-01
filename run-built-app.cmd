@echo off
set APP=%~dp0bin\DepthLayer Studio.exe
if exist "%APP%" (
  start "" "%APP%"
) else (
  echo DepthLayer Studio has not been built yet.
  echo Run build-windows.ps1, then try again.
  pause
)
