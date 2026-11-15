@echo off
:: Starts the Windows app as administrator. Quantum Step (https://quantumstep.in)
set "EXE_PATH=%~dp0dist\QuantumUpdater\QuantumUpdater.exe"
if not exist "%EXE_PATH%" (
  echo Build the app first: python build_exe.py QuantumUpdater
  exit /b 1
)
powershell -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%EXE_PATH%' -Verb RunAs"
exit /b 0
