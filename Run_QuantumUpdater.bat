@echo off
:: Silent elevated launch of Quantum Updater by Quantum Step (https://quantumstep.in)
set "EXE_PATH=%~dp0dist\portable\QuantumUpdater.exe"
if not exist "%EXE_PATH%" set "EXE_PATH=%~dp0src-tauri\target\release\quantum_updater.exe"
if not exist "%EXE_PATH%" set "EXE_PATH=%~dp0dist\portable\WinPackageHub.exe"
powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%EXE_PATH%' -Verb RunAs"
exit
