# Quantum Updater

Windows app by [Quantum Step](https://quantumstep.in). It updates and installs software, and it includes the system tools already on this branch.

This branch is `channel/windows`. The program you open is `dist\QuantumUpdater\QuantumUpdater.exe`. Mac work stays on `channel/macos` and is not part of this build.

The screens, colours, type, and brand are shared. The normal theme is Direction B (Deep Teal). Dark mode is Direction A (Graphite Prism). The emblem is in `brand/`. Windows supplies the window through WebView2. Mac will use the same screens later, with its own window material.

## What is in this branch

- `main.py` opens the window and asks for administrator.
- `web/` is the shared interface (`index.html`, `style.css`, `app.js`).
- `web_api.py` is the bridge from the screens to Python.
- `core/` is the Windows work: Winget, Chocolatey, cleanup, and the other system tools.
- `design/` holds the approved mockups.
- `python build_exe.py QuantumUpdater` writes `dist\QuantumUpdater\QuantumUpdater.exe`.

## Build and open

```powershell
pip install -r requirements.txt
pip install pyinstaller
python build_exe.py QuantumUpdater
```

Then start `dist\QuantumUpdater\QuantumUpdater.exe`. `Run_QuantumUpdater.vbs` starts that same file and asks for administrator.

Do not start `python main.py` when you want to test the app. That is the unpackaged script. The test is the exe.
