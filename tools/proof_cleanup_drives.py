import os
import shutil
import string

from ctypes import windll

from core.system_tools.drives import _probe_letter, _volume_label
from web_api import WebAPI
from core.manager import PackageManager


def main() -> None:
    api = WebAPI(PackageManager())
    data = api.cleanup_drives()
    vols = data.get("volumes", [])
    print(
        "physical volumes:",
        [(v["letter"], v.get("label"), v.get("bus"), v.get("external")) for v in vols],
    )
    for letter in string.ascii_uppercase:
        path = f"{letter}:\\"
        if not os.path.exists(path):
            continue
        usage = shutil.disk_usage(path)
        drive_type = windll.kernel32.GetDriveTypeW(path)
        probe = _probe_letter(letter, drive_type)
        print(
            f"raw {letter}: label={_volume_label(letter)!r} type={drive_type} "
            f"probe={'yes' if probe else 'no'} size_gb={round(usage.total / 1e9, 2)}"
        )


if __name__ == "__main__":
    main()
