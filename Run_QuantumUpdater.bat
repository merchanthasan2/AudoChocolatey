@echo off
:: Starts DeskVerve (formerly Quantum Updater) as administrator. Quantum Step (https://quantumstep.in)
set "EXE_PATH=%~dp0dist\QuantumUpdater-WinUI\DeskVerve.exe"
if not exist "%EXE_PATH%" set "EXE_PATH=%~dp0dist\QuantumUpdater\DeskVerve.exe"
if not exist "%EXE_PATH%" set "EXE_PATH=%~dp0dist\QuantumUpdater-WinUI\QuantumUpdater.exe"
if not exist "%EXE_PATH%" set "EXE_PATH=%~dp0dist\QuantumUpdater\QuantumUpdater.exe"
if not exist "%EXE_PATH%" (
  echo DeskVerve executable was not found. Build it with: .\scripts\build_installer.ps1 or .\scripts\publish_winui.ps1
  exit /b 1
)
powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%EXE_PATH%' -Verb RunAs"
exit /b 0
