import struct
import unittest

from core.hardware_facts import (
    _smbios_type17_speed_mts,
    battery_health_percent,
    detect_gpu_vendor,
    is_usb_display_adapter,
    usb_display_key,
    normalize_arch,
    normalize_serial,
    parse_cpu_brand_string,
)
from core.system_tools.drives import _is_virtual_label

CPU_SAMPLES = [
    (
        "AMD Ryzen 7 4800H with Radeon Graphics",
        {"vendor": "amd", "family": "Ryzen 7", "generation": "Ryzen 4000 series (Zen 2, Renoir)", "model_number": "4800H", "segment": "H"},
    ),
    (
        "13th Gen Intel(R) Core(TM) i7-1355U",
        {"vendor": "intel", "family": "Core i7", "generation": "13th Gen", "model_number": "1355U", "segment": "U"},
    ),
    (
        "Intel(R) Core(TM) Ultra 7 155H",
        {"vendor": "intel", "family": "Core Ultra 7", "generation": "Core Ultra series 1", "model_number": "155H", "segment": "H"},
    ),
    (
        "Snapdragon(R) X Elite - X1E78100 - Qualcomm(R) Oryon(TM) CPU",
        {"vendor": "qualcomm", "family": "Snapdragon X Elite"},
    ),
    (
        "Apple M2 Pro",
        {"vendor": "apple", "family": "Apple M2 Pro"},
    ),
    (
        "12th Gen Intel(R) Core(TM) i5-1240P",
        {"vendor": "intel", "family": "Core i5", "generation": "12th Gen", "model_number": "1240P", "segment": "P"},
    ),
    (
        "11th Gen Intel(R) Core(TM) i9-11900K",
        {"vendor": "intel", "family": "Core i9", "generation": "11th Gen", "model_number": "11900K", "segment": "K"},
    ),
    (
        "Intel(R) Core(TM) i3-10110U CPU @ 2.10GHz",
        {"vendor": "intel", "family": "Core i3", "model_number": "10110U", "segment": "U"},
    ),
    (
        "Intel(R) Core(TM) Ultra 5 125U",
        {"vendor": "intel", "family": "Core Ultra 5", "model_number": "125U", "segment": "U"},
    ),
    (
        "AMD Ryzen 9 5900X 12-Core Processor",
        {"vendor": "amd", "family": "Ryzen 9", "model_number": "5900X", "generation": "Ryzen 5000 series (Zen 3, Cezanne/Vermeer)", "segment": "X"},
    ),
    (
        "AMD Ryzen 5 3600 6-Core Processor",
        {"vendor": "amd", "family": "Ryzen 5", "model_number": "3600", "generation": "Ryzen 3000 series (Zen 2, Matisse)"},
    ),
    (
        "AMD Ryzen 7 7840U with Radeon Graphics",
        {"vendor": "amd", "family": "Ryzen 7", "model_number": "7840U", "generation": "Ryzen 7000 series (Zen 4, Raphael)", "segment": "U"},
    ),
    (
        "AMD Ryzen AI 9 HX 370",
        {"vendor": "amd", "family": "Ryzen AI 9", "generation": "Ryzen AI 300 series"},
    ),
    (
        "AMD Ryzen Threadripper 3970X 32-Core Processor",
        {"vendor": "amd", "family": "Threadripper 3970"},
    ),
    (
        "AMD Athlon 3000G with Radeon Graphics",
        {"vendor": "amd", "family": "Athlon 3000"},
    ),
    (
        "AMD EPYC 7713 64-Core Processor",
        {"vendor": "amd"},
    ),
    (
        "Snapdragon 7c Compute Platform",
        {"vendor": "qualcomm", "family": "Snapdragon 7c"},
    ),
    (
        "Qualcomm Snapdragon 8cx Gen 3",
        {"vendor": "qualcomm", "family": "Snapdragon 8cx"},
    ),
    (
        "Snapdragon X Plus",
        {"vendor": "qualcomm", "family": "Snapdragon X Plus"},
    ),
    (
        "Apple M1",
        {"vendor": "apple", "family": "Apple M1"},
    ),
    (
        "Apple M3 Max",
        {"vendor": "apple", "family": "Apple M3 Max"},
    ),
    (
        "Apple M4",
        {"vendor": "apple", "family": "Apple M4"},
    ),
    (
        "Intel(R) Xeon(R) CPU E5-2690 v4 @ 2.60GHz",
        {"vendor": "intel", "family": "Xeon"},
    ),
    (
        "Intel(R) Celeron(R) N4020 CPU @ 1.10GHz",
        {"vendor": "intel", "family": "Celeron"},
    ),
    (
        "Intel(R) Pentium(R) Silver N6000 CPU @ 1.10GHz",
        {"vendor": "intel", "family": "Pentium"},
    ),
    (
        "Microsoft SQ3",
        {"vendor": "qualcomm", "family": "Microsoft SQ"},
    ),
]


