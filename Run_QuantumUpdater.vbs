' VBScript launcher to start Quantum Updater elevated with ZERO command prompt window flash
' Official Utility by Quantum Step (https://quantumstep.in)
Set fso = CreateObject("Scripting.FileSystemObject")
currentDir = fso.GetParentFolderName(WScript.ScriptFullName)
exePath = currentDir & "\dist\portable\QuantumUpdater.exe"
If Not fso.FileExists(exePath) Then
    exePath = currentDir & "\src-tauri\target\release\quantum_updater.exe"
End If
If Not fso.FileExists(exePath) Then
    exePath = currentDir & "\dist\portable\WinPackageHub.exe"
End If

Set objShell = CreateObject("Shell.Application")
objShell.ShellExecute exePath, "", "", "runas", 1
