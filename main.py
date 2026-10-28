import sys
import os
import webview.util

# ==============================================================================
# Robust Native WebView2 Runtime & Interop Resolution Patch
# ==============================================================================
# pywebview's edgechromium attempts to locate win-arm64, win-x64, win-x86 runtimes
# via interop_dll_path. In compiled PyInstaller onedir/onefile packages, missing or
# relocated runtime subfolders cause FileNotFoundError: Cannot find win-arm64.
# We patch interop_dll_path to search all known bundle locations and safely fallback
# so the application launches cleanly across all architectures.
_original_interop_dll_path = webview.util.interop_dll_path

def _patched_interop_dll_path(dll_name: str) -> str:
    try:
        res = _original_interop_dll_path(dll_name)
        if os.path.exists(res):
            return res
    except Exception:
        pass

    # Build search candidates
    base_dirs = []

    if getattr(sys, 'frozen', False):
        if hasattr(sys, '_MEIPASS'):
            base_dirs.append(sys._MEIPASS)
        exe_dir = os.path.dirname(sys.executable)
        base_dirs.append(exe_dir)
        base_dirs.append(os.path.join(exe_dir, '_internal'))

    try:
        import webview
        if hasattr(webview, '__file__') and webview.__file__:
            base_dirs.append(os.path.dirname(os.path.abspath(webview.__file__)))
    except Exception:
        pass

    base_dirs.append(os.path.abspath('.'))

    # Architecture runtime search (win-arm64, win-x64, win-x86)
    if dll_name in ('win-arm64', 'win-x64', 'win-x86'):
        search_archs = [dll_name]
        for fallback in ('win-x64', 'win-x86', 'win-arm64'):
            if fallback not in search_archs:
                search_archs.append(fallback)

        for arch in search_archs:
            for b in base_dirs:
                for sub in (
                    os.path.join('webview', 'lib', 'runtimes', arch, 'native'),
                    os.path.join('lib', 'runtimes', arch, 'native'),
                    os.path.join('runtimes', arch, 'native'),
                    os.path.join(arch, 'native'),
                    arch,
                ):
                    target = os.path.join(b, sub)
                    if os.path.exists(target):
                        return target

        # If architecture directory not found on disk, return existing base directory to avoid crash
        for b in base_dirs:
            if os.path.exists(b):
                return b

    # Interop DLL search (Microsoft.Web.WebView2.Core.dll, etc.)
    for b in base_dirs:
        for sub in (
            dll_name,
            os.path.join('lib', dll_name),
            os.path.join('webview', 'lib', dll_name),
            os.path.join('_internal', dll_name),
            os.path.join('_internal', 'webview', 'lib', dll_name),
        ):
            target = os.path.join(b, sub)
            if os.path.exists(target):
                return target

    raise FileNotFoundError(f'Cannot find {dll_name}')

webview.util.interop_dll_path = _patched_interop_dll_path

import webview
from core.admin import elevate_if_needed, is_admin
from core.manager import PackageManager
from web_api import WebAPI

def get_web_root():
    """Locate the web directory whether running as script or compiled PyInstaller bundle."""
    if getattr(sys, 'frozen', False):
        base_dir = getattr(sys, '_MEIPASS', os.path.dirname(sys.executable))
    else:
        base_dir = os.path.dirname(os.path.abspath(__file__))
    return os.path.join(base_dir, "web", "index.html")

def main():
    # 1. Automatic UAC Elevation
    if "--no-elevate" not in sys.argv:
        elevate_if_needed()

    # 2. Initialize Dual-Engine Core
    manager = PackageManager()
    api = WebAPI(manager)

    # 3. Resolve Frontend URL
    html_file = get_web_root()
    if not os.path.exists(html_file):
        # Fallback to local relative
        html_file = os.path.abspath(os.path.join("web", "index.html"))

    # 4. Create Glassmorphism Desktop Window with Edge WebView2 / WebKit
    window = webview.create_window(
        title="Quantum Updater • System & Package Manager",
        url=html_file,
        width=1200,
        height=780,
        min_size=(960, 640),
        js_api=api,
        text_select=True
    )
    api.set_window(window)

    def on_closing():
        """Cleanly abort all subprocesses and background installers upon window close."""
        try:
            manager.abort_all()
        except Exception:
            pass
        os._exit(0)

    window.events.closing += on_closing

    # 5. Start Event Loop (Edge Chromium WebView2)
    webview.start(debug=False)

if __name__ == "__main__":
    main()
