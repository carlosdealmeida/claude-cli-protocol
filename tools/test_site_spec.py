"""The pages of the specification (site/src/content/docs/spec/) against the recordings.

Usage, from the root of the repository: python -I tools/test_site_spec.py -v

- A JSON block preceded by <!-- fixture: <file>:<line> --> is that line of fixtures/<file>, or a cut of it: every
  key of the example is in the line with the same value; a string that ends in "…" is a prefix; a list may stop
  early, and a last item "…" says that more follow.
- A JSON block preceded by <!-- stdin --> is a line for the claude's stdin: it must parse, and it must be a single
  line.
- The tables of the page of events have, for each event the expected results give, exactly the fields they give;
  an event that no expected result gives is listed under the section of the events no recording exercises.

Standard library only.
"""
import glob
import json
import os
import re
import unittest

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPEC = os.path.join(ROOT, "site", "src", "content", "docs", "spec")
FIXTURES = os.path.join(ROOT, "fixtures")
ELLIPSIS = "…"
UNRECORDED_SECTION = "## Os que nenhuma gravação exercita"
SESSION_EVENTS = {"control_replied"}

BLOCK = re.compile(r"<!--\s*(fixture:\s*(?P<file>[^:\s]+):(?P<line>\d+)|stdin)\s*-->\s*\n```json\n(?P<body>.*?)\n```",
                   re.DOTALL)
ROW = re.compile(r"^\|\s*`(?P<event>[a-z_]+)`\s*\|[^|]*\|\s*(?P<fields>[^|]*)\|\s*$")


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def fixture_line(name, number):
    lines = [line for line in read(os.path.join(FIXTURES, name)).split("\n") if line.strip()]
    if name.endswith(".expected.json"):
        # the file is one JSON document; its line N is one event, with the comma that separates it
        lines = read(os.path.join(FIXTURES, name)).split("\n")
        return json.loads(lines[number - 1].strip().rstrip(","))
    return json.loads(lines[number - 1])


def difference(example, actual, path="$"):
    """None when the example is a cut of the actual value; else where it differs."""
    if isinstance(example, str) and example.endswith(ELLIPSIS) and isinstance(actual, str):
        return None if actual.startswith(example[:-1]) else path
    if isinstance(example, dict):
        if not isinstance(actual, dict):
            return path
        for key, value in example.items():
            if key not in actual:
                return path + "." + key + " (missing)"
            found = difference(value, actual[key], path + "." + key)
            if found:
                return found
        return None
    if isinstance(example, list):
        if not isinstance(actual, list):
            return path
        items = example[:-1] if example and example[-1] == ELLIPSIS else example
        if len(items) > len(actual) or (items is example and len(items) != len(actual)):
            return path + " (length)"
        for i, value in enumerate(items):
            found = difference(value, actual[i], "%s[%d]" % (path, i))
            if found:
                return found
        return None
    if isinstance(example, (int, float)) and not isinstance(example, bool) and isinstance(actual, (int, float)):
        return None if abs(example - actual) <= 1e-9 else path
    return None if example == actual else path


def pages():
    return sorted(glob.glob(os.path.join(SPEC, "*.md")))


def expected_events():
    events = {}
    for name in glob.glob(os.path.join(FIXTURES, "*.expected.json")):
        for e in json.loads(read(name))["events"]:
            events.setdefault(e["event"], set()).update(e["fields"].keys())
    return events


class SpecPages(unittest.TestCase):
    def test_the_six_pages_exist(self):
        names = {os.path.basename(p) for p in pages()}
        for slug in ("protocolo", "comandos", "controle", "transporte", "eventos", "fixtures"):
            self.assertIn(slug + ".md", names)

    def test_every_marked_block_is_what_it_says(self):
        count = 0
        for page in pages():
            for match in BLOCK.finditer(read(page)):
                count += 1
                where = "%s: %s" % (os.path.basename(page), match.group(0).split("\n")[0])
                body = match.group("body")
                example = json.loads(body)
                if match.group("file") is None:
                    self.assertEqual(1, len(body.strip().split("\n")), where + ": a stdin line is one line")
                    continue
                actual = fixture_line(match.group("file"), int(match.group("line")))
                self.assertIsNone(difference(example, actual), where)
        self.assertGreater(count, 20)

    def test_every_json_block_parses(self):
        for page in pages():
            for body in re.findall(r"```json\n(.*?)\n```", read(page), re.DOTALL):
                json.loads(body)

    def test_the_tables_of_events_give_the_fields_of_the_expected_results(self):
        text = read(os.path.join(SPEC, "eventos.md"))
        before, _, rest = text.partition(UNRECORDED_SECTION)
        self.assertTrue(rest, "the page has no section of the unrecorded events")
        unrecorded_part, heading, after = rest.partition("\n## ")
        recorded_part = before + heading + after
        expected = expected_events()

        def rows(part):
            found = {}
            for line in part.split("\n"):
                m = ROW.match(line)
                if m:
                    fields = set(re.findall(r"`([a-z_]+)`", m.group("fields")))
                    found[m.group("event")] = fields
            return found

        recorded, unrecorded = rows(recorded_part), rows(unrecorded_part)
        for event, fields in expected.items():
            self.assertIn(event, recorded, event + " is not in the tables")
            self.assertEqual(fields, recorded[event], event)
        for event in recorded:
            if event in expected:
                continue
            # a row of the tables of content blocks is not an event
            if event in ("text", "thinking", "tool_use", "tool_result", "image"):
                continue
            self.assertIn(event, SESSION_EVENTS | set(unrecorded), event + " is listed as recorded")
        for event in unrecorded:
            self.assertNotIn(event, expected, event + " is recorded: move it up")
        self.assertEqual(36, len(expected) + len(unrecorded), "the readers produce 36 kinds of event")


if __name__ == "__main__":
    unittest.main()
