import hashlib
import os
import tempfile
import unittest
from unittest.mock import MagicMock, patch

from core.app_icons import (
    CACHE_VERSION,
    cache_dir,
    cache_key,
    detect_image_mime,
    expand_win_path,
    icon_for_catalog,
    icon_url_allowed,
    parse_display_icon,
    parse_winget_show_homepage,
    same_registrable_domain,
    svg_is_safe,
    _collect_declared_icon_urls,
    _write_disk_cache,
    _read_disk_cache,
)


class DisplayIconParseTests(unittest.TestCase):
    def test_strip_quotes_and_index(self):
        path, idx = parse_display_icon('"C:\\Program Files\\App\\app.exe",0')
        self.assertEqual(idx, 0)
        self.assertTrue(path.endswith("app.exe"))

    def test_trailing_comma_index(self):
        path, idx = parse_display_icon("C:\\Icons\\foo.dll,3")
        self.assertEqual(idx, 3)
        self.assertIn("foo.dll", path)

    def test_expand_programfiles_env(self):
        pf = os.environ.get("ProgramFiles", r"C:\Program Files")
        raw = r"%ProgramFiles%\Vendor\app.exe,1"
        path, idx = parse_display_icon(raw)
        self.assertEqual(idx, 1)
        self.assertTrue(path.lower().startswith(pf.lower()))


class DomainAndMagicTests(unittest.TestCase):
    def test_same_domain_subdomain_allowed(self):
        self.assertTrue(
            same_registrable_domain(
                "https://www.example.com/app",
                "https://cdn.example.com/favicon.png",
            )
        )

    def test_cross_domain_rejected(self):
        self.assertFalse(
            same_registrable_domain(
                "https://example.com",
                "https://evil.net/icon.png",
            )
        )

    def test_cdn_declared_in_html_accepted(self):
        home = "https://www.mozilla.org/firefox/"
        html = (
            b'<html><head>'
            b'<link rel="icon" href="https://firefox.cdn.mozilla.net/icon.png" />'
            b"</head></html>"
        )
        declared, best = _collect_declared_icon_urls(home, html)
        cdn = "https://firefox.cdn.mozilla.net/icon.png"
        self.assertIn(cdn, declared)
        self.assertTrue(icon_url_allowed(home, cdn, declared))
        self.assertEqual(best, cdn)

    def test_undeclared_off_domain_rejected(self):
        self.assertFalse(
            icon_url_allowed(
                "https://www.example.com/",
                "https://cdn.other.net/icon.png",
                set(),
            )
        )

    def test_http_rejected(self):
        self.assertFalse(icon_url_allowed("https://example.com", "http://example.com/i.png", set()))

    def test_magic_png(self):
        self.assertEqual(detect_image_mime(b"\x89PNG\r\n\x1a\n\x00"), "image/png")

    def test_magic_ico(self):
        self.assertEqual(detect_image_mime(b"\x00\x00\x01\x00\x01\x00"), "image/x-icon")

    def test_svg_rejects_script(self):
        self.assertFalse(svg_is_safe('<svg><script>alert(1)</script></svg>'))

    def test_svg_rejects_external_href(self):
        self.assertFalse(svg_is_safe('<svg><a href="https://evil.com"></a></svg>'))


class CacheKeyTests(unittest.TestCase):
    def test_cache_key_stable_and_negative_ttl(self):
        pid = "Publisher.App"
        key = cache_key(pid)
        self.assertEqual(len(key), 64)
        self.assertEqual(key, cache_key(pid))
        self.assertNotEqual(
            hashlib.sha256(b"1:publisher.app").hexdigest(),
            hashlib.sha256(f"{CACHE_VERSION}:publisher.app".encode()).hexdigest(),
        )
        with tempfile.TemporaryDirectory() as tmp:
            os.environ["LOCALAPPDATA"] = tmp
            _write_disk_cache(pid, None)
            self.assertIsNone(_read_disk_cache(pid))
            neg_path = os.path.join(cache_dir(), f"{key}.neg")
            self.assertTrue(os.path.isfile(neg_path))


class WingetHomepageParseTests(unittest.TestCase):
    def test_parse_homepage_from_sample(self):
        sample = """
Name: Mozilla Firefox (en-US)
Publisher: Mozilla
Homepage: https://www.mozilla.org/en-US/firefox/new/
License: MPL-2.0
Publisher Url: https://www.mozilla.org/
"""
        self.assertEqual(
            parse_winget_show_homepage(sample),
            "https://www.mozilla.org/en-US/firefox/new/",
        )


class CatalogFetchOfflineTests(unittest.TestCase):
    def test_invalid_homepage_returns_none(self):
        data, mime = icon_for_catalog("test.pkg", "http://insecure.example.com")
        self.assertIsNone(data)
        self.assertIsNone(mime)


class GetAppIconsBridgeTests(unittest.TestCase):
    @patch("core.app_icons.resolve_icons_batch")
    def test_positional_homepages_and_meta_reach_resolve_batch(self, mock_resolve):
        mock_resolve.return_value = {"arpa:orca": None}
        from web_api import WebAPI

        api = WebAPI(MagicMock())
        api.manager.cached_installed = []
        ids = ["arpa:orca"]
        homepages = {"Orca.Orca": "https://www.orca.example.com/"}
        meta = {
            "arpa:orca": {
                "name": "Orca",
                "origin": "this-pc",
                "install_location": r"C:\Program Files\Orca",
            }
        }
        out = api.get_app_icons(ids, homepages, meta)
        self.assertIn("arpa:orca", out)
        mock_resolve.assert_called_once()
        call_kwargs = mock_resolve.call_args.kwargs
        self.assertEqual(call_kwargs["homepages"], homepages)
        self.assertEqual(call_kwargs["installed_meta"], meta)
        self.assertEqual(call_kwargs["installed_inventory"], [])


if __name__ == "__main__":
    unittest.main()
