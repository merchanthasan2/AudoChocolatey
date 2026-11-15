"""Windows hardware fact collection for About this computer."""

from __future__ import annotations

import ctypes
import json
import os
import platform
import re
import shutil
import string
import struct
import threading
import time
from concurrent.futures import ThreadPoolExecutor, as_completed
from typing import Any, Callable, Dict, List, Optional, Tuple

_SERIAL_PLACEHOLDERS = frozenset({
    "",
    "0",
    "00000000",
    "000000000000",
    "none",
    "not applicable",
    "not specified",
    "default string",
    "system serial number",
    "to be filled by o.e.m.",
    "to be filled by o.e.m",
    "chassis serial number",
    "unknown",
})

_INTEL_GEN_RE = re.compile(
    r"(\d+)(?:st|nd|rd|th)\s+gen(?:eration)?",
    re.IGNORECASE,
)
_INTEL_MODEL_RE = re.compile(
    r"\b(?:core\s*(?:tm|®)?\s*)?(?:i[39]|i[57]|i[35]|i[57]-?\d*|i[39]-?\d*|"
    r"ultra\s*[3579]|celeron|pentium|xeon)\s*[-\s]?\s*(\d{4,5}[A-Za-z]{0,3})",
    re.IGNORECASE,
)
_AMD_RYZEN_RE = re.compile(
    r"\b(ryzen|threadripper|athlon|epyc)\s*(\d+)?\s*(?:pro\s*)?(\d{4}[a-z]{0,3})?",
    re.IGNORECASE,
)
_AMD_AI_RE = re.compile(r"\bryzen\s*ai\s*(\d+)\b", re.IGNORECASE)
_SNAPDRAGON_RE = re.compile(
    r"snapdragon\s*(?:\(r\)\s*)?([^,\-]+)?(?:\s*-\s*([X\dA-Za-z]+))?",
    re.IGNORECASE,
)
_APPLE_SILICON_RE = re.compile(r"\bapple\s*m(\d+)\s*(pro|max|ultra)?\b", re.IGNORECASE)
_MS_SQ_RE = re.compile(r"\bmicrosoft\s*sq\d+", re.IGNORECASE)
_SEGMENT_RE = re.compile(r"(\d{3,5})([A-Za-z]{1,3})\b")

_AMD_ZEN_BY_SERIES: Dict[int, str] = {
    1: "Ryzen 1000 series (Zen, Summit Ridge)",
    2: "Ryzen 2000 series (Zen+, Pinnacle Ridge)",
    3: "Ryzen 3000 series (Zen 2, Matisse)",
    4: "Ryzen 4000 series (Zen 2, Renoir)",
    5: "Ryzen 5000 series (Zen 3, Cezanne/Vermeer)",
    6: "Ryzen 6000 series (Zen 3+, Rembrandt)",
    7: "Ryzen 7000 series (Zen 4, Raphael)",
    8: "Ryzen 8000 series (Zen 4, Phoenix)",
    9: "Ryzen 9000 series (Zen 5, Granite Ridge)",
}

_INTEL_FAMILY_BY_DIGIT = {
    3: "Core i3",
    5: "Core i5",
    7: "Core i7",
    9: "Core i9",
}


def normalize_serial(raw: str) -> str:
    text = (raw or "").strip()
    if text.casefold() in _SERIAL_PLACEHOLDERS:
        return ""
    if re.fullmatch(r"0+", text):
        return ""
    return text


def normalize_arch(machine: str = "") -> str:
    value = (machine or platform.machine() or "").upper()
    if value in ("AMD64", "X64", "X86_64"):
        return "x64"
    if value in ("ARM64", "AARCH64"):
        return "arm64"
    if value in ("X86", "I386", "I686"):
        return "x86"
    return value.casefold() or "unknown"


def _segment_from_model(model: str) -> str:
    m = _SEGMENT_RE.search(model or "")
    if not m:
        return ""
    letters = (m.group(2) or "").upper()
    if not letters:
        return ""
    return letters[0] if letters[0] in "UHYPTGKSXEF" else letters


def _amd_generation_from_model(model: str, family_name: str) -> str:
    if not model or len(model) < 4:
        return ""
    if not model[:4].isdigit():
        return ""
    series = int(model[0])
    if family_name.casefold().startswith("threadripper"):
        return f"Threadripper {series}000 series"
    if family_name.casefold().startswith("athlon"):
        return f"Athlon {series}000 series"
    return _AMD_ZEN_BY_SERIES.get(series, f"Ryzen {series}000 series")


