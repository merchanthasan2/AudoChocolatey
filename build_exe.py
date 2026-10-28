import subprocess
import os
import sys
import shutil

def build(app_name: str = "QuantumUpdaterApp"):
    print("=" * 60)
    print(f"Building Quantum Updater Standalone Executable: {app_name}.exe")
    print("Glassmorphic WebView2 Architecture (Windows)")
    print("=" * 60)

    dist_dir = os.path.join(os.path.dirname(__file__), "dist")
    target_out = os.path.join(dist_dir, app_name)
    if os.path.exists(target_out):
        try:
            shutil.rmtree(target_out, ignore_errors=True)
        except Exception as e:
            print(f"[Note]: Could not clean {target_out} ({e}), building in-place.")

    icon_path = os.path.join(os.path.dirname(__file__), "src-tauri", "icons", "icon.ico")

    # Locate pywebview library directory to ensure all native runtimes (win-arm64, win-x64, win-x86) and DLLs are bundled
    import webview
    webview_dir = os.path.dirname(os.path.abspath(webview.__file__))
    webview_lib = os.path.join(webview_dir, "lib")

    cmd = [
        sys.executable,
        "-m",
        "PyInstaller",
        "--noconfirm",
        "--onedir",
        "--windowed", # Suppress black console window
        "--uac-admin", # Request Administrator privilege via embedded Windows manifest
        "--name", app_name,
        "--icon", icon_path,
        "--collect-all", "webview",
        "--collect-all", "pythonnet",
        "--collect-all", "clr_loader",
        "--add-data", f"core{os.pathsep}core",
        "--add-data", f"web{os.pathsep}web",
        "--add-data", f"brand{os.pathsep}brand",
    ]

    if os.path.exists(webview_lib):
        cmd.extend([
            "--add-data", f"{webview_lib}{os.pathsep}webview{os.sep}lib",
            "--add-data", f"{webview_lib}{os.pathsep}lib",
        ])

    cmd.append("main.py")

    print("Running command:", " ".join(cmd))
    res = subprocess.run(cmd)

    if res.returncode == 0:
        exe_path = os.path.join(dist_dir, app_name, f"{app_name}.exe")
        print("\n" + "=" * 60)
        print(f"[SUCCESS]: Standalone executable built successfully!")
        print(f"Location: {exe_path}")
        print("=" * 60)
        return exe_path
    else:
        print("\n[ERROR]: Build failed with code", res.returncode)
        return None

if __name__ == "__main__":
    target = sys.argv[1] if len(sys.argv) > 1 else "QuantumUpdaterApp"
    build(target)
