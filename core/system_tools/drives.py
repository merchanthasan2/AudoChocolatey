"""Physical drive discovery (ADR 010)."""

from __future__ import annotations

import ctypes
import os
import shutil
import string
import threading
from ctypes import wintypes
from typing import Any, Dict, List, Optional, Tuple

PROBE_TIMEOUT_S = 1.0

DRIVE_UNKNOWN = 0
DRIVE_REMOVABLE = 2
DRIVE_FIXED = 3
DRIVE_REMOTE = 4
DRIVE_RAMDISK = 6

IOCTL_STORAGE_GET_DEVICE_NUMBER = 0x002D1080
IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400
StorageDeviceProperty = 0
StorageDeviceSeekPenaltyProperty = 7

BUS_TYPE_MAP = {
    3: "sata",
    7: "usb",
    8: "usb",
    11: "sd",
    17: "nvme",
}

VIRTUAL_LABEL_MARKERS = (
    "google drive",
    "googledrivefs",
    "onedrive",
    "dropbox",
    "box sync",
    "network",
)


class STORAGE_DEVICE_NUMBER(ctypes.Structure):
    _fields_ = [
        ("DeviceType", wintypes.DWORD),
        ("DeviceNumber", wintypes.DWORD),
        ("PartitionNumber", wintypes.DWORD),
    ]


class STORAGE_PROPERTY_QUERY(ctypes.Structure):
    _fields_ = [
        ("PropertyId", wintypes.DWORD),
        ("QueryType", wintypes.DWORD),
        ("AdditionalParameters", wintypes.BYTE * 1),
    ]


def _kernel32():
    return ctypes.windll.kernel32


def _run_with_timeout(fn, timeout_s: float) -> Tuple[bool, Any]:
    box: Dict[str, Any] = {"value": None, "error": None}

    def _target():
        try:
            box["value"] = fn()
        except Exception as exc:  # noqa: BLE001
            box["error"] = exc

    thread = threading.Thread(target=_target, daemon=True)
    thread.start()
    thread.join(timeout_s)
    if thread.is_alive():
        return False, None
    if box["error"] is not None:
        return False, None
    return True, box["value"]


def _volume_label(letter: str) -> str:
    buf = ctypes.create_unicode_buffer(261)
    try:
        if _kernel32().GetVolumeInformationW(
            f"{letter}:\\",
            buf,
            260,
            None,
            None,
            None,
            None,
            0,
        ):
            return buf.value or ""
    except Exception:
        pass
    return ""


def _volume_fs(letter: str) -> str:
    fs_buf = ctypes.create_unicode_buffer(64)
    try:
        if _kernel32().GetVolumeInformationW(
            f"{letter}:\\",
            None,
            0,
            None,
            None,
            None,
            fs_buf,
            64,
        ):
            return fs_buf.value or ""
    except Exception:
        pass
    return ""


def _open_volume(letter: str) -> Optional[int]:
    path = f"\\\\.\\{letter}:"
    handle = _kernel32().CreateFileW(
        path,
        0,
        wintypes.DWORD(0x3),
        None,
        3,
        0,
        None,
    )
    if handle == wintypes.HANDLE(-1).value:
        return None
    return int(handle)


def _device_number(letter: str) -> Optional[STORAGE_DEVICE_NUMBER]:
    handle = _open_volume(letter)
    if handle is None:
        return None
    try:
        dev = STORAGE_DEVICE_NUMBER()
        returned = wintypes.DWORD()
        ok = _kernel32().DeviceIoControl(
            handle,
            IOCTL_STORAGE_GET_DEVICE_NUMBER,
            None,
            0,
            ctypes.byref(dev),
            ctypes.sizeof(dev),
            ctypes.byref(returned),
            None,
        )
        if not ok:
            return None
        return dev
    finally:
        _kernel32().CloseHandle(handle)


def _storage_bus_type(letter: str) -> Optional[int]:
    handle = _open_volume(letter)
    if handle is None:
        return None
    try:
        query = STORAGE_PROPERTY_QUERY()
        query.PropertyId = StorageDeviceProperty
        query.QueryType = 0
        out_buf = (wintypes.BYTE * 1024)()
        returned = wintypes.DWORD()
        ok = _kernel32().DeviceIoControl(
            handle,
            IOCTL_STORAGE_QUERY_PROPERTY,
            ctypes.byref(query),
            ctypes.sizeof(query),
            ctypes.byref(out_buf),
            ctypes.sizeof(out_buf),
            ctypes.byref(returned),
            None,
        )
        if not ok or returned.value < 32:
            return None
        # STORAGE_DEVICE_DESCRIPTOR.BusType at offset 28 for Win32 descriptor v1
        return int(out_buf[28])
    finally:
        _kernel32().CloseHandle(handle)


