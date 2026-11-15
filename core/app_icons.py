"""Real application icons: local extraction and publisher-site catalog fetch."""

from __future__ import annotations

import base64
import ctypes
import hashlib
import html.parser
import json
import logging
import os
import re
import shutil
import struct
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import zlib
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Tuple

from core.arp_inventory import ArpEntry, find_arp_entry, list_arp_entries

logger = logging.getLogger(__name__)

CACHE_VERSION = 2
_NEGATIVE_TTL_SECONDS = 24 * 60 * 60
_HOMEPAGE_TTL_SECONDS = 30 * 24 * 60 * 60
_HTML_MAX_BYTES = 1_048_576
_ICON_MAX_BYTES = 524_288
_FETCH_TIMEOUT = 8
_ICON_SIZES = (64, 48)

_MEMORY_LOCK = threading.Lock()
_MEMORY_CACHE: Dict[str, Optional[bytes]] = {}

_ENV_VAR_RE = re.compile(r"%([^%]+)%")
_SVG_UNSAFE_SCRIPT_RE = re.compile(r"<\s*script", re.IGNORECASE)
_SVG_EVENT_HANDLER_RE = re.compile(r"\son[a-z]+\s*=", re.IGNORECASE)
_SVG_EXTERNAL_HREF_RE = re.compile(
    r"(?:href|xlink:href)\s*=\s*[\"']https?://",
    re.IGNORECASE,
)
_SIZE_RE = re.compile(r"(\d+)\s*x\s*(\d+)", re.IGNORECASE)


@dataclass
class InstalledIconEntry:
    package_id: str
    name: str = ""
    origin: str = "catalog"
    arp_id: str = ""
    display_icon: str = ""
    install_location: str = ""


def cache_dir() -> str:
    root = os.path.join(
        os.environ.get("LOCALAPPDATA", ""),
        "QuantumUpdater",
        "icons",
    )
    os.makedirs(root, exist_ok=True)
    return root


def cache_key(package_id: str) -> str:
    norm = f"{CACHE_VERSION}:{(package_id or '').strip().casefold()}".encode("utf-8")
    return hashlib.sha256(norm).hexdigest()


def _purge_legacy_negative_files() -> None:
    root = cache_dir()
    try:
        for name in os.listdir(root):
            if name.endswith(".neg"):
                try:
                    os.remove(os.path.join(root, name))
                except OSError:
                    pass
    except OSError:
        pass


_purge_legacy_negative_files()


def expand_win_path(path: str) -> str:
    text = (path or "").strip().strip('"')
    if not text:
        return ""
    def _repl(match: re.Match[str]) -> str:
        key = match.group(1)
        if key in os.environ:
            return os.environ[key]
        try:
            import winreg

            for hive in (
                winreg.HKEY_LOCAL_MACHINE,
                winreg.HKEY_CURRENT_USER,
            ):
                try:
                    with winreg.OpenKey(hive, rf"Environment") as env_key:
                        val, _ = winreg.QueryValueEx(env_key, key)
                        return str(val)
                except OSError:
                    continue
        except Exception:
            pass
        return match.group(0)

    expanded = _ENV_VAR_RE.sub(_repl, text)
    expanded = os.path.expandvars(expanded)
    return os.path.normpath(expanded)


def parse_display_icon(raw: str) -> Tuple[str, int]:
    text = (raw or "").strip()
    if not text:
        return "", 0
    if text.startswith('"'):
        close = text.find('"', 1)
        if close > 0:
            path = text[1:close]
            rest = text[close + 1 :].lstrip()
            if rest.startswith(","):
                rest = rest[1:].strip()
            idx = 0
            if rest.isdigit() or (rest.startswith("-") and rest[1:].isdigit()):
                idx = int(rest)
            return expand_win_path(path), idx
    if "," in text:
        path_part, idx_part = text.rsplit(",", 1)
        path_part = path_part.strip().strip('"')
        idx = 0
        try:
            idx = int(idx_part.strip())
        except ValueError:
            idx = 0
        return expand_win_path(path_part), idx
    return expand_win_path(text.strip('"')), 0


def _registrable_domain(hostname: str) -> str:
    host = (hostname or "").strip().lower().split(":")[0]
    if not host or host == "localhost":
        return host
    parts = host.split(".")
    if len(parts) <= 2:
        return host
    return ".".join(parts[-2:])


