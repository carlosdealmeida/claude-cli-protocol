"""Checks the shape of the repository: the files every phase relies on, the translations and the links.

Usage: python -I tools/test_repo.py -v

Only the standard library is needed. The tests that read YAML use PyYAML and are skipped without it.
"""
import pathlib
import re
import subprocess
import unittest

ROOT = pathlib.Path(__file__).resolve().parent.parent

# In the order pt-BR, en, es, zh-cn, zh-tw.
LANGUAGE_LABELS = ("Português", "English", "Español", "简体中文", "繁體中文")

_FENCE = re.compile(r"^(```|~~~).*?^\1[^\n]*$", re.MULTILINE | re.DOTALL)
_CODE_SPAN = re.compile(r"`[^`\n]*`")
_LINK = re.compile(r"\[[^\]]*\]\(([^)\s]+)[^)]*\)")


def read(rel):
    """The UTF-8 text of a file of the repository."""
    return (ROOT / rel).read_text(encoding="utf-8")


def tracked():
    """The files git knows, as paths relative to the root."""
    out = subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8",
                         check=True).stdout
    return [line for line in out.splitlines() if line]


def relative_links(text):
    """The target of each Markdown link that points inside the repository, without its anchor.

    Links to the web, to an e-mail address or to an anchor of the same page are left out, and so is anything
    inside a fenced block or a code span: the plans show links as examples there.
    """
    text = _CODE_SPAN.sub("", _FENCE.sub("", text))
    links = []
    for target in _LINK.findall(text):
        if target.startswith(("http://", "https://", "mailto:", "#")):
            continue
        links.append(target.split("#", 1)[0])
    return links


class BaseFiles(unittest.TestCase):
    def test_base_files_exist(self):
        for rel in (".gitattributes", ".editorconfig", ".gitignore", "LICENSE"):
            self.assertTrue((ROOT / rel).is_file(), rel)

    def test_license_is_mit_and_names_the_author(self):
        text = read("LICENSE")
        self.assertIn("MIT License", text)
        self.assertIn("Copyright (c) 2026 carlosdealmeida", text)

    def test_index_has_no_crlf(self):
        out = subprocess.run(["git", "ls-files", "--eol"], cwd=ROOT, capture_output=True, text=True,
                             encoding="utf-8", check=True).stdout
        bad = [line for line in out.splitlines() if line.startswith("i/crlf") or line.startswith("i/mixed")]
        self.assertEqual(bad, [])

    def test_relative_links_resolve(self):
        for rel in [p for p in tracked() if p.endswith(".md") and ("/" not in p or p.startswith("docs/"))]:
            for link in relative_links(read(rel)):
                self.assertTrue((ROOT / rel).parent.joinpath(link).exists(), f"{rel} -> {link}")


if __name__ == "__main__":
    unittest.main()