def parse_cpu_brand_string(brand: str) -> Dict[str, Any]:
    text = (brand or "").strip()
    low = text.casefold()
    vendor = "other"
    family = ""
    generation = ""
    model_number = ""
    segment = ""

    if "apple" in low or re.search(r"\bm[1-4]\b", low):
        vendor = "apple"
        m = _APPLE_SILICON_RE.search(text) or re.search(r"\bm(\d+)\s*(pro|max|ultra)?", text, re.I)
        if m:
            tier = m.group(1)
            suffix = (m.group(2) or "").title()
            family = f"Apple M{tier}" + (f" {suffix}" if suffix else "")
            generation = f"M{tier} generation"
        else:
            family = "Apple silicon"
    elif "qualcomm" in low or "snapdragon" in low or "oryon" in low:
        vendor = "qualcomm"
        if "x elite" in low:
            family = "Snapdragon X Elite"
            generation = "Snapdragon X Elite (Oryon)"
        elif "x plus" in low:
            family = "Snapdragon X Plus"
            generation = "Snapdragon X Plus (Oryon)"
        elif "8cx" in low:
            family = "Snapdragon 8cx"
            generation = "Snapdragon 8cx Gen series"
        elif re.search(r"\b8c\b", low):
            family = "Snapdragon 8c"
            generation = "Snapdragon 8c"
        elif re.search(r"\b7c\b", low):
            family = "Snapdragon 7c"
            generation = "Snapdragon 7c"
        else:
            family = "Snapdragon"
        m = _SNAPDRAGON_RE.search(text)
        if m and m.group(2):
            model_number = m.group(2).strip()
    elif _MS_SQ_RE.search(low):
        vendor = "qualcomm"
        family = "Microsoft SQ"
        generation = "Microsoft SQ ARM"
    elif "amd" in low or "ryzen" in low or "threadripper" in low or "athlon" in low or "epyc" in low:
        vendor = "amd"
        ai = _AMD_AI_RE.search(text)
        if ai:
            family = f"Ryzen AI {ai.group(1)}"
            generation = "Ryzen AI 300 series"
        elif "threadripper" in low:
            tm = re.search(r"threadripper\s*(\d{4}[A-Z]{0,2})", text, re.I)
            if tm:
                model_number = tm.group(1).upper()
                family = f"Threadripper {model_number[:4]}"
                generation = _amd_generation_from_model(model_number, family)
        elif "athlon" in low:
            am = re.search(r"athlon\s*(\d{4}[A-Z]{0,2})?", text, re.I)
            series_digit = (am.group(1) or "")[:4] if am else ""
            family = f"Athlon {series_digit}".strip()
            if am and am.group(1):
                model_number = am.group(1).upper()
            generation = _amd_generation_from_model(model_number, family)
        else:
            m = _AMD_RYZEN_RE.search(text)
            if m:
                brand_word = (m.group(1) or "Ryzen").title()
                series_digit = m.group(2) or ""
                model_number = (m.group(3) or "").upper()
                family = f"Ryzen {series_digit}".strip()
                generation = _amd_generation_from_model(model_number, family)
        if not model_number:
            tail = re.search(r"\b(\d{4}[A-Z]{0,3})\b", text.upper())
            if tail:
                model_number = tail.group(1)
    elif "intel" in low or "core" in low or "xeon" in low or "celeron" in low or "pentium" in low:
        vendor = "intel"
        if "ultra" in low:
            um = re.search(r"ultra\s*([3579])\s+(\d{3,4}[A-Za-z]{0,3})\b", text, re.I)
            if um:
                family = f"Core Ultra {um.group(1)}"
                model_number = um.group(2).upper()
                series = int(model_number[0]) if model_number[:1].isdigit() else 1
                generation = f"Core Ultra series {series}"
            else:
                family = "Core Ultra"
                generation = "Core Ultra series"
        else:
            gen_m = _INTEL_GEN_RE.search(text)
            if gen_m:
                generation = f"{gen_m.group(1)}th Gen"
            model_m = _INTEL_MODEL_RE.search(text)
            if model_m:
                model_number = model_m.group(1).upper()
            im = re.search(r"\bi([3579])[-\s]?(\d{4,5}[A-Za-z]{0,3})", text, re.I)
            if im:
                family = _INTEL_FAMILY_BY_DIGIT.get(int(im.group(1)), f"Core i{im.group(1)}")
                if not model_number:
                    model_number = im.group(2).upper()
            elif "xeon" in low:
                family = "Xeon"
            elif "celeron" in low:
                family = "Celeron"
            elif "pentium" in low:
                family = "Pentium"
            if not generation and model_number and model_number[:2].isdigit():
                gen = int(model_number[:2])
                if gen >= 10:
                    generation = f"{gen}th Gen"
    elif "arm" in low:
        vendor = "arm"

    if model_number:
        segment = _segment_from_model(model_number)

    return {
        "vendor": vendor,
        "brand_string": text,
        "family": family,
        "generation": generation,
        "model_number": model_number,
        "segment": segment,
        "arch": "unknown",
    }


def detect_gpu_vendor(name: str, provider: str = "") -> str:
    blob = f"{name} {provider}".casefold()
    if "nvidia" in blob or "geforce" in blob or "quadro" in blob:
        return "nvidia"
    if "amd" in blob or "radeon" in blob:
        return "amd"
    if "intel" in blob or "arc" in blob or "iris" in blob or "uhd" in blob:
        return "intel"
    if "qualcomm" in blob or "adreno" in blob:
        return "qualcomm"
    return "other"


