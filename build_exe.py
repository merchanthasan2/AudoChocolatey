import subprocess
import os
import sys
import shutil

def build():
    print("=" * 60)
    print("Building Quantum Updater Standalone Executable (.exe)")
    print("Glassmorphic WebView2 Architecture")
    print("=" * 60)

    # Output directory
    dist_dir = os.path.join(os.path.dirname(__file__), "dist")
    target_out = os.path.join(dist_dir, "QuantumUpdater")
    if os.path.exists(target_out):
        try:
            shutil.rmtree(target_out, ignore_errors=True)
        except Exception:
            pass

    icon_path = os.path.join(os.path.dirname(__file__), "src-tauri", "icons", "icon.ico")

    cmd = [
        sys.executable,
        "-m",
        "PyInstaller",
        "--noconfirm",
        "--onedir",
        "--windowed", # Suppress black cmd prompt
        "--uac-admin", # Request Administrator privilege via embedded Windows manifest
        "--name", "QuantumUpdater",
        "--icon", icon_path,
        "--collect-all", "webview",
        "--collect-all", "pythonnet",
        "--collect-all", "clr_loader",
        "--add-data", f"core{os.pathsep}core",
        "--add-data", f"web{os.pathsep}web",
        "--add-data", f"brand{os.pathsep}brand",
        "main.py"
    ]

    print("Running command:", " ".join(cmd))
    res = subprocess.run(cmd)

    if res.returncode == 0:
        exe_path = os.path.join(dist_dir, "QuantumUpdater", "QuantumUpdater.exe")
        print("\n" + "=" * 60)
        print(f"[SUCCESS]: Executable built successfully!")
        print(f"Location: {exe_path}")
        print("=" * 60)
        return exe_path
    else:
        print("\n[ERROR]: Build failed with code", res.returncode)
        return None

if __name__ == "__main__":
    build()