def _normalize_icon_url(url: str) -> str:
    parsed = urllib.parse.urlparse((url or "").strip())
    host = (parsed.hostname or "").lower()
    path = parsed.path or "/"
    return f"{host}{path}".rstrip("/").casefold()


def icon_url_allowed(
    homepage_url: str,
    icon_url: str,
    declared_urls: Optional[set] = None,
) -> bool:
    icon = (icon_url or "").strip()
    if not icon.lower().startswith("https://"):
        return False
    declared = declared_urls or set()
    target = _normalize_icon_url(icon)
    for item in declared:
        if _normalize_icon_url(item) == target:
            return True
    return same_registrable_domain(homepage_url, icon)


def same_registrable_domain(homepage_url: str, icon_url: str) -> bool:
    home = urllib.parse.urlparse(homepage_url)
    icon = urllib.parse.urlparse(icon_url)
    if home.scheme.lower() != "https" or icon.scheme.lower() != "https":
        return False
    h_home = (home.hostname or "").lower()
    h_icon = (icon.hostname or "").lower()
    if not h_home or not h_icon:
        return False
    if h_icon == h_home or h_icon.endswith("." + h_home) or h_home.endswith("." + h_icon):
        return True
    return _registrable_domain(h_home) == _registrable_domain(h_icon)


def detect_image_mime(data: bytes) -> Optional[str]:
    if not data:
        return None
    if data.startswith(b"\x89PNG\r\n\x1a\n"):
        return "image/png"
    if data.startswith(b"\x00\x00\x01\x00"):
        return "image/x-icon"
    head = data[:512].lstrip()
    if head.startswith(b"<svg") or head.startswith(b"<?xml"):
        return "image/svg+xml"
    if b"<svg" in head[:2048].lower():
        return "image/svg+xml"
    return None


def svg_is_safe(text: str) -> bool:
    if not text:
        return False
    if _SVG_UNSAFE_SCRIPT_RE.search(text):
        return False
    if _SVG_EVENT_HANDLER_RE.search(text):
        return False
    if _SVG_EXTERNAL_HREF_RE.search(text):
        return False
    return True


def _png_chunk(tag: bytes, payload: bytes) -> bytes:
    crc = zlib.crc32(tag + payload) & 0xFFFFFFFF
    return struct.pack(">I", len(payload)) + tag + payload + struct.pack(">I", crc)


def encode_png(rgba: bytes, width: int, height: int) -> bytes:
    if width <= 0 or height <= 0:
        return b""
    row_bytes = width * 4
    raw_rows = bytearray()
    for y in range(height):
        raw_rows.append(0)
        start = y * row_bytes
        raw_rows.extend(rgba[start : start + row_bytes])
    compressed = zlib.compress(bytes(raw_rows), level=6)
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return (
        b"\x89PNG\r\n\x1a\n"
        + _png_chunk(b"IHDR", ihdr)
        + _png_chunk(b"IDAT", compressed)
        + _png_chunk(b"IEND", b"")
    )


def png_dimensions(data: bytes) -> Tuple[int, int]:
    if len(data) < 24 or not data.startswith(b"\x89PNG\r\n\x1a\n"):
        return 0, 0
    width, height = struct.unpack(">II", data[16:24])
    return int(width), int(height)


def _cache_paths(key: str) -> Dict[str, str]:
    base = cache_dir()
    return {
        "png": os.path.join(base, f"{key}.png"),
        "neg": os.path.join(base, f"{key}.neg"),
        "meta": os.path.join(base, f"{key}.meta.json"),
    }


def _read_disk_cache(package_id: str) -> Optional[bytes]:
    paths = _cache_paths(cache_key(package_id))
    if os.path.isfile(paths["neg"]):
        try:
            with open(paths["neg"], "r", encoding="utf-8") as fh:
                ts = float((fh.read() or "0").strip())
            if time.time() - ts < _NEGATIVE_TTL_SECONDS:
                return None
        except Exception:
            pass
    if os.path.isfile(paths["png"]):
        try:
            with open(paths["png"], "rb") as fh:
                data = fh.read()
            if data:
                return data
        except OSError:
            pass
    meta_path = paths["meta"]
    if os.path.isfile(meta_path):
        try:
            with open(meta_path, "r", encoding="utf-8") as fh:
                meta = json.load(fh)
            rel = meta.get("file", "")
            mime = meta.get("mime", "")
            path = os.path.join(cache_dir(), rel)
            if rel and os.path.isfile(path):
                with open(path, "rb") as fh:
                    data = fh.read()
                if mime == "image/svg+xml" and not svg_is_safe(data.decode("utf-8", errors="ignore")):
                    return None
                return data
        except Exception:
            pass
    return None