def is_usb_display_adapter(name: str, provider: str = "", matching_id: str = "") -> bool:
    """A USB display link is not a graphics card. Windows still files it under display adapters."""
    blob = f"{name} {provider}".casefold()
    matching = (matching_id or "").casefold()
    if matching.startswith("usb\\"):
        return True
    return "displaylink" in blob


def usb_display_key(name: str, matching_id: str = "") -> str:
    """One dock has two USB interfaces. Count the dock once."""
    matching = (matching_id or "").casefold()
    if "&mi_" in matching:
        matching = matching.split("&mi_", 1)[0]
    return f"{(name or '').casefold()}|{matching}"


def _run_timed(fn: Callable[[], Any], timeout: float, default: Any) -> Any:
    box: Dict[str, Any] = {}
    err: Dict[str, Exception] = {}

    def worker() -> None:
        try:
            box["value"] = fn()
        except Exception as exc:
            err["exc"] = exc

    thread = threading.Thread(target=worker, daemon=True)
    thread.start()
    thread.join(timeout)
    if thread.is_alive():
        return default
    if err:
        return default
    return box.get("value", default)


def _registry_value(path: str, value_name: str, hive_name: str = "HKLM") -> str:
    if os.name != "nt":
        return ""
    try:
        import winreg

        hive = winreg.HKEY_LOCAL_MACHINE if hive_name == "HKLM" else winreg.HKEY_CURRENT_USER
        access = winreg.KEY_READ | getattr(winreg, "KEY_WOW64_64KEY", 0)
        with winreg.OpenKey(hive, path, 0, access) as key:
            val, _ = winreg.QueryValueEx(key, value_name)
            return str(val).strip() if val is not None else ""
    except OSError:
        return ""


def _read_cpu_brand() -> str:
    return _registry_value(
        r"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
        "ProcessorNameString",
    )


