"""Sweeps the issues and the CHANGELOG of anthropics/claude-code for what touches `claude -p` and stream-json.

Usage:
    python docs/sweep_issues.py                   # every issue, whole history
    python docs/sweep_issues.py 2026-11-01        # incremental: issues created after the date
    python docs/sweep_issues.py --changelog 2.1.310   # CHANGELOG lines of the versions after 2.1.310

Needs the `gh` CLI, authenticated (`gh auth status`): an authenticated search allows 30 requests a minute.
Each query takes the top results by reactions, open and closed. The issue sweep writes `sweep_results.json`
(ignored by git) and prints a table, without what docs/ROADMAP.md already tracks (KNOWN).

Limits of the method, learned in claude-todos-vscode, where this script comes from:
  - each query brings only the top results by reactions; with a window longer than about two weeks the tail is
    lost, so run it in two windows and join them;
  - KNOWN does not tell a change of STATE: an issue already tracked can be closed without showing up here;
  - neither mode replaces reading the whole CHANGELOG since the last version seen: what matters most is often
    in a generic line that names none of the terms below.
"""
import io
import json
import re
import subprocess
import sys
import time

REPO = "anthropics/claude-code"

# Issues docs/ROADMAP.md already tracks (validated, delivered or discarded).
KNOWN = {
    # sweep 2026-11-24
    111427, 109249, 110808, 109534, 110401,             # items 2, 3, 5, 8, 9 (phase 2)
    111343,                                             # item 13 (covered)
    111046, 106811, 108132, 110490, 110918,             # items 15-19 (to follow)
    110943, 107349, 109393, 110211, 110540,             # item 20
    111159, 111811, 107839,                             # item 20
    107106, 107134, 108851,                             # item 21 (discarded: billing)
    109679, 106877, 106868, 108577, 110977,             # item 21 (discarded: not the -p protocol)
}

# (label, query); every query gets `repo:` and `is:issue`.
QUERIES = [
    ("stream-json", "stream-json"),
    ("output-format", '"--output-format"'),
    ("input-format", '"--input-format"'),
    ("print mode", '"print mode"'),
    ("headless (title)", "headless in:title"),
    ("control_request", "control_request OR control_response"),
    ("can_use_tool", "can_use_tool"),
    ("permission-prompt-tool", '"permission-prompt-tool"'),
    ("include-partial-messages", '"include-partial-messages"'),
    ("stream_event", "stream_event"),
    ("Agent SDK stream", '"Agent SDK" stream'),
    ("label agent-sdk", "label:area:agent-sdk"),
]

# The billing cluster: high volume, nothing to do with the protocol.
NOISE = re.compile(r"usage credits|credits required|weekly (quota|allowance)|max plan|"
                   r"billed|billing|quota drain|usage limit|5-hour", re.I)

# What the libraries read or write: the flags of `claude -p` and the names of the stream.
PROTOCOL = re.compile(
    r"stream-json|stream_json|ndjson|"
    r"--(output|input)-format|--print\b|claude -p\b|print mode|headless|"
    r"--permission-prompt-tool|--include-partial-messages|--permission-mode|--resume\b|"
    r"control_(request|response|cancel_request)|can_use_tool|permission_suggestions|"
    r"stream_event|content_block_(start|delta|stop)|message_start|rate_limit_event|"
    r"set_permission_mode|set_model|apply_flag_settings|"
    r"\bstdin\b|\bstdout\b|agent sdk|session file|\.jsonl\b",
    re.I)

PER_PAGE = 40
SLEEP = 2.2  # authenticated: 30 requests a minute


def is_noise(title):
    """True for an issue of the billing cluster."""
    return bool(NOISE.search(title))


def matches_protocol(text):
    """True when the text names something of `claude -p` or of the stream."""
    return bool(PROTOCOL.search(text))


def rank(issues, known):
    """The issues worth reading, most reactions first: not tracked yet, not noise, and touching the protocol."""
    kept = [it for it in issues
            if it["number"] not in known
            and not is_noise(it["title"])
            and matches_protocol(it["title"] + " " + it.get("body", ""))]
    kept.sort(key=lambda it: (-it.get("reactions", 0), -it.get("comments", 0), it["number"]))
    return kept


