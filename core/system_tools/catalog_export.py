"""WinGet catalog export/import for Wave 2 tracker 31.2."""
from __future__ import annotations

import os
import shutil
import subprocess
from typing import Any, Dict, Optional


def resolve_winget_executable() -> Optional[str]:
    """Match WingetEngine discovery: PATH then WindowsApps alias."""
    exe = shutil.which("winget.exe") or shutil.which("winget")
    if not exe:
        local_appdata = os.environ.get("LOCALAPPDATA", "")
        candidate = os.path.join(local_appdata, "Microsoft", "WindowsApps", "winget.exe")
        if os.path.exists(candidate):
            exe = candidate
    if exe and os.path.exists(exe):
        return exe
    return None


def _norm_path(path: str) -> str:
    raw = str(path or "").strip()
    if not raw:
        return ""
    if not os.path.isabs(raw):
        home = os.path.expanduser("~")
        raw = os.path.join(home, raw.replace("/", os.sep))
    return os.path.normpath(os.path.abspath(os.path.expanduser(raw)))


def _allowed_catalog_path(path: str) -> tuple[bool, str]:
    resolved = _norm_path(path)
    if not resolved.lower().endswith(".json"):
        return False, "The file must be a .json path."
    home = _norm_path(os.path.expanduser("~"))
    profile = _norm_path(os.environ.get("USERPROFILE", home) or home)
    downloads = _norm_path(os.path.join(profile, "Downloads"))

    def under(root: str) -> bool:
        return resolved == root or resolved.startswith(root + os.sep)

    if under(profile) or under(downloads):
        return True, resolved
    return False, "Save the catalog under your user profile or Downloads folder only."


def _run_winget(args: list, timeout: int = 600) -> subprocess.CompletedProcess:
    startupinfo = None
    flags = 0
    if os.name == "nt":
        startupinfo = subprocess.STARTUPINFO()
        startupinfo.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startupinfo.wShowWindow = 0
        flags = 0x08000000
    return subprocess.run(
        args,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout,
        startupinfo=startupinfo,
        creationflags=flags,
    )


def export_catalog(path: str) -> Dict[str, Any]:
    """Export installed packages with versions (winget export)."""
    if os.name != "nt":
        return {"ok": False, "error": "Catalog export is only available on Windows."}
    allowed, detail = _allowed_catalog_path(path)
    if not allowed:
        return {"ok": False, "error": detail}
    out_path = detail
    exe = resolve_winget_executable()
    if not exe:
        return {"ok": False, "error": "WinGet is not available on this PC."}
    parent = os.path.dirname(out_path)
    if parent and not os.path.isdir(parent):
        try:
            os.makedirs(parent, exist_ok=True)
        except OSError as e:
            return {"ok": False, "error": str(e)}
    try:
        res = _run_winget(
            [exe, "export", "-o", out_path, "--include-versions", "--disable-interactivity"],
            timeout=600,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Catalog export timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    out = ((res.stdout or "") + "\n" + (res.stderr or "")).strip()
    ok = res.returncode == 0 and os.path.isfile(out_path)
    if not ok:
        return {
            "ok": False,
            "error": out or f"Export failed (exit {res.returncode}).",
            "exit_code": res.returncode,
        }
    return {
        "ok": True,
        "message": f"Installed apps exported to {out_path}.",
        "path": out_path,
        "exit_code": res.returncode,
        "detail": out[:2000],
    }


def import_catalog(path: str, confirmed: bool = False) -> Dict[str, Any]:
    """Reinstall from a prior export (winget import). Requires confirmation."""
    if not confirmed:
        return {
            "ok": False,
            "error": "Import requires explicit confirmation.",
            "needs_confirm": True,
        }
    if os.name != "nt":
        return {"ok": False, "error": "Catalog import is only available on Windows."}
    allowed, detail = _allowed_catalog_path(path)
    if not allowed:
        return {"ok": False, "error": detail}
    in_path = detail
    if not os.path.isfile(in_path):
        return {"ok": False, "error": "That catalog file was not found."}
    exe = resolve_winget_executable()
    if not exe:
        return {"ok": False, "error": "WinGet is not available on this PC."}
    try:
        res = _run_winget(
            [
                exe,
                "import",
                "-i",
                in_path,
                "--accept-package-agreements",
                "--accept-source-agreements",
                "--disable-interactivity",
            ],
            timeout=1800,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Catalog import timed out."}
    except Exception as e:
        return {"ok": False, "error": str(e)}
    out = ((res.stdout or "") + "\n" + (res.stderr or "")).strip()
    ok = res.returncode == 0
    return {
        "ok": ok,
        "message": (
            "Catalog import finished."
            if ok
            else (out or f"Import failed (exit {res.returncode}).")
        ),
        "path": in_path,
        "exit_code": res.returncode,
        "detail": out[:4000],
        "error": None if ok else (out or f"Import failed (exit {res.returncode})."),
    }