def _logical_cpu_counts() -> Tuple[int, int]:
    threads = int(os.cpu_count() or 0)
    cores = int(os.process_cpu_count() or 0) if hasattr(os, "process_cpu_count") else 0
    if os.name == "nt":
        try:
            import subprocess
            import tempfile

            script = (
                "$p = Get-CimInstance Win32_Processor | Select-Object -First 1\n"
                'Write-Output ("NumberOfCores={0}" -f $p.NumberOfCores)\n'
                'Write-Output ("NumberOfLogicalProcessors={0}" -f $p.NumberOfLogicalProcessors)\n'
            )
            with tempfile.NamedTemporaryFile(
                "w", suffix=".ps1", delete=False, encoding="utf-8"
            ) as fh:
                fh.write(script)
                script_path = fh.name
            try:
                proc = subprocess.run(
                    ["powershell", "-NoProfile", "-File", script_path],
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                    errors="replace",
                    timeout=2,
                    creationflags=0x08000000,
                )
            finally:
                try:
                    os.remove(script_path)
                except OSError:
                    pass
            text = proc.stdout or ""
            for line in text.splitlines():
                if line.startswith("NumberOfCores="):
                    cores = int(line.split("=", 1)[1].strip() or 0)
                if line.startswith("NumberOfLogicalProcessors="):
                    threads = int(line.split("=", 1)[1].strip() or 0)
        except Exception:
            pass
    if cores <= 0:
        cores = threads
    if threads <= 0:
        threads = cores
    if cores == threads and threads > 1:
        cores = max(1, threads // 2)
    return cores, threads


def _parse_smbios_strings(data: bytes, offset: int) -> Tuple[int, List[str]]:
    if offset + 4 > len(data):
        return offset, []
    typ, length, _handle = struct.unpack_from("<BBH", data, offset)
    if typ == 127:
        return offset, []
    end = offset + length
    strings: List[str] = []
    cursor = end
    string_start = cursor
    while cursor < len(data) - 1:
        if data[cursor] == 0 and data[cursor + 1] == 0:
            cursor += 2
            break
        cursor += 1
    raw = data[string_start:cursor]
    chunk = []
    for b in raw:
        if b == 0:
            if chunk:
                strings.append(bytes(chunk).decode("latin-1", errors="replace").strip())
                chunk = []
        else:
            chunk.append(b)
    if chunk:
        strings.append(bytes(chunk).decode("latin-1", errors="replace").strip())
    return cursor, strings


def _smbios_table() -> bytes:
    if os.name != "nt":
        return b""
    try:
        kernel32 = ctypes.windll.kernel32
        rsmb = 0x52534D42
        size = kernel32.GetSystemFirmwareTable(rsmb, 0, None, 0)
        if size <= 0:
            return b""
        buf = ctypes.create_string_buffer(size)
        got = kernel32.GetSystemFirmwareTable(rsmb, 0, buf, size)
        if got != size:
            return b""
        return bytes(buf.raw)
    except Exception:
        return b""


def _smbios_entries() -> List[Tuple[int, bytes, List[str]]]:
    data = _smbios_table()
    if len(data) < 8:
        return []
    # Skip eight-byte Raw SMBIOS table header (version/length fields).
    offset = 8
    out: List[Tuple[int, bytes, List[str]]] = []
    while offset + 4 < len(data):
        typ, length, _handle = struct.unpack_from("<BBH", data, offset)
        if typ == 127:
            break
        if length < 4 or offset + length > len(data):
            break
        body = data[offset : offset + length]
        offset, strings = _parse_smbios_strings(data, offset)
        out.append((typ, body, strings))
    return out


def _smbios_string(strings: List[str], index: int) -> str:
    if index <= 0 or index > len(strings):
        return ""
    return strings[index - 1]


def _smbios_serial() -> str:
    for typ, body, strings in _smbios_entries():
        if typ == 1 and len(body) >= 8:
            serial_idx = body[7]
            return normalize_serial(_smbios_string(strings, serial_idx))
    return ""


def _smbios_type17_speed_mts(body: bytes) -> Optional[int]:
    """Rated and configured MT/s from SMBIOS Type 17 formatted area (header at offset 0)."""

    def word_mts(word_offset: int, extended_offset: int) -> Optional[int]:
        if len(body) < word_offset + 2:
            return None
        word = struct.unpack_from("<H", body, word_offset)[0]
        if word == 0:
            return None
        if word == 0xFFFF:
            if len(body) < extended_offset + 4:
                return None
            extended = struct.unpack_from("<I", body, extended_offset)[0]
            return int(extended) if extended else None
        return int(word)

    configured = word_mts(0x20, 0x58) if len(body) >= 0x22 else None
    rated = word_mts(0x15, 0x54) if len(body) >= 0x17 else None
    if configured is not None:
        return configured
    return rated


def _smbios_memory() -> Dict[str, Any]:
    total_kb = 0
    speed = None
    mem_type = None
    slots = 0
    type_map = {
        26: "DDR4",
        34: "DDR5",
        35: "LPDDR5",
        29: "LPDDR4",
    }
    for typ, body, strings in _smbios_entries():
        if typ != 17 or len(body) < 0x15:
            continue
        size_words = struct.unpack_from("<H", body, 0x0C)[0]
        if size_words in (0, 0xFFFF):
            if len(body) >= 0x1C:
                size_kb = struct.unpack_from("<I", body, 0x1C)[0]
            else:
                size_kb = 0
        else:
            size_kb = size_words
        if size_kb <= 0:
            continue
        slots += 1
        total_kb += size_kb
        if speed is None:
            module_speed = _smbios_type17_speed_mts(body)
            if module_speed is not None:
                speed = module_speed
        if len(body) > 0x12 and body[0x12] and mem_type is None:
            mem_type = type_map.get(int(body[0x12]))
    return {
        "total_bytes": int(total_kb) * 1024,
        "speed_mhz": speed,
        "type": mem_type,
        "slots_used": slots if slots else None,
    }


def _device_info() -> Dict[str, Any]:
    bios = r"HARDWARE\DESCRIPTION\System\BIOS"
    manufacturer = _registry_value(bios, "SystemManufacturer")
    product = _registry_value(bios, "SystemProductName")
    family = _registry_value(bios, "SystemFamily")
    sku = _registry_value(bios, "BaseBoardProduct") or _registry_value(bios, "SystemSKU")
    model_name = family or product or ""
    if not model_name and manufacturer and product:
        model_name = f"{manufacturer} {product}".strip()
    serial = _smbios_serial() or normalize_serial(_registry_value(bios, "SystemSerialNumber"))
    kind = "unknown"
    chassis = 0
    for typ, body, _strings in _smbios_entries():
        if typ == 3 and len(body) >= 6:
            chassis = int(body[5])
            break
    laptop_types = {8, 9, 10, 11, 14, 30, 31, 32}
    desktop_types = {3, 4, 5, 6, 7, 15, 16, 35}
    tablet_types = {30, 31, 32}
    if chassis in tablet_types:
        kind = "tablet"
    elif chassis in laptop_types:
        kind = "laptop"
    elif chassis in desktop_types:
        kind = "desktop"
    return {
        "kind": kind,
        "manufacturer": manufacturer,
        "model_name": model_name,
        "model_id": sku or product or "",
        "serial_number": serial,
    }


def _mhz_from_registry() -> Optional[int]:
    raw = _registry_value(
        r"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
        "~MHz",
    )
    try:
        return int(raw)
    except (TypeError, ValueError):
        return None


def _cpu_info() -> Dict[str, Any]:
    brand = _read_cpu_brand()
    parsed = parse_cpu_brand_string(brand)
    cores, threads = _logical_cpu_counts()
    parsed["cores"] = cores
    parsed["threads"] = threads
    parsed["base_mhz"] = _mhz_from_registry()
    parsed["arch"] = normalize_arch(platform.machine())
    return parsed


def _iter_display_class_rows() -> List[Dict[str, Any]]:
    if os.name != "nt":
        return []
    import winreg

    path = r"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"
    rows: List[Dict[str, Any]] = []
    try:
        access = winreg.KEY_READ | getattr(winreg, "KEY_WOW64_64KEY", 0)
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, path, 0, access) as base:
            index = 0
            while True:
                try:
                    sub = winreg.EnumKey(base, index)
                    index += 1
                except OSError:
                    break
                if not sub.isdigit():
                    continue
                full = f"{path}\\{sub}"
                desc = _registry_value(full, "DriverDesc")
                if not desc or "microsoft" in desc.casefold() and "basic" in desc.casefold():
                    continue
                provider = _registry_value(full, "ProviderName")
                matching_id = _registry_value(full, "MatchingDeviceId")
                driver_version = _registry_value(full, "DriverVersion")
                vram = None
                for name in ("HardwareInformation.qwMemorySize", "MemorySize"):
                    raw = _registry_value(full, name)
                    if not raw:
                        continue
                    try:
                        vram = int(raw, 0 if raw.lower().startswith("0x") else 10)
                        break
                    except ValueError:
                        pass
                rows.append(
                    {
                        "name": desc,
                        "provider": provider,
                        "matching_id": matching_id,
                        "vram_bytes": vram,
                        "driver_version": driver_version or None,
                    }
                )
    except OSError:
        pass
    return rows