def _write_disk_cache(package_id: str, data: Optional[bytes], mime: str = "image/png") -> None:
    key = cache_key(package_id)
    paths = _cache_paths(key)
    if data is None:
        try:
            with open(paths["neg"], "w", encoding="utf-8") as fh:
                fh.write(str(time.time()))
        except OSError:
            pass
        return
    try:
        if os.path.isfile(paths["neg"]):
            os.remove(paths["neg"])
    except OSError:
        pass
    if mime == "image/png":
        try:
            with open(paths["png"], "wb") as fh:
                fh.write(data)
            return
        except OSError:
            pass
    ext = ".svg" if mime == "image/svg+xml" else ".ico" if mime == "image/x-icon" else ".bin"
    rel = f"{key}{ext}"
    try:
        with open(os.path.join(cache_dir(), rel), "wb") as fh:
            fh.write(data)
        with open(paths["meta"], "w", encoding="utf-8") as fh:
            json.dump({"file": rel, "mime": mime}, fh)
    except OSError:
        pass


def _memory_get(package_id: str) -> Optional[bytes]:
    with _MEMORY_LOCK:
        if package_id in _MEMORY_CACHE:
            return _MEMORY_CACHE[package_id]
    return None


def _memory_put(package_id: str, data: Optional[bytes]) -> None:
    with _MEMORY_LOCK:
        _MEMORY_CACHE[package_id] = data


def _arp_hkey(entry: ArpEntry):
    import winreg

    if entry.scope == "user":
        return winreg.HKEY_CURRENT_USER
    return winreg.HKEY_LOCAL_MACHINE


def _read_arp_value(entry: ArpEntry, value_name: str) -> str:
    if os.name != "nt":
        return ""
    try:
        import winreg

        access = winreg.KEY_READ
        if _arp_hkey(entry) == winreg.HKEY_LOCAL_MACHINE:
            access |= getattr(winreg, "KEY_WOW64_64KEY", 0)
        with winreg.OpenKey(_arp_hkey(entry), entry.registry_path, 0, access) as key:
            val, _ = winreg.QueryValueEx(key, value_name)
            return str(val).strip() if val is not None else ""
    except OSError:
        return ""


def find_arp_for_winget(package_id: str, name: str = "") -> Optional[ArpEntry]:
    needle_name = (name or "").casefold().strip()
    slug = (package_id or "").rsplit(".", 1)[-1].casefold().replace("_", "")
    best: Optional[ArpEntry] = None
    best_score = 0
    for entry in list_arp_entries():
        display = entry.name.casefold()
        if needle_name and display == needle_name:
            return entry
        compact = display.replace(" ", "")
        score = 0
        if needle_name and needle_name in display:
            score += 4
        if slug and slug in compact:
            score += 3
        if slug and slug in display.replace(" ", ""):
            score += 2
        if score > best_score:
            best_score = score
            best = entry
    return best if best_score > 0 else None


def find_main_exe(install_dir: str, name_hint: str = "") -> str:
    if not install_dir or not os.path.isdir(install_dir):
        return ""
    blocked = ("unins", "setup", "update", "crash", "helper")
    hint = (name_hint or "").split()[0].casefold() if name_hint else ""
    matches: List[str] = []
    for root, dirs, files in os.walk(install_dir):
        depth = root[len(install_dir) :].count(os.sep)
        if depth > 2:
            dirs.clear()
            continue
        for fname in files:
            low = fname.casefold()
            if not low.endswith(".exe"):
                continue
            if any(b in low for b in blocked):
                continue
            matches.append(os.path.join(root, fname))
    if not matches:
        return ""
    if hint:
        for path in matches:
            if hint in os.path.basename(path).casefold():
                return path
    matches.sort(key=lambda p: (-os.path.getsize(p), len(p)))
    return matches[0]


