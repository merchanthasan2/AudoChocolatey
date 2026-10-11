import sys
import os
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

    # 4. Create Glassmorphism Desktop Window with Edge WebView2
    window = webview.create_window(
        title="WinPackageHub • Package & Update Manager",
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