def _gpu_from_display_row(row: Dict[str, Any]) -> Dict[str, Any]:
    desc = str(row.get("name") or "")
    provider = str(row.get("provider") or "")
    vendor = detect_gpu_vendor(desc, provider)
    low_desc = desc.casefold()
    integrated = vendor == "intel" or (
        "radeon" in low_desc
        and "geforce" not in low_desc
        and "rtx" not in low_desc
        and "gtx" not in low_desc
        and "rx " not in low_desc
    )
    return {
        "vendor": vendor,
        "name": desc,
        "vram_bytes": row.get("vram_bytes"),
        "integrated": integrated,
        "driver_version": row.get("driver_version"),
    }


def _enum_display_adapters() -> List[Dict[str, Any]]:
    gpus: List[Dict[str, Any]] = []
    for row in _iter_display_class_rows():
        if is_usb_display_adapter(row["name"], row.get("provider") or "", row.get("matching_id") or ""):
            continue
        gpus.append(_gpu_from_display_row(row))
    return gpus


def _enum_usb_display_devices() -> List[Dict[str, Any]]:
    seen = set()
    devices: List[Dict[str, Any]] = []
    for row in _iter_display_class_rows():
        name = str(row.get("name") or "")
        matching_id = str(row.get("matching_id") or "")
        if not is_usb_display_adapter(name, row.get("provider") or "", matching_id):
            continue
        key = usb_display_key(name, matching_id)
        if key in seen:
            continue
        seen.add(key)
        devices.append(
            {
                "name": name,
                "external": True,
                "connection": "usb",
                "kind": "usb-display",
                "matching_id": matching_id or None,
                "merge_key": key,
            }
        )
    return devices


def _physical_memory_bytes() -> int:
    if os.name != "nt":
        return 0
    try:
        class MEMORYSTATUSEX(ctypes.Structure):
            _fields_ = [
                ("dwLength", ctypes.c_uint32),
                ("dwMemoryLoad", ctypes.c_uint32),
                ("ullTotalPhys", ctypes.c_ulonglong),
                ("ullAvailPhys", ctypes.c_ulonglong),
                ("ullTotalPageFile", ctypes.c_ulonglong),
                ("ullAvailPageFile", ctypes.c_ulonglong),
                ("ullTotalVirtual", ctypes.c_ulonglong),
                ("ullAvailVirtual", ctypes.c_ulonglong),
                ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
            ]

        status = MEMORYSTATUSEX()
        status.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
        if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status)):
            return int(status.ullTotalPhys)
    except Exception:
        pass
    return 0


def _memory_info() -> Dict[str, Any]:
    total = _physical_memory_bytes()
    smb = _smbios_memory()
    speed = smb.get("speed_mhz")
    if speed is not None and speed < 200:
        speed = None
    smb_total = int(smb.get("total_bytes") or 0)
    if total > (1 << 29):
        total_bytes = total
    elif smb_total > (1 << 29):
        total_bytes = smb_total
    else:
        total_bytes = total or smb_total
    return {
        "total_bytes": total_bytes,
        "speed_mhz": speed,
        "type": smb.get("type"),
        "slots_used": smb.get("slots_used"),
    }


def _drive_media(letter: str) -> str:
    try:
        import winreg

        path = rf"SYSTEM\CurrentControlSet\Enum\SCSI"
        access = winreg.KEY_READ | getattr(winreg, "KEY_WOW64_64KEY", 0)
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, path, 0, access) as base:
            idx = 0
            while True:
                try:
                    port = winreg.EnumKey(base, idx)
                    idx += 1
                except OSError:
                    break
                probe = rf"{path}\\{port}"
                try:
                    with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, probe, 0, access) as pkey:
                        j = 0
                        while True:
                            try:
                                dev = winreg.EnumKey(pkey, j)
                                j += 1
                            except OSError:
                                break
                            friendly = _registry_value(f"{probe}\\{dev}", "FriendlyName")
                            if not friendly:
                                continue
                            low = friendly.casefold()
                            if "nvme" in low:
                                return "nvme"
                            if "ssd" in low:
                                return "ssd"
                            if "hdd" in low or "disk" in low:
                                return "hdd"
                except OSError:
                    continue
    except OSError:
        pass
    return "unknown"