def _icon_from_hicon(hicon: int, size: int) -> Optional[bytes]:
    if not hicon:
        return None
    user32 = ctypes.windll.user32
    gdi32 = ctypes.windll.gdi32

    class BITMAPINFOHEADER(ctypes.Structure):
        _fields_ = [
            ("biSize", ctypes.c_uint32),
            ("biWidth", ctypes.c_int32),
            ("biHeight", ctypes.c_int32),
            ("biPlanes", ctypes.c_uint16),
            ("biBitCount", ctypes.c_uint16),
            ("biCompression", ctypes.c_uint32),
            ("biSizeImage", ctypes.c_uint32),
            ("biXPelsPerMeter", ctypes.c_int32),
            ("biYPelsPerMeter", ctypes.c_int32),
            ("biClrUsed", ctypes.c_uint32),
            ("biClrImportant", ctypes.c_uint32),
        ]

    hdc = user32.GetDC(0)
    if not hdc:
        return None
    hdc_mem = gdi32.CreateCompatibleDC(hdc)
    if not hdc_mem:
        user32.ReleaseDC(0, hdc)
        return None

    bmi = BITMAPINFOHEADER()
    bmi.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.biWidth = size
    bmi.biHeight = -size
    bmi.biPlanes = 1
    bmi.biBitCount = 32
    bmi.biCompression = 0

    bits = ctypes.c_void_p()
    hbmp = gdi32.CreateDIBSection(
        hdc,
        ctypes.byref(bmi),
        0,
        ctypes.byref(bits),
        None,
        0,
    )
    if not hbmp or not bits.value:
        gdi32.DeleteDC(hdc_mem)
        user32.ReleaseDC(0, hdc)
        return None

    old = gdi32.SelectObject(hdc_mem, hbmp)
    user32.DrawIconEx(hdc_mem, 0, 0, hicon, size, size, 0, 0, 0x0003)
    buf = (ctypes.c_ubyte * (size * size * 4)).from_address(bits.value)
    rgba = bytearray(size * size * 4)
    for i in range(0, len(buf), 4):
        rgba[i] = buf[i + 2]
        rgba[i + 1] = buf[i + 1]
        rgba[i + 2] = buf[i]
        rgba[i + 3] = buf[i + 3]

    gdi32.SelectObject(hdc_mem, old)
    gdi32.DeleteObject(hbmp)
    gdi32.DeleteDC(hdc_mem)
    user32.ReleaseDC(0, hdc)
    return encode_png(bytes(rgba), size, size)


def _load_private_extract_icons():
    try:
        return ctypes.windll.shell32.PrivateExtractIconsW
    except (AttributeError, OSError):
        try:
            shell = ctypes.WinDLL("shell32")
            return shell.PrivateExtractIconsW
        except Exception:
            return None


def _decode_ico_file(path: str) -> Optional[bytes]:
    try:
        with open(path, "rb") as fh:
            data = fh.read()
    except OSError:
        return None
    if len(data) < 6 or data[:4] != b"\x00\x00\x01\x00":
        return None
    count = struct.unpack("<H", data[4:6])[0]
    if count <= 0:
        return None
    best: Optional[bytes] = None
    best_area = -1
    for i in range(count):
        offset = 6 + i * 16
        if offset + 16 > len(data):
            break
        _, width, height, _, _, size, image_offset = struct.unpack(
            "<BBBBHHII", data[offset : offset + 16]
        )
        w = width or 256
        h = height or 256
        area = w * h
        blob = data[image_offset : image_offset + size]
        if len(blob) < size:
            continue
        if blob.startswith(b"\x89PNG\r\n\x1a\n") and area >= best_area:
            best = blob
            best_area = area
    return best


def _extract_windows_icon(path: str, index: int) -> Optional[bytes]:
    if os.name != "nt" or not path or not os.path.isfile(path):
        return None
    if path.lower().endswith(".ico"):
        ico_png = _decode_ico_file(path)
        if ico_png:
            return ico_png
    shell32 = ctypes.windll.shell32
    user32 = ctypes.windll.user32
    private_extract = _load_private_extract_icons()
    hicon_large = (ctypes.c_void_p * 1)()
    for size in _ICON_SIZES:
        if private_extract is not None:
            try:
                count = private_extract(
                    path,
                    index,
                    size,
                    size,
                    ctypes.cast(hicon_large, ctypes.POINTER(ctypes.c_void_p)),
                    None,
                    1,
                    0,
                )
            except OSError:
                count = 0
            if count > 0 and hicon_large[0]:
                hicon = int(hicon_large[0])
                try:
                    png = _icon_from_hicon(hicon, size)
                    if png:
                        return png
                finally:
                    user32.DestroyIcon(hicon)
        hsmall = (ctypes.c_void_p * 1)()
        hlarge = (ctypes.c_void_p * 1)()
        count = shell32.ExtractIconExW(
            path,
            index,
            ctypes.cast(hlarge, ctypes.POINTER(ctypes.c_void_p)),
            ctypes.cast(hsmall, ctypes.POINTER(ctypes.c_void_p)),
            1,
        )
        if count > 0:
            hicon = int(hlarge[0] or hsmall[0] or 0)
            if hicon:
                try:
                    png = _icon_from_hicon(hicon, size)
                    if png:
                        return png
                finally:
                    user32.DestroyIcon(hicon)
    return None


