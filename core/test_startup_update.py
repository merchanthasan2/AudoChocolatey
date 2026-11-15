import unittest
from unittest.mock import MagicMock

from web_api import WebAPI


class StartupUpdateTests(unittest.TestCase):
    def test_preload_caches_result(self):
        manager = MagicMock()
        manager.updater.check_for_updates.return_value = {
            "has_update": True,
            "latest_version": "2.0.1",
            "current_version": "2.0.0",
            "download_url": "https://github.com/merchanthasan2/AudoChocolatey/releases/download/v2.0.1/QuantumUpdater.exe",
        }
        api = WebAPI(manager)
        api.preload_self_update_check()
        cached = api.get_startup_update()
        self.assertTrue(cached.get("preloaded"))
        self.assertTrue(cached.get("has_update"))
        again = api.check_self_update()
        self.assertTrue(again.get("has_update"))
        manager.updater.check_for_updates.assert_called_once()

    def test_force_bypasses_cache(self):
        manager = MagicMock()
        manager.updater.check_for_updates.side_effect = [
            {"has_update": False, "current_version": "2.0.0"},
            {"has_update": True, "latest_version": "2.0.2", "current_version": "2.0.0"},
        ]
        api = WebAPI(manager)
        api.preload_self_update_check()
        forced = api.check_self_update(force=True)
        self.assertTrue(forced.get("has_update"))
        self.assertEqual(manager.updater.check_for_updates.call_count, 2)


if __name__ == "__main__":
    unittest.main()