def battery_health_percent(
    design_capacity_mwh: Optional[int], full_charge_mwh: Optional[int]
) -> Optional[int]:
    if not design_capacity_mwh or not full_charge_mwh:
        return None
    return min(100, round(full_charge_mwh / design_capacity_mwh * 100))


def _wmi_battery_capacities() -> Tuple[Optional[int], Optional[int], Optional[int]]:
    design: Optional[int] = None
    full: Optional[int] = None
    cycle: Optional[int] = None
    try:
        import win32com.client

        locator = win32com.client.Dispatch("WbemScripting.SWbemLocator")
        svc = locator.ConnectServer(".", "root\\wmi")
        static_rows = list(svc.ExecQuery("SELECT DesignedCapacity FROM BatteryStaticData"))
        if static_rows:
            design = int(static_rows[0].DesignedCapacity)
        full_rows = list(
            svc.ExecQuery("SELECT FullChargedCapacity FROM BatteryFullChargedCapacity")
        )
        if full_rows:
            full = int(full_rows[0].FullChargedCapacity)
        cycle_rows = list(svc.ExecQuery("SELECT CycleCount FROM BatteryCycleCount"))
        if cycle_rows:
            cycle = int(cycle_rows[0].CycleCount)
    except Exception:
        pass
    return design, full, cycle


def _storage_drives() -> List[Dict[str, Any]]:
    if os.name != "nt":
        return []
    from core.system_tools.drives import _is_virtual_label

    kernel32 = ctypes.windll.kernel32
    drives: List[Dict[str, Any]] = []
    mask = kernel32.GetLogicalDrives()
    for i, letter in enumerate(string.ascii_uppercase):
        if not (mask & (1 << i)):
            continue
        root = f"{letter}:\\"
        if kernel32.GetDriveTypeW(root) != 3:
            continue
        try:
            usage = shutil.disk_usage(root)
        except OSError:
            continue
        label_buf = ctypes.create_unicode_buffer(261)
        fs_buf = ctypes.create_unicode_buffer(64)
        serial = ctypes.c_uint(0)
        max_len = ctypes.c_uint(0)
        flags = ctypes.c_uint(0)
        kernel32.GetVolumeInformationW(
            root,
            label_buf,
            len(label_buf),
            ctypes.byref(serial),
            ctypes.byref(max_len),
            ctypes.byref(flags),
            fs_buf,
            len(fs_buf),
        )
        label = label_buf.value or ""
        fs = fs_buf.value or ""
        if _is_virtual_label(label, fs):
            continue
        drives.append(
            {
                "letter": letter,
                "label": label,
                "fs": fs,
                "total_bytes": int(usage.total),
                "free_bytes": int(usage.free),
                "media": _drive_media(letter),
            }
        )
    return drives


def _battery_info(timeout: float = 4.0) -> Dict[str, Any]:
    default: Dict[str, Any] = {
        "present": False,
        "charge_percent": None,
        "on_ac": None,
        "design_capacity_mwh": None,
        "full_charge_mwh": None,
        "health_percent": None,
        "cycle_count": None,
    }
    if os.name != "nt":
        return default

    def read() -> Dict[str, Any]:
        out = dict(default)
        try:
            class SYSTEM_POWER_STATUS(ctypes.Structure):
                _fields_ = [
                    ("ACLineStatus", ctypes.c_ubyte),
                    ("BatteryFlag", ctypes.c_ubyte),
                    ("BatteryLifePercent", ctypes.c_ubyte),
                    ("Reserved1", ctypes.c_ubyte),
                    ("BatteryLifeTime", ctypes.c_ulong),
                    ("BatteryFullLifeTime", ctypes.c_ulong),
                ]

            status = SYSTEM_POWER_STATUS()
            if ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(status)):
                out["on_ac"] = status.ACLineStatus == 1
                if status.BatteryFlag != 128 and status.BatteryLifePercent <= 100:
                    out["charge_percent"] = int(status.BatteryLifePercent)
                    out["present"] = True
        except Exception:
            pass
        design, full, cycle = _wmi_battery_capacities()
        if design is not None:
            out["design_capacity_mwh"] = design
        if full is not None:
            out["full_charge_mwh"] = full
        if cycle is not None:
            out["cycle_count"] = cycle
        health = battery_health_percent(
            out.get("design_capacity_mwh"), out.get("full_charge_mwh")
        )
        if health is not None:
            out["health_percent"] = health
            out["present"] = True
        return out

    return _run_timed(read, timeout, default)