class CpuBrandParseTests(unittest.TestCase):
    def test_cpu_samples(self):
        for brand, expected in CPU_SAMPLES:
            with self.subTest(brand=brand):
                parsed = parse_cpu_brand_string(brand)
                for key, value in expected.items():
                    self.assertEqual(parsed.get(key), value)

    def test_arch_normalization(self):
        self.assertEqual(normalize_arch("AMD64"), "x64")
        self.assertEqual(normalize_arch("ARM64"), "arm64")


def _synthetic_type17_body(
    *,
    length: int = 84,
    size_kb: int = 8192,
    mem_type: int = 26,
    speed_mts: int = 0,
    configured_mts: int = 0,
) -> bytes:
    body = bytearray(length)
    body[0] = 17
    body[1] = length
    struct.pack_into("<H", body, 0x0C, size_kb)
    body[0x12] = mem_type
    struct.pack_into("<H", body, 0x15, speed_mts)
    if length >= 0x22:
        struct.pack_into("<H", body, 0x20, configured_mts)
    return bytes(body)


class SmbiosType17SpeedTests(unittest.TestCase):
    def test_prefers_configured_over_rated_speed(self):
        body = bytearray(_synthetic_type17_body(speed_mts=3200, configured_mts=3200))
        body[0x16] = 12
        self.assertEqual(_smbios_type17_speed_mts(bytes(body)), 3200)

    def test_rated_speed_when_configured_zero(self):
        body = _synthetic_type17_body(speed_mts=2666, configured_mts=0)
        self.assertEqual(_smbios_type17_speed_mts(body), 2666)


class BatteryHealthTests(unittest.TestCase):
    def test_battery_health_percent_live_values(self):
        self.assertEqual(battery_health_percent(90006, 64185), 71)

    def test_battery_health_capped_at_100(self):
        self.assertEqual(battery_health_percent(50000, 55000), 100)

    def test_battery_health_missing_capacity(self):
        self.assertIsNone(battery_health_percent(None, 64185))
        self.assertIsNone(battery_health_percent(90006, 0))


class StorageVirtualLabelTests(unittest.TestCase):
    def test_google_drive_gmail_labels_excluded(self):
        self.assertTrue(
            _is_virtual_label("happy143@gmail.com - Google Drive", "FAT32")
        )
        self.assertTrue(
            _is_virtual_label("mumbaiipn@gmail.com - Google Drive", "FAT32")
        )

    def test_physical_volume_labels_kept(self):
        self.assertFalse(_is_virtual_label("OS", "NTFS"))
        self.assertFalse(_is_virtual_label("1Tb SSD980", "NTFS"))


class SerialAndGpuTests(unittest.TestCase):
    def test_placeholder_serial_rejected(self):
        self.assertEqual(normalize_serial("To be filled by O.E.M."), "")
        self.assertEqual(normalize_serial("Default string"), "")
        self.assertEqual(normalize_serial("00000000"), "")

    def test_gpu_vendor_detection(self):
        self.assertEqual(detect_gpu_vendor("NVIDIA GeForce RTX 4060"), "nvidia")
        self.assertEqual(detect_gpu_vendor("AMD Radeon RX 6800", "Advanced Micro Devices, Inc."), "amd")
        self.assertEqual(detect_gpu_vendor("Intel(R) UHD Graphics 630", "Intel Corporation"), "intel")

    def test_displaylink_is_not_a_graphics_adapter(self):
        self.assertTrue(
            is_usb_display_adapter(
                "DisplayLink USB Device",
                "DisplayLink",
                r"USB\VID_17E9&PID_6006&MI_01",
            )
        )
        self.assertFalse(
            is_usb_display_adapter(
                "NVIDIA GeForce RTX 3050 Laptop GPU",
                "NVIDIA",
                r"PCI\VEN_10DE&DEV_25A2",
            )
        )
        self.assertFalse(
            is_usb_display_adapter(
                "AMD Radeon(TM) Graphics",
                "Advanced Micro Devices, Inc.",
                r"PCI\VEN_1002&DEV_1636",
            )
        )

    def test_one_displaylink_dock_is_one_device(self):
        first = usb_display_key("DisplayLink USB Device", r"USB\VID_17E9&PID_6006&MI_00")
        second = usb_display_key("DisplayLink USB Device", r"USB\VID_17E9&PID_6006&MI_01")
        self.assertEqual(first, second)


if __name__ == "__main__":
    unittest.main()
