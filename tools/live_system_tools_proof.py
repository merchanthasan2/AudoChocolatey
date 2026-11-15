"""Live proof for startup, large files, and leftovers (owner-requested)."""

import os
import sys
import time
import winreg

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from web_api import WebAPI
from core.manager import PackageManager
from core.system_tools import startup as startup_mod

RUN_PATH = r"Software\Microsoft\Windows\CurrentVersion\Run"
APPROVED = r"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"
ENTRY = "QuantumUpdaterTestEntry"


def main() -> None:
    api = WebAPI(PackageManager())
    notepad = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "notepad.exe")

    print("=== (a) Startup live proof ===")
    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, RUN_PATH) as key:
        winreg.SetValueEx(key, ENTRY, 0, winreg.REG_SZ, notepad)

    listed = api.startup_list()
    hits = [e for e in listed if e["name"] == ENTRY]
    print(f"startup_list: found={len(hits)}")
    assert hits, "Test entry not listed"
    eid = hits[0]["id"]

    api.startup_set_enabled(eid, False)
    raw = startup_mod._reg_read_binary(winreg.HKEY_CURRENT_USER, APPROVED, ENTRY)
    print(f"StartupApproved disabled byte: 0x{raw[0]:02x}" if raw else "StartupApproved missing")
    assert raw and raw[0] == 0x03

    api.startup_set_enabled(eid, True)
    raw2 = startup_mod._reg_read_binary(winreg.HKEY_CURRENT_USER, APPROVED, ENTRY)
    print(f"StartupApproved enabled byte: 0x{raw2[0]:02x}" if raw2 else "StartupApproved missing")
    assert raw2 and raw2[0] in startup_mod._ENABLED_BYTES

    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, RUN_PATH, 0, winreg.KEY_SET_VALUE) as key:
            winreg.DeleteValue(key, ENTRY)
    except OSError:
        pass
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, APPROVED, 0, winreg.KEY_SET_VALUE) as key:
            winreg.DeleteValue(key, ENTRY)
    except OSError:
        pass
    print("Cleaned test registry values.")

    print("=== (b) Large files live proof ===")
    t0 = time.perf_counter()
    job = api.large_files_scan_start(root=os.path.expanduser("~"), min_mb=1000)["job_id"]
    status = api.large_files_status(job)
    while status.get("status") != "done" and time.perf_counter() - t0 < 185:
        time.sleep(0.5)
        status = api.large_files_status(job)
    elapsed = time.perf_counter() - t0
    print(f"Profile scan min 1000 MB: count={status.get('count', 0)} elapsed={elapsed:.2f}s partial={status.get('partial')}")

    temp_root = os.path.join(os.environ.get("TEMP", ""), "qu_lf_test")
    os.makedirs(temp_root, exist_ok=True)
    sample = os.path.join(temp_root, "sample_2mb.bin")
    with open(sample, "wb") as fh:
        fh.write(b"Q" * (2 * 1024 * 1024))
    job2 = api.large_files_scan_start(root=temp_root, min_mb=1)["job_id"]
    t1 = time.perf_counter()
    st2 = api.large_files_status(job2)
    while st2.get("status") != "done" and time.perf_counter() - t1 < 30:
        time.sleep(0.1)
        st2 = api.large_files_status(job2)
    rec = api.large_files_recycle([sample], **{"job_id": job2})
    print(f"Temp folder scan+recycle: success={rec.get('success')} exists_after={os.path.exists(sample)}")

    print("=== (c) Leftovers live proof ===")
    t2 = time.perf_counter()
    lo = api.leftovers_scan()
    print(f"leftovers_scan: count={lo.get('count', 0)} elapsed={lo.get('elapsed_s', time.perf_counter()-t2):.3f}s")


if __name__ == "__main__":
    main()
