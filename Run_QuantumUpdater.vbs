' Starts Quantum Updater elevated, without a command window.
' Quantum Step (https://quantumstep.in)
Set fso = CreateObject("Scripting.FileSystemObject")
currentDir = fso.GetParentFolderName(WScript.ScriptFullName)
exePath = currentDir & "\dist\QuantumUpdater\QuantumUpdater.exe"
If Not fso.FileExists(exePath) Then
    MsgBox "QuantumUpdater.exe was not found. Build it with: python build_exe.py QuantumUpdater", 48, "Quantum Updater"
    WScript.Quit 1
End If

Set objShell = CreateObject("Shell.Application")
objShell.ShellExecute exePath, "", "", "runas", 1
