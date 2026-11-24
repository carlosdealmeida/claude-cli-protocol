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


ADVISORIES = "https://github.com/carlosdealmeida/claude-cli-protocol/security/advisories/new"


def load_yaml(rel):
    """The YAML document of a file of the repository; the test is skipped without PyYAML."""
    try:
        import yaml
    except ImportError:
        raise unittest.SkipTest("PyYAML not installed")
    return yaml.safe_load(read(rel))


class ReleaseDocs(unittest.TestCase):
    def test_changelog_follows_keep_a_changelog_and_has_nothing_released(self):
        text = read("CHANGELOG.md")
        self.assertIn("https://keepachangelog.com/en/1.1.0/", text)
        self.assertIn("## [Unreleased]", text)
        self.assertNotRegex(text, r"## \[\d+\.\d+\.\d+\]")

    def test_issue_templates(self):
        config = load_yaml(".github/ISSUE_TEMPLATE/config.yml")
        self.assertIs(config["blank_issues_enabled"], False)
        self.assertEqual(config["contact_links"][0]["url"], ADVISORIES)
        bug = load_yaml(".github/ISSUE_TEMPLATE/bug_report.yml")
        ids = [item.get("id") for item in bug["body"]]
        for wanted in ("description", "steps", "library", "lib-version", "claude-version", "os"):
            self.assertIn(wanted, ids)
        self.assertEqual(load_yaml(".github/ISSUE_TEMPLATE/feature_request.yml")["labels"], ["enhancement"])

    def test_releasing_names_the_tag_and_the_only_secret_it_needs(self):
        text = read("RELEASING.md")
        self.assertIn("v<semver>", text)
        self.assertIn("NUGET_USER", text)


class Policies(unittest.TestCase):
    SECURITY = ("SECURITY.md", "SECURITY.en.md", "SECURITY.es.md")

    def test_three_languages_each_with_the_language_line(self):
        for base in ("SECURITY", "CONTRIBUTING"):
            names = [f"{base}.md", f"{base}.en.md", f"{base}.es.md"]
            for name, label in zip(names, LANGUAGE_LABELS[:3]):
                line = read(name).splitlines()[2]
                self.assertIn(f"**{label}**", line, name)
                for other, other_label in zip(names, LANGUAGE_LABELS[:3]):
                    if other != name:
                        self.assertIn(f"[{other_label}]({other})", line, name)

    def test_security_points_to_the_private_report_and_states_the_deadlines(self):
        for name in self.SECURITY:
            text = read(name)
            self.assertIn(ADVISORIES, text, name)
            self.assertRegex(text, r"\b5\b", name)    # receipt confirmed within 5 working days
            self.assertRegex(text, r"\b10\b", name)   # first assessment within 10 working days

    def test_security_scope_names_what_the_library_does(self):
        for name in self.SECURITY:
            text = read(name)
            for term in ("permission_suggestions", "CLAUDE_CONFIG_DIR", "stderr"):
                self.assertIn(term, text, name)


READMES = ("README.md", "README.en.md", "README.es.md", "README.zh-cn.md", "README.zh-tw.md")


class Readmes(unittest.TestCase):
    NOTICE = {
        "README.md": "Projeto da comunidade, sem vínculo com a Anthropic nem endosso dela.",
        "README.en.md": "Community project, not affiliated with or endorsed by Anthropic.",
        "README.es.md": "Proyecto de la comunidad, sin afiliación ni respaldo de Anthropic.",
        "README.zh-cn.md": "社区项目，与 Anthropic 无隶属关系，也未获其认可。",
        "README.zh-tw.md": "社群專案，與 Anthropic 無隸屬關係，亦未獲其認可。",
    }

    def test_each_readme_has_the_language_line(self):
        # third line: the file's own language in bold, the other four as links to the right files
        for name, label in zip(READMES, LANGUAGE_LABELS):
            line = read(name).splitlines()[2]
            self.assertIn(f"**{label}**", line, name)
            for other, other_label in zip(READMES, LANGUAGE_LABELS):
                if other != name:
                    self.assertIn(f"[{other_label}]({other})", line, name)

    def test_each_readme_carries_the_notice(self):
        for name, sentence in self.NOTICE.items():
            self.assertIn(sentence, read(name), name)

    def test_all_readmes_have_the_same_sections(self):
        counts = {name: sum(1 for line in read(name).splitlines() if line.startswith("## ")) for name in READMES}
        self.assertEqual(len(set(counts.values())), 1, counts)

    def test_no_readme_claims_a_published_package(self):
        # nothing is published in phase 0: no install instruction and no NuGet badge
        for name in READMES:
            text = read(name)
            self.assertNotIn("dotnet add package", text, name)
            self.assertNotIn("img.shields.io/nuget", text, name)


