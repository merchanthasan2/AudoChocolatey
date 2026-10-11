import ctypes
import sys
import os

def is_admin() -> bool:
    """Check if the current process is running with administrative privileges."""
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False

def elevate_if_needed() -> bool:
    """
    Relaunches the application with administrator privileges if not already elevated.
    Returns True if elevation was requested and process should exit.
    """
    if is_admin():
        return False

    try:
        if getattr(sys, 'frozen', False):
            # Running as compiled PyInstaller executable
            executable = sys.executable
            args = " ".join([f'"{arg}"' for arg in sys.argv[1:]])
            ret = ctypes.windll.shell32.ShellExecuteW(None, "runas", executable, args, None, 1)
        else:
            # Running as python script
            executable = sys.executable
            script = os.path.abspath(sys.argv[0])
            params = f'"{script}" ' + " ".join([f'"{arg}"' for arg in sys.argv[1:]])
            ret = ctypes.windll.shell32.ShellExecuteW(None, "runas", executable, params, None, 1)

        # Return code > 32 indicates ShellExecute succeeded
        if ret > 32:
            sys.exit(0)
            return True
    except Exception as e:
        print(f"[Elevation Error]: {e}")

    return False