def icon_for_installed(entry: InstalledIconEntry | Dict[str, Any]) -> Optional[bytes]:
    if isinstance(entry, dict):
        entry = InstalledIconEntry(
            package_id=str(entry.get("package_id") or entry.get("id") or ""),
            name=str(entry.get("name") or ""),
            origin=str(entry.get("origin") or "catalog"),
            arp_id=str(entry.get("arp_id") or ""),
            display_icon=str(entry.get("display_icon") or ""),
            install_location=str(entry.get("install_location") or ""),
        )

    arp: Optional[ArpEntry] = None
    if entry.arp_id or (entry.package_id or "").startswith("arp:"):
        arp = find_arp_entry(entry.arp_id or entry.package_id)
    elif entry.origin == "this-pc" and entry.package_id.startswith("arp:"):
        arp = find_arp_entry(entry.package_id)
    elif entry.package_id and not entry.package_id.startswith("arp:"):
        arp = find_arp_for_winget(entry.package_id, entry.name)

    display_icon = entry.display_icon
    install_location = entry.install_location
    if arp:
        if not display_icon:
            display_icon = _read_arp_value(arp, "DisplayIcon")
        if not install_location:
            install_location = arp.install_location or _read_arp_value(arp, "InstallLocation")

    if not install_location and entry.package_id and not entry.package_id.startswith("arp:"):
        slug = entry.package_id.rsplit(".", 1)[-1]
        for guess in (
            os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", slug),
            os.path.join(os.environ.get("LOCALAPPDATA", ""), "Programs", slug.casefold()),
            os.path.join(os.environ.get("ProgramFiles", ""), slug),
        ):
            if guess and os.path.isdir(guess):
                install_location = guess
                break

    paths: List[Tuple[str, int]] = []
    if display_icon:
        icon_path, icon_idx = parse_display_icon(display_icon)
        if icon_path:
            paths.append((icon_path, icon_idx))

    main_exe = find_main_exe(install_location, entry.name)
    if main_exe:
        paths.append((main_exe, 0))

    seen = set()
    for path, idx in paths:
        key = (path.casefold(), idx)
        if key in seen:
            continue
        seen.add(key)
        if path.lower().endswith(".ico"):
            data = _extract_windows_icon(path, idx)
            if data:
                return data
        elif path.lower().endswith((".exe", ".dll")):
            data = _extract_windows_icon(path, idx)
            if data:
                return data
        else:
            data = _extract_windows_icon(path, idx)
            if data:
                return data
    return None