class Layout(unittest.TestCase):
    def test_spec_folders_exist_with_a_readme(self):
        for rel in ("fixtures/README.md", "examples/README.md", "libs/delphi/README.md"):
            self.assertTrue((ROOT / rel).is_file(), rel)

    def test_delphi_readme_states_the_floor_and_what_stays_out(self):
        text = read("libs/delphi/README.md")
        self.assertIn("10.1 Berlin", text)
        self.assertIn("VCL", text)

    def test_glossary_covers_the_five_languages(self):
        header = next(line for line in read("docs/i18n/glossary.md").splitlines() if line.startswith("| "))
        for column in ("pt-BR", "en", "es", "zh-CN", "zh-TW"):
            self.assertIn(column, header)

    def test_roadmap_has_the_status_legend(self):
        self.assertIn("Status", read("docs/ROADMAP.md"))


class Workflows(unittest.TestCase):
    WORKFLOWS = ("ci.yml", "release.yml", "pages.yml", "fixtures.yml")
    ALLOWED_SECRETS = {"ANTHROPIC_API_KEY", "NUGET_USER", "GITHUB_TOKEN"}

    def test_the_four_workflows_parse_and_declare_permissions(self):
        for name in self.WORKFLOWS:
            doc = load_yaml(f".github/workflows/{name}")
            self.assertIn("permissions", doc, name)
            self.assertIn("jobs", doc, name)

    def test_workflows_only_name_known_secrets(self):
        for name in self.WORKFLOWS:
            used = set(re.findall(r"secrets\.([A-Za-z_][A-Za-z0-9_]*)", read(f".github/workflows/{name}")))
            self.assertLessEqual(used, self.ALLOWED_SECRETS, name)

    def test_the_api_key_is_named_only_by_the_fixtures_workflow(self):
        for name in self.WORKFLOWS:
            has = "secrets.ANTHROPIC_API_KEY" in read(f".github/workflows/{name}")
            self.assertEqual(has, name == "fixtures.yml", name)

    def test_no_api_key_in_the_repo(self):
        pattern = re.compile(r"sk-ant-[A-Za-z0-9_-]{20,}")
        for rel in tracked():
            data = (ROOT / rel).read_bytes()
            if b"\0" in data[:8000]:
                continue
            self.assertIsNone(pattern.search(data.decode("utf-8", "replace")), rel)

    def test_branch_filters_name_master(self):
        for name in ("ci.yml", "pages.yml"):
            self.assertIn("master", read(f".github/workflows/{name}"), name)

    def test_ci_runs_what_the_docs_tell_a_contributor_to_run(self):
        ci = read(".github/workflows/ci.yml")
        for command in ("dotnet build libs/dotnet -c Release", "dotnet test libs/dotnet -c Release --no-build",
                        "tools/check_package.py", "tools/test_repo.py", "npm run build", "npm run check"):
            self.assertIn(command, ci)


class ReviewFindings(unittest.TestCase):
    """What the review of phase 0 found; each test failed before its fix."""

    def test_a_job_that_configures_pages_may_read_pages(self):
        # actions/configure-pages asks the API for the Pages site: with contents: read alone the step fails
        doc = load_yaml(".github/workflows/pages.yml")
        for name, job in doc["jobs"].items():
            uses = [step.get("uses", "") for step in job.get("steps", [])]
            if any(u.startswith("actions/configure-pages@") for u in uses):
                granted = {**doc.get("permissions", {}), **job.get("permissions", {})}
                self.assertIn("pages", granted, name)

    def test_releasing_lists_the_private_reporting_setting(self):
        # SECURITY and the issue templates send people to /security/advisories/new, which needs this setting
        self.assertIn("Private vulnerability reporting", read("RELEASING.md"))

    def test_the_chinese_readmes_say_they_are_ai_translations(self):
        self.assertIn("本译文由 AI 生成", read("README.zh-cn.md"))
        self.assertIn("本譯文由 AI 產生", read("README.zh-tw.md"))

    def test_no_text_claims_what_phase_0_does_not_have(self):
        self.assertNotIn("the recordings every library is tested against", read("libs/dotnet/README.md"))
        self.assertNotIn("e um teste confere cada arquivo", read("fixtures/README.md"))
        self.assertNotIn("checks that the five READMEs have the same sections and that every relative link",
                         read("docs/i18n/glossary.md"))
        self.assertNotIn("Published on GitHub Pages", read("site/astro.config.mjs"))

    def test_no_text_overstates_the_permission_rule(self):
        # the rule binds whoever writes to the claude's stdin; the types alone do not stop that side from doing otherwise
        self.assertIn("trusts the request it is given", read("libs/dotnet/README.md"))
        for name, phrase in (("libs/dotnet/README.md", "has no way to change what the tool will run"),
                             ("README.en.md", "has no way to change what the tool will run"),
                             ("README.md", "não tem como trocar o que a ferramenta vai executar"),
                             ("README.es.md", "no tiene forma de cambiar lo que la herramienta va a ejecutar"),
                             ("README.zh-cn.md", "无法更改工具将要执行的内容"),
                             ("README.zh-tw.md", "無法變更工具將要執行的內容")):
            self.assertNotIn(phrase, " ".join(read(name).split()), name)

    def test_contributing_does_not_say_the_privacy_test_is_still_to_come(self):
        for name in ("CONTRIBUTING.md", "CONTRIBUTING.en.md", "CONTRIBUTING.es.md"):
            self.assertIn("FixturePrivacyTests", read(name), name)


if __name__ == "__main__":
    unittest.main()
