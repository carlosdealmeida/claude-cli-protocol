"""Tests of the pure parts of sweep_issues.py. Usage: python -I docs/test_sweep_issues.py -v"""
import importlib.util
import pathlib
import unittest

_SPEC = importlib.util.spec_from_file_location("sweep_issues", pathlib.Path(__file__).with_name("sweep_issues.py"))
sweep = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(sweep)


class Sweep(unittest.TestCase):
    def test_protocol_terms_are_recognised(self):
        for text in ("--output-format stream-json drops the result line",
                     "control_request can_use_tool never answered",
                     "claude -p hangs with --input-format stream-json",
                     "--permission-prompt-tool stdio",
                     "--include-partial-messages emits no stream_event"):
            self.assertTrue(sweep.matches_protocol(text), text)
        self.assertFalse(sweep.matches_protocol("Sidebar icon is misaligned in the desktop app"))

    def test_billing_noise_is_dropped(self):
        self.assertTrue(sweep.is_noise("Usage credits required after upgrading"))
        self.assertFalse(sweep.is_noise("stream-json output is missing usage"))

    def test_rank_drops_known_and_noise_and_sorts_by_reactions(self):
        issues = [{"number": 1, "title": "stream-json a", "reactions": 3},
                  {"number": 2, "title": "stream-json b", "reactions": 9},
                  {"number": 3, "title": "Usage credits required", "reactions": 99},
                  {"number": 4, "title": "stream-json c", "reactions": 50}]
        self.assertEqual([i["number"] for i in sweep.rank(issues, known={4})], [2, 1])

    def test_rank_drops_what_does_not_touch_the_protocol(self):
        issues = [{"number": 1, "title": "Sidebar icon is misaligned", "reactions": 80, "body": "desktop app"},
                  {"number": 2, "title": "Hang in headless runs", "reactions": 1, "body": "with --output-format stream-json"}]
        self.assertEqual([i["number"] for i in sweep.rank(issues, known=set())], [2])

    def test_changelog_hits_start_after_the_given_version(self):
        md = ("## 2.1.312\n- Fixed stream-json result line\n- New sidebar\n"
              "## 2.1.311\n- control_request change\n"
              "## 2.1.310\n- stream-json old\n")
        self.assertEqual(sweep.changelog_hits(md, "2.1.310"),
                         [("2.1.312", "Fixed stream-json result line"), ("2.1.311", "control_request change")])

    def test_changelog_hits_compare_versions_as_numbers(self):
        md = "## 2.1.99\n- stream-json older\n## 2.1.100\n- stream-json newer\n"
        self.assertEqual(sweep.changelog_hits(md, "2.1.99"), [("2.1.100", "stream-json newer")])


if __name__ == "__main__":
    unittest.main()