class _IconLinkParser(html.parser.HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.links: List[Tuple[str, int]] = []
        self.manifests: List[str] = []

    def handle_starttag(self, tag: str, attrs: List[Tuple[str, Optional[str]]]) -> None:
        attrs_dict = {k.lower(): (v or "") for k, v in attrs}
        if tag.lower() == "link":
            rel = attrs_dict.get("rel", "").lower()
            href = attrs_dict.get("href", "").strip()
            if not href:
                return
            if "manifest" in rel:
                self.manifests.append(href)
                return
            score = 0
            if "apple-touch-icon" in rel:
                score = 100
            elif "icon" in rel or "shortcut icon" in rel:
                score = 80
            sizes = attrs_dict.get("sizes", "")
            px = 0
            m = _SIZE_RE.search(sizes)
            if m:
                px = max(int(m.group(1)), int(m.group(2)))
            if score:
                self.links.append((href, score + px))


class _HttpsOnlyRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if not (newurl or "").lower().startswith("https://"):
            return None
        return urllib.request.Request(
            newurl,
            headers=req.headers,
            method=req.get_method(),
        )


_FETCH_OPENER = urllib.request.build_opener(_HttpsOnlyRedirectHandler())


def _fetch_bytes(url: str, max_bytes: int) -> Optional[bytes]:
    if not url.lower().startswith("https://"):
        return None
    req = urllib.request.Request(url, headers={"User-Agent": "QuantumUpdater/1.0"})
    try:
        with _FETCH_OPENER.open(req, timeout=_FETCH_TIMEOUT) as resp:
            final = resp.geturl()
            if not (final or url).lower().startswith("https://"):
                return None
            data = resp.read(max_bytes + 1)
            if len(data) > max_bytes:
                return None
            return data
    except (urllib.error.URLError, OSError, TimeoutError):
        return None


def _resolve_url(base: str, href: str) -> str:
    return urllib.parse.urljoin(base, href)


def _collect_declared_icon_urls(homepage_url: str, html_bytes: bytes) -> Tuple[set, Optional[str]]:
    declared: set = set()
    text = html_bytes.decode("utf-8", errors="ignore")
    parser = _IconLinkParser()
    try:
        parser.feed(text)
    except html.parser.HTMLParseError:
        pass
    best_href = ""
    best_score = -1
    for href, score in parser.links:
        full = _resolve_url(homepage_url, href)
        if not full.lower().startswith("https://"):
            continue
        declared.add(full)
        if score > best_score:
            best_score = score
            best_href = full
    for manifest_href in parser.manifests:
        manifest_url = _resolve_url(homepage_url, manifest_href)
        if not manifest_url.lower().startswith("https://"):
            continue
        declared.add(manifest_url)
        manifest_bytes = _fetch_bytes(manifest_url, _HTML_MAX_BYTES)
        if not manifest_bytes:
            continue
        try:
            data = json.loads(manifest_bytes.decode("utf-8", errors="ignore"))
        except json.JSONDecodeError:
            continue
        icons = data.get("icons") or []
        for item in icons:
            if not isinstance(item, dict):
                continue
            src = str(item.get("src") or "").strip()
            if not src:
                continue
            full = _resolve_url(manifest_url, src)
            if not full.lower().startswith("https://"):
                continue
            declared.add(full)
            sizes = str(item.get("sizes") or "0")
            first = sizes.split()[0] if sizes else "0"
            try:
                size = int(first.split("x")[0])
            except ValueError:
                size = 0
            if size + 70 > best_score:
                best_score = size + 70
                best_href = full
    favicon = _resolve_url(homepage_url, "/favicon.ico")
    if favicon.lower().startswith("https://"):
        declared.add(favicon)
    if not best_href and icon_url_allowed(homepage_url, favicon, declared):
        best_href = favicon
    return declared, (best_href or None)


def parse_winget_show_homepage(text: str) -> str:
    for line in (text or "").splitlines():
        if ":" not in line:
            continue
        key, val = line.split(":", 1)
        key_fold = key.strip().casefold()
        if key_fold in ("homepage", "publisher url", "publisher support url"):
            url = val.strip()
            if url.lower().startswith("https://"):
                return url
    return ""


def _homepage_cache_path(package_id: str) -> str:
    return os.path.join(cache_dir(), f"{cache_key(package_id)}.home.json")


def _read_homepage_cache(package_id: str) -> str:
    path = _homepage_cache_path(package_id)
    if not os.path.isfile(path):
        return ""
    try:
        with open(path, "r", encoding="utf-8") as fh:
            payload = json.load(fh)
        url = str(payload.get("url") or "").strip()
        ts = float(payload.get("ts") or 0)
        if url.lower().startswith("https://") and time.time() - ts < _HOMEPAGE_TTL_SECONDS:
            return url
    except (OSError, json.JSONDecodeError, ValueError, TypeError):
        pass
    return ""


def _write_homepage_cache(package_id: str, url: str) -> None:
    if not url.lower().startswith("https://"):
        return
    try:
        with open(_homepage_cache_path(package_id), "w", encoding="utf-8") as fh:
            json.dump({"url": url, "ts": time.time()}, fh)
    except OSError:
        pass


def _has_cached_icon(package_id: str) -> bool:
    paths = _cache_paths(cache_key(package_id))
    if os.path.isfile(paths["png"]):
        return True
    meta_path = paths["meta"]
    if os.path.isfile(meta_path):
        try:
            with open(meta_path, "r", encoding="utf-8") as fh:
                meta = json.load(fh)
            rel = meta.get("file", "")
            if rel and os.path.isfile(os.path.join(cache_dir(), rel)):
                return True
        except (OSError, json.JSONDecodeError):
            pass
    return False


def winget_homepage(package_id: str) -> str:
    if os.name != "nt" or (package_id or "").startswith("arp:"):
        return ""
    if _has_cached_icon(package_id):
        return _read_homepage_cache(package_id)
    cached = _read_homepage_cache(package_id)
    if cached:
        return cached
    from core.winget_engine import WingetEngine

    engine = WingetEngine()
    if not engine.is_available() or not engine._executable:
        return ""
    try:
        res = engine._run_capture(
            [
                engine._executable,
                "show",
                "--id",
                package_id,
                "--exact",
                "--disable-interactivity",
                "--accept-source-agreements",
            ],
            timeout=20,
        )
        text = (res.stdout or "") + (res.stderr or "")
    except Exception:
        return ""
    url = parse_winget_show_homepage(text)
    if url:
        _write_homepage_cache(package_id, url)
    return url


def _is_catalog_id(package_id: str) -> bool:
    return bool(package_id) and not package_id.startswith("arp:")


def _match_installed_inventory(
    package_id: str,
    meta: Dict[str, Any],
    inventory: List[Dict[str, Any]],
) -> Optional[Dict[str, Any]]:
    name = str(meta.get("name") or "").strip()
    pid_fold = package_id.casefold()
    for item in inventory:
        iid = str(item.get("id") or "").casefold()
        iname = str(item.get("name") or "").strip()
        if iid and iid == pid_fold:
            return item
        if name and iname.casefold() == name.casefold():
            return item
    return None


_RESOLVE_TRACE: Dict[str, str] = {}


def last_icon_resolve_trace() -> Dict[str, str]:
    return dict(_RESOLVE_TRACE)


def _png_size(data: bytes) -> Optional[Tuple[int, int]]:
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    width, height = struct.unpack(">II", data[16:24])
    if width <= 0 or height <= 0:
        return None
    return width, height


def _acceptable_icon(data: bytes) -> bool:
    """Keep square-ish icons. Drop banners and tiny tracking pixels."""
    mime = detect_image_mime(data)
    if mime == "image/svg+xml":
        return True
    if mime != "image/png":
        return True
    size = _png_size(data)
    if not size:
        return False
    width, height = size
    if width < 16 or height < 16:
        return False
    if max(width, height) / min(width, height) > 1.6:
        return False
    return True


def icon_for_catalog(
    package_id: str,
    homepage_url: str,
) -> Tuple[Optional[bytes], Optional[str]]:
    homepage = (homepage_url or "").strip()
    if not homepage.lower().startswith("https://"):
        return None, None
    html_bytes = _fetch_bytes(homepage, _HTML_MAX_BYTES)
    if not html_bytes:
        return None, None
    declared, icon_url = _collect_declared_icon_urls(homepage, html_bytes)
    if not icon_url:
        return None, None
    if not icon_url_allowed(homepage, icon_url, declared):
        return None, None
    icon_bytes = _fetch_bytes(icon_url, _ICON_MAX_BYTES)
    if not icon_bytes or not _acceptable_icon(icon_bytes):
        return None, None
    mime = detect_image_mime(icon_bytes)
    if not mime:
        return None, None
    if mime == "image/svg+xml":
        text = icon_bytes.decode("utf-8", errors="ignore")
        if not svg_is_safe(text):
            return None, None
    return icon_bytes, mime


def bytes_to_data_url(data: bytes, mime: str = "image/png") -> str:
    encoded = base64.b64encode(data).decode("ascii")
    return f"data:{mime};base64,{encoded}"


def _resolve_one(
    package_id: str,
    homepage: str = "",
    installed_meta: Optional[Dict[str, Any]] = None,
    installed_inventory: Optional[List[Dict[str, Any]]] = None,
) -> Optional[bytes]:
    with _MEMORY_LOCK:
        if package_id in _MEMORY_CACHE:
            return _MEMORY_CACHE[package_id]
    cached = _memory_get(package_id)
    if cached is not None:
        return cached
    disk = _read_disk_cache(package_id)
    if disk is not None:
        _memory_put(package_id, disk)
        return disk
    paths = _cache_paths(cache_key(package_id))
    if os.path.isfile(paths["neg"]):
        try:
            with open(paths["neg"], "r", encoding="utf-8") as fh:
                ts = float((fh.read() or "0").strip())
            if time.time() - ts < _NEGATIVE_TTL_SECONDS:
                _memory_put(package_id, None)
                return None
        except Exception:
            pass

    data: Optional[bytes] = None
    mime = "image/png"
    meta = installed_meta or {}
    inventory = installed_inventory or []
    matched = _match_installed_inventory(package_id, meta, inventory)
    is_installed = (
        (package_id or "").startswith("arp:")
        or meta.get("origin") == "this-pc"
        or meta.get("installed") is True
        or matched is not None
    )
    if not is_installed and find_arp_for_winget(package_id, meta.get("name", "")):
        is_installed = True

    if is_installed:
        entry = InstalledIconEntry(
            package_id=package_id,
            name=str(meta.get("name") or (matched or {}).get("name") or ""),
            origin=str(meta.get("origin") or "catalog"),
            arp_id=str(meta.get("arp_id") or (package_id if package_id.startswith("arp:") else "")),
            display_icon=str(meta.get("display_icon") or ""),
            install_location=str(meta.get("install_location") or ""),
        )
        data = icon_for_installed(entry)
        if data:
            _trace(package_id, "local program file")
            _write_disk_cache(package_id, data, "image/png")
            _memory_put(package_id, data)
            return data
        if not _is_catalog_id(package_id):
            _write_disk_cache(package_id, None)
            _memory_put(package_id, None)
            _trace(package_id, "no local icon")
            return None

    home = homepage or meta.get("homepage") or ""
    if not home and _is_catalog_id(package_id):
        home = _read_homepage_cache(package_id)
    if not home and _is_catalog_id(package_id) and not _has_cached_icon(package_id):
        home = winget_homepage(package_id)
    if home:
        raw, mime = icon_for_catalog(package_id, home)
        if raw and mime:
            _trace(package_id, f"publisher site ({home})")
            if mime == "image/png":
                data = raw
            elif mime == "image/svg+xml":
                data = raw
            elif mime == "image/x-icon":
                ico_path = os.path.join(cache_dir(), f"_tmp_{cache_key(package_id)}.ico")
                try:
                    with open(ico_path, "wb") as fh:
                        fh.write(raw)
                    data = _extract_windows_icon(ico_path, 0) or raw
                finally:
                    try:
                        os.remove(ico_path)
                    except OSError:
                        pass
                mime = "image/png" if data and detect_image_mime(data) == "image/png" else mime
            if data:
                _write_disk_cache(package_id, data, mime)
                _memory_put(package_id, data)
                return data
    _write_disk_cache(package_id, None)
    _memory_put(package_id, None)
    _trace(package_id, "no icon")
    return None


def _trace(package_id: str, message: str) -> None:
    _RESOLVE_TRACE[package_id] = message


def resolve_icons_batch(
    ids: List[str],
    homepages: Optional[Dict[str, str]] = None,
    installed_meta: Optional[Dict[str, Dict[str, Any]]] = None,
    installed_inventory: Optional[List[Dict[str, Any]]] = None,
    network_workers: int = 4,
) -> Dict[str, Optional[str]]:
    homepages = homepages or {}
    installed_meta = installed_meta or {}
    inventory = installed_inventory or []
    _RESOLVE_TRACE.clear()
    out: Dict[str, Optional[str]] = {}
    if not ids:
        return out

    network_ids: List[str] = []
    for pid in ids:
        meta = installed_meta.get(pid, {})
        matched = _match_installed_inventory(pid, meta, inventory)
        if pid.startswith("arp:") or meta.get("origin") == "this-pc" or matched:
            try:
                data = _resolve_one(pid, homepages.get(pid, ""), meta, inventory)
                out[pid] = bytes_to_data_url(data, detect_image_mime(data) or "image/png") if data else None
            except Exception:
                logger.debug("icon resolve failed for %s", pid, exc_info=True)
                out[pid] = None
            continue
        if find_arp_for_winget(pid, meta.get("name", "")):
            try:
                data = _resolve_one(pid, homepages.get(pid, ""), meta, inventory)
                out[pid] = bytes_to_data_url(data, detect_image_mime(data) or "image/png") if data else None
            except Exception:
                out[pid] = None
            continue
        network_ids.append(pid)

    if network_ids:
        workers = max(1, min(network_workers, 4))
        with ThreadPoolExecutor(max_workers=workers) as pool:
            futures = {
                pool.submit(
                    _resolve_one,
                    pid,
                    homepages.get(pid, ""),
                    installed_meta.get(pid, {}),
                    inventory,
                ): pid
                for pid in network_ids
            }
            for future in as_completed(futures):
                pid = futures[future]
                try:
                    data = future.result()
                    if data:
                        mime = detect_image_mime(data) or "image/png"
                        out[pid] = bytes_to_data_url(data, mime)
                    else:
                        out[pid] = None
                except Exception:
                    logger.debug("catalog icon failed for %s", pid, exc_info=True)
                    out[pid] = None

    for pid in ids:
        out.setdefault(pid, None)
    return out
