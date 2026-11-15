import unittest

from core.winget_user_context import (
    WINGET_ERROR_CANNOT_RUN_AS_ADMIN,
    build_user_context_install_command,
    build_winget_install_args,
    is_needs_user_context_failure,
    winget_hresult_to_exit_code,
)


class WingetUserContextTests(unittest.TestCase):
    def test_hresult_maps_to_signed_exit_code(self):
        self.assertEqual(winget_hresult_to_exit_code("0x8A150056"), WINGET_ERROR_CANNOT_RUN_AS_ADMIN)

    def test_admin_context_code_classified_as_needs_user_context(self):
        output = "The installer cannot be run from an administrator context."
        self.assertTrue(is_needs_user_context_failure(WINGET_ERROR_CANNOT_RUN_AS_ADMIN, output))

    def test_admin_context_phrase_without_code(self):
        output = "Error: The installer cannot be run from an administrator context."
        self.assertTrue(is_needs_user_context_failure(1, output))

    def test_spotify_retry_command_argv(self):
        winget = r"C:\Users\me\AppData\Local\Microsoft\WindowsApps\winget.exe"
        argv = build_user_context_install_command(
            winget,
            "Spotify.Spotify",
            silent=True,
            accept_agreements=True,
        )
        self.assertEqual(
            argv,
            [
                winget,
                "install",
                "--id",
                "Spotify.Spotify",
                "--exact",
                "--disable-interactivity",
                "--silent",
                "--accept-package-agreements",
                "--accept-source-agreements",
            ],
        )
        self.assertEqual(
            build_winget_install_args("Spotify.Spotify"),
            [
                "install",
                "--id",
                "Spotify.Spotify",
                "--exact",
                "--disable-interactivity",
                "--silent",
                "--accept-package-agreements",
                "--accept-source-agreements",
            ],
        )


if __name__ == "__main__":
    unittest.main()