def _display_info() -> List[Dict[str, Any]]:
    if os.name != "nt":
        return []
    user32 = ctypes.windll.user32
    displays: List[Dict[str, Any]] = []

    class DISPLAY_DEVICEW(ctypes.Structure):
        _fields_ = [
            ("cb", ctypes.c_uint32),
            ("DeviceName", ctypes.c_wchar * 32),
            ("DeviceString", ctypes.c_wchar * 128),
            ("StateFlags", ctypes.c_uint32),
            ("DeviceID", ctypes.c_wchar * 128),
            ("DeviceKey", ctypes.c_wchar * 128),
        ]

    class DEVMODEW(ctypes.Structure):
        _fields_ = [
            ("dmDeviceName", ctypes.c_wchar * 32),
            ("dmSpecVersion", ctypes.c_uint16),
            ("dmDriverVersion", ctypes.c_uint16),
            ("dmSize", ctypes.c_uint16),
            ("dmDriverExtra", ctypes.c_uint16),
            ("dmFields", ctypes.c_uint32),
            ("dmPositionX", ctypes.c_int32),
            ("dmPositionY", ctypes.c_int32),
            ("dmDisplayOrientation", ctypes.c_uint32),
            ("dmDisplayFixedOutput", ctypes.c_uint32),
            ("dmColor", ctypes.c_int16),
            ("dmDuplex", ctypes.c_int16),
            ("dmYResolution", ctypes.c_int16),
            ("dmTTOption", ctypes.c_int16),
            ("dmCollate", ctypes.c_int16),
            ("dmFormName", ctypes.c_wchar * 32),
            ("dmLogPixels", ctypes.c_uint16),
            ("dmBitsPerPel", ctypes.c_uint32),
            ("dmPelsWidth", ctypes.c_uint32),
            ("dmPelsHeight", ctypes.c_uint32),
            ("dmDisplayFlags", ctypes.c_uint32),
            ("dmDisplayFrequency", ctypes.c_uint32),
            ("dmICMMethod", ctypes.c_uint32),
            ("dmICMIntent", ctypes.c_uint32),
            ("dmMediaType", ctypes.c_uint32),
            ("dmDitherType", ctypes.c_uint32),
            ("dmReserved1", ctypes.c_uint32),
            ("dmReserved2", ctypes.c_uint32),
            ("dmPanningWidth", ctypes.c_uint32),
            ("dmPanningHeight", ctypes.c_uint32),
        ]

    i = 0
    while True:
        dd = DISPLAY_DEVICEW()
        dd.cb = ctypes.sizeof(DISPLAY_DEVICEW)
        if not user32.EnumDisplayDevicesW(None, i, ctypes.byref(dd), 0):
            break
        if dd.StateFlags & 0x1:
            dm = DEVMODEW()
            dm.dmSize = ctypes.sizeof(DEVMODEW)
            if user32.EnumDisplaySettingsW(dd.DeviceName, -1, ctypes.byref(dm)):
                monitor_name = dd.DeviceString or dd.DeviceName
                j = 0
                while True:
                    mon = DISPLAY_DEVICEW()
                    mon.cb = ctypes.sizeof(DISPLAY_DEVICEW)
                    if not user32.EnumDisplayDevicesW(dd.DeviceName, j, ctypes.byref(mon), 0):
                        break
                    if mon.DeviceString and mon.StateFlags & 0x1:
                        monitor_name = mon.DeviceString
                        break
                    j += 1
                scale = 100
                displays.append(
                    {
                        "name": monitor_name,
                        "width": int(dm.dmPelsWidth),
                        "height": int(dm.dmPelsHeight),
                        "refresh_hz": int(dm.dmDisplayFrequency) or None,
                        "scale_percent": scale,
                    }
                )
        i += 1
    if not displays:
        dm = DEVMODEW()
        dm.dmSize = ctypes.sizeof(DEVMODEW)
        if user32.EnumDisplaySettingsW(None, -1, ctypes.byref(dm)):
            displays.append(
                {
                    "name": "Primary Display",
                    "width": int(dm.dmPelsWidth),
                    "height": int(dm.dmPelsHeight),
                    "refresh_hz": int(dm.dmDisplayFrequency) or None,
                    "scale_percent": 100,
                }
            )
    return displays


def _camera_devices() -> List[str]:
    if os.name != "nt":
        return []
    names: List[str] = []
    classes = (
        r"SYSTEM\CurrentControlSet\Control\Class\{6bdd1fc6-810f-11d0-bec7-08002be10318}",
        r"SYSTEM\CurrentControlSet\Control\Class\{ca3e7ab9-b4c3-4ae6-8251-579ef933890f}",
    )
    import winreg

    for path in classes:
        try:
            access = winreg.KEY_READ | getattr(winreg, "KEY_WOW64_64KEY", 0)
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, path, 0, access) as base:
                idx = 0
                while True:
                    try:
                        sub = winreg.EnumKey(base, idx)
                        idx += 1
                    except OSError:
                        break
                    if not sub.isdigit():
                        continue
                    desc = _registry_value(f"{path}\\{sub}", "DriverDesc")
                    friendly = _registry_value(f"{path}\\{sub}", "FriendlyName")
                    title = friendly or desc
                    if title and title not in names:
                        names.append(title)
        except OSError:
            continue
    return names