def _version(text):
    return tuple(int(part) for part in re.findall(r"\d+", text))


def changelog_hits(markdown, since_version):
    """(version, line) for each CHANGELOG entry of a version after `since_version` that touches the protocol."""
    floor = _version(since_version)
    hits, current = [], None
    for line in markdown.splitlines():
        heading = re.match(r"##\s+\[?(\d+(?:\.\d+)+)\]?", line)
        if heading:
            current = heading.group(1)
            continue
        entry = re.match(r"\s*[-*]\s+(.*\S)", line)
        if entry and current and _version(current) > floor and matches_protocol(entry.group(1)):
            hits.append((current, entry.group(1)))
    return hits


def _gh(args):
    out = subprocess.run(["gh", *args], capture_output=True, text=True, encoding="utf-8")
    if out.returncode != 0:
        raise RuntimeError(out.stderr.strip()[:300])
    return out.stdout


def fetch(query, since):
    date_filter = f" created:>{since}" if since else ""
    q = f"repo:{REPO} is:issue{date_filter} {query}"
    return json.loads(_gh(["api", "-X", "GET", "search/issues", "-f", f"q={q}", "-f", "sort=reactions",
                           "-f", "order=desc", "-f", f"per_page={PER_PAGE}"]))


def sweep_issues(since):
    print(f"Sweep of {REPO}" + (f" (created:>{since})" if since else " (whole history)"))
    seen, sources, totals = {}, {}, {}
    for i, (label, query) in enumerate(QUERIES):
        for attempt in range(3):
            try:
                data = fetch(query, since)
                break
            except Exception as error:
                print(f"  ! {label}: {error} (attempt {attempt + 1})", file=sys.stderr)
                time.sleep(20)
        else:
            totals[label] = -1
            continue
        totals[label] = data.get("total_count", 0)
        for it in data.get("items", []):
            number = it["number"]
            sources.setdefault(number, []).append(label)
            if number not in seen:
                seen[number] = {
                    "number": number,
                    "title": it["title"],
                    "state": it["state"],
                    "state_reason": it.get("state_reason"),
                    "labels": [lbl["name"] for lbl in it.get("labels", [])],
                    "reactions": it.get("reactions", {}).get("total_count", 0),
                    "comments": it.get("comments", 0),
                    "created": it["created_at"][:10],
                    "body": (it.get("body") or "")[:600],
                }
        print(f"[{i + 1}/{len(QUERIES)}] {label}: total={totals[label]}, unique={len(seen)}")
        if i < len(QUERIES) - 1:
            time.sleep(SLEEP)

    fresh = [v for k, v in seen.items() if k not in KNOWN]
    core = rank(fresh, KNOWN)
    with open("sweep_results.json", "w", encoding="utf-8") as f:
        json.dump({"totals_per_query": totals, "total_unique": len(seen),
                   "already_known": sorted(k for k in seen if k in KNOWN),
                   "candidates": fresh, "touching_the_protocol": core,
                   "sources": {str(k): v for k, v in sources.items() if k not in KNOWN}},
                  f, ensure_ascii=False, indent=2)

    print(f"\n== {len(fresh)} new candidates; {len(core)} touching the protocol ==")
    for it in core[:80]:
        reason = f"/{it['state_reason']}" if it["state_reason"] else ""
        labels = ",".join(it["labels"][:4])
        print(f"#{it['number']:>6} [{it['state']}{reason}] r={it['reactions']:<3} c={it['comments']:<3} "
              f"({it['created']}) {it['title'][:95]}  [{labels}]")


def sweep_changelog(since_version):
    markdown = _gh(["api", f"repos/{REPO}/contents/CHANGELOG.md", "-H", "Accept: application/vnd.github.raw"])
    hits = changelog_hits(markdown, since_version)
    print(f"CHANGELOG of {REPO} after {since_version}: {len(hits)} lines touching the protocol")
    for version, line in hits:
        print(f"{version:>10}  {line[:160]}")


def main(argv):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    if len(argv) >= 2 and argv[1] == "--changelog":
        if len(argv) != 3:
            print("usage: sweep_issues.py --changelog <version>", file=sys.stderr)
            return 2
        sweep_changelog(argv[2])
    else:
        sweep_issues(argv[1] if len(argv) > 1 else None)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
