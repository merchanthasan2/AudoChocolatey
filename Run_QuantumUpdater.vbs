' Starts DeskVerve (formerly Quantum Updater) elevated, without a command window.
' Quantum Step (https://quantumstep.in)
Set fso = CreateObject("Scripting.FileSystemObject")
currentDir = fso.GetParentFolderName(WScript.ScriptFullName)
exePath = currentDir & "\dist\QuantumUpdater-WinUI\DeskVerve.exe"
If Not fso.FileExists(exePath) Then
    exePath = currentDir & "\dist\QuantumUpdater\DeskVerve.exe"
End If
If Not fso.FileExists(exePath) Then
    exePath = currentDir & "\dist\QuantumUpdater-WinUI\QuantumUpdater.exe"
End If
If Not fso.FileExists(exePath) Then
    exePath = currentDir & "\dist\QuantumUpdater\QuantumUpdater.exe"
End If
If Not fso.FileExists(exePath) Then
    MsgBox "DeskVerve executable was not found. Build it with: .\scripts\build_installer.ps1 or .\scripts\publish_winui.ps1", 48, "DeskVerve"
    WScript.Quit 1
End If

Set objShell = CreateObject("Shell.Application")
objShell.ShellExecute exePath, "", "", "runas", 1