def _windows_nt_build_number() -> int:
    raw = _registry_value(
        r"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        "CurrentBuildNumber",
    )
    try:
        return int(str(raw).split(".", 1)[0])
    except (TypeError, ValueError):
        return 0


def _windows_product_display() -> str:
    product = _registry_value(
        r"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        "ProductName",
    )
    if product and _windows_nt_build_number() >= 22000 and product.startswith("Windows 10"):
        product = "Windows 11" + product[len("Windows 10") :]
    return product or "Windows"


def _windows_os_block() -> Dict[str, Any]:
    display_ver = _registry_value(
        r"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        "DisplayVersion",
    )
    return {
        "name": _windows_product_display(),
        "version": display_ver or None,
        "build": str(_windows_nt_build_number() or "") or None,
    }


def collect_windows_hardware(deadline: float = 2.8) -> Dict[str, Any]:
    start = time.perf_counter()
    results: Dict[str, Any] = {}
    tasks = {
        "device": _device_info,
        "cpu": _cpu_info,
        "gpus": _enum_display_adapters,
        "usb_devices": _enum_usb_display_devices,
        "memory": _memory_info,
        "storage": _storage_drives,
        "battery": lambda: _battery_info(timeout=4.0),
        "display": _display_info,
        "camera": _camera_devices,
        "os": _windows_os_block,
    }
    remaining = max(0.4, deadline - (time.perf_counter() - start))
    with ThreadPoolExecutor(max_workers=6) as pool:
        futures = {pool.submit(fn): key for key, fn in tasks.items()}
        try:
            for fut in as_completed(futures, timeout=remaining):
                key = futures[fut]
                try:
                    results[key] = fut.result()
                except Exception:
                    results[key] = None
        except Exception:
            pass
    for key, fn in tasks.items():
        if key not in results or results[key] is None:
            try:
                results[key] = fn()
            except Exception:
                results[key] = [] if key in ("gpus", "usb_devices", "storage", "display", "camera") else {}
    battery = results.get("battery")
    if isinstance(battery, dict) and battery.get("charge_percent") is not None:
        results.setdefault("device", {})
        if results["device"].get("kind") == "unknown":
            results["device"]["kind"] = "laptop"
    return {
        "device": results.get("device") or {},
        "cpu": results.get("cpu") or {},
        "gpus": results.get("gpus") or [],
        "usb_devices": results.get("usb_devices") or [],
        "memory": results.get("memory") or {},
        "storage": results.get("storage") or [],
        "battery": results.get("battery") or {},
        "display": results.get("display") or [],
        "camera": results.get("camera") or [],
        "os": results.get("os") or {},
    }


def flat_legacy_from_hardware(hw: Dict[str, Any]) -> Dict[str, Any]:
    device = hw.get("device") or {}
    cpu = hw.get("cpu") or {}
    memory = hw.get("memory") or {}
    storage = hw.get("storage") or []
    display = hw.get("display") or []
    camera = hw.get("camera") or []
    os_info = hw.get("os") or {}

    total_bytes = 0
    free_bytes = 0
    if storage:
        system = os.environ.get("SystemDrive", "C:")[0].upper()
        primary = next((d for d in storage if d.get("letter") == system), storage[0])
        total_bytes = int(primary.get("total_bytes") or 0)
        free_bytes = int(primary.get("free_bytes") or 0)
    used_pct = int(round(((total_bytes - free_bytes) / total_bytes) * 100)) if total_bytes else 0

    def fmt_bytes(num: int) -> str:
        if num < 1024 * 1024:
            return f"{num / 1024:.1f} KB"
        if num < 1024 * 1024 * 1024:
            return f"{num / (1024 * 1024):.1f} MB"
        return f"{num / (1024 * 1024 * 1024):.2f} GB"

    mem_bytes = int(memory.get("total_bytes") or 0)
    mem_gib = (mem_bytes + (1 << 30) - 1) // (1 << 30) if mem_bytes else 0
    arch = cpu.get("arch") or normalize_arch(platform.machine())
    win_ver = (platform.version() or "").strip()
    os_display = str(os_info.get("name") or "")
    if win_ver:
        os_line = f"{os_display} {win_ver} ({arch})"
    else:
        os_line = f"{os_display} ({arch})" if os_display else arch

    return {
        "model_name": device.get("model_name") or "",
        "model_id": device.get("model_id") or "",
        "serial_number": device.get("serial_number") or "",
        "cpu": cpu.get("brand_string") or "",
        "memory": f"{mem_gib} GB" if mem_gib else "",
        "os_display": os_display,
        "os_info": os_line,
        "arch": arch,
        "camera": camera[0] if camera else "",
        "display": (
            f'{display[0].get("width")}×{display[0].get("height")}'
            if display
            else ""
        ),
        "storage_total_str": fmt_bytes(total_bytes),
        "storage_free_str": fmt_bytes(free_bytes),
        "storage_used_percent": used_pct,
    }
