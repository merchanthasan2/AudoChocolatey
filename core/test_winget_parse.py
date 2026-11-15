import unittest

from core.winget_engine import WingetEngine, _EXPLICIT_TARGETING_SPLIT

WINGET_TABLE_FIXTURE = """
Name                    Id                          Version    Available  Source
--------------------------------------------------------------------------------
Git                     Git.Git                     2.43.0     2.44.0     winget
Some Very Long Name Th… 9NBLGGH4NNS1                1.0.0      1.0.1      msstore
Downloading package  [####################################]  62%
The following packages have an upgrade available, but require explicit targeting for upgrade:
Name                    Id                          Version    Available  Source
--------------------------------------------------------------------------------
Discord                 Discord.Discord             1.0.9015   1.0.9018   winget
"""


class WingetParseTests(unittest.TestCase):
    def setUp(self):
        self.engine = WingetEngine.__new__(WingetEngine)

    def test_parse_table_expected_ids(self):
        rows = self.engine._parse_table(WINGET_TABLE_FIXTURE)
        ids = [row["id"] for row in rows]
        self.assertEqual(ids, ["Git.Git", "9NBLGGH4NNS1", "Discord.Discord"])

    def test_long_name_ellipsis_trimmed(self):
        rows = self.engine._parse_table(WINGET_TABLE_FIXTURE)
        msstore = next(r for r in rows if r["id"] == "9NBLGGH4NNS1")
        self.assertEqual(msstore["name"], "Some Very Long Name Th")

    def test_explicit_targeting_split_flexible(self):
        text = (
            "Name                    Id            Version    Available  Source\n"
            "------------------------------------------------------------\n"
            "Foo                     Foo.Bar       1.0        2.0        winget\n"
            "The following packages have upgrades available but need explicit targeting for upgrade:\n"
            "Name                    Id            Version    Available  Source\n"
            "------------------------------------------------------------\n"
            "Bar                     Bar.Baz       1.0        2.0        winget\n"
        )
        parts = _EXPLICIT_TARGETING_SPLIT.split(text, maxsplit=1)
        self.assertEqual(len(parts), 2)
        main_ids = [r["id"] for r in self.engine._parse_table(parts[0])]
        explicit_ids = [r["id"] for r in self.engine._parse_table(parts[1])]
        self.assertEqual(main_ids, ["Foo.Bar"])
        self.assertEqual(explicit_ids, ["Bar.Baz"])


if __name__ == "__main__":
    unittest.main()