def _seek_penalty(letter: str) -> Optional[bool]:
    """True => HDD-like seek penalty; False => SSD-like."""
    handle = _open_volume(letter)
    if handle is None:
        return None
    try:
        query = STORAGE_PROPERTY_QUERY()
        query.PropertyId = StorageDeviceSeekPenaltyProperty
        query.QueryType = 0
        out_buf = (wintypes.BYTE * 32)()
        returned = wintypes.DWORD()
        ok = _kernel32().DeviceIoControl(
            handle,
            IOCTL_STORAGE_QUERY_PROPERTY,
            ctypes.byref(query),
            ctypes.sizeof(query),
            ctypes.byref(out_buf),
            ctypes.sizeof(out_buf),
            ctypes.byref(returned),
            None,
        )
        if not ok or returned.value < 12:
            return None
        incurs = int.from_bytes(bytes(out_buf[8:12]), "little")
        return bool(incurs)
    finally:
        _kernel32().CloseHandle(handle)


def _is_virtual_label(label: str, fs: str) -> bool:
    blob = f"{label} {fs}".lower()
    if any(m in blob for m in VIRTUAL_LABEL_MARKERS):
        return True
    compact = blob.replace(" ", "")
    if "googledrive" in compact or "googledrivefs" in compact:
        return True
    if "google" in blob and ("drive" in blob or "google d" in blob):
        return True
    if "@gmail.com" in blob and "google" in blob:
        return True
    return False


def _classify_media(bus: str, seek_penalty: Optional[bool]) -> str:
    if bus == "nvme":
        return "nvme"
    if seek_penalty is False:
        return "ssd"
    if seek_penalty is True:
        return "hdd"
    return "unknown"


def _probe_letter(letter: str, drive_type: int) -> Optional[Dict[str, Any]]:
    path = f"{letter}:\\"
    if not os.path.exists(path):
        return None

    label = _volume_label(letter)
    fs = _volume_fs(letter)
    if drive_type == DRIVE_REMOTE or drive_type == DRIVE_RAMDISK:
        return None
    if _is_virtual_label(label, fs):
        return None

    ok_dev, dev = _run_with_timeout(lambda: _device_number(letter), PROBE_TIMEOUT_S)
    if not ok_dev or dev is None:
        return None

    ok_bus, bus_raw = _run_with_timeout(lambda: _storage_bus_type(letter), PROBE_TIMEOUT_S)
    bus_code = bus_raw if ok_bus else None
    bus = BUS_TYPE_MAP.get(bus_code or -1, "other")

    ok_penalty, seek_penalty = _run_with_timeout(lambda: _seek_penalty(letter), PROBE_TIMEOUT_S)
    media = _classify_media(bus, seek_penalty if ok_penalty else None)

    try:
        usage = shutil.disk_usage(path)
    except OSError:
        return None

    external = drive_type == DRIVE_REMOVABLE or bus in ("usb", "sd")
    return {
        "letter": letter,
        "label": label,
        "filesystem": fs,
        "total_bytes": usage.total,
        "free_bytes": usage.free,
        "used_bytes": usage.used,
        "bus": bus,
        "media": media,
        "external": external,
        "device_number": int(dev.DeviceNumber),
        "partition_number": int(dev.PartitionNumber),
        "drive_type": "removable" if drive_type == DRIVE_REMOVABLE else "fixed",
    }


def enumerate_cleanup_volumes(
    *,
    include_external: bool = True,
) -> List[Dict[str, Any]]:
    """Volumes backed by a physical disk (ADR 010); internal by default for cleanup scope."""
    mask = _kernel32().GetLogicalDrives()
    volumes: List[Dict[str, Any]] = []
    for i, letter in enumerate(string.ascii_uppercase):
        if not (mask & (1 << i)):
            continue
        drive_type = _kernel32().GetDriveTypeW(f"{letter}:\\")
        if drive_type not in (DRIVE_FIXED, DRIVE_REMOVABLE):
            continue
        ok, row = _run_with_timeout(lambda l=letter, t=drive_type: _probe_letter(l, t), PROBE_TIMEOUT_S)
        if not ok or row is None:
            continue
        if row["external"] and not include_external:
            continue
        volumes.append(row)
    volumes.sort(key=lambda v: v["letter"])
    return volumes


def list_physical_drives() -> List[Dict[str, Any]]:
    """Hardware-oriented drive list for About / sidebar (unique device numbers)."""
    volumes = enumerate_cleanup_volumes(include_external=True)
    by_device: Dict[int, Dict[str, Any]] = {}
    for vol in volumes:
        dev_num = vol["device_number"]
        if dev_num not in by_device:
            by_device[dev_num] = {
                "device_number": dev_num,
                "bus": vol["bus"],
                "media": vol["media"],
                "external": vol["external"],
                "volumes": [],
            }
        by_device[dev_num]["volumes"].append(
            {
                "letter": vol["letter"],
                "label": vol["label"],
                "filesystem": vol["filesystem"],
                "total_bytes": vol["total_bytes"],
                "free_bytes": vol["free_bytes"],
                "used_bytes": vol["used_bytes"],
            }
        )
    return list(by_device.values())
