"""Checks a packed ClaudeCliProtocol .nupkg: its two builds, its README and what it depends on.

Usage: python -I tools/check_package.py artifacts/ClaudeCliProtocol.<version>.nupkg

Prints "package ok" and exits 0, or prints what is wrong, one line each, and exits 1.
"""
import sys
import xml.etree.ElementTree as ET
import zipfile

FILES = ("lib/netstandard2.0/ClaudeCliProtocol.dll", "lib/net6.0/ClaudeCliProtocol.dll", "README.md")
NOTICE = "not affiliated with or endorsed by Anthropic"

# Target framework of the group -> exactly the packages it may depend on.
DEPENDENCIES = {
    ".NETStandard2.0": {"System.Text.Json": "8.0.5", "Microsoft.Bcl.AsyncInterfaces": "8.0.0"},
    "net6.0": {"System.Text.Json": "8.0.5"},
}


def _local(tag):
    return tag.rsplit("}", 1)[-1]


def _child(element, name):
    for child in element:
        if _local(child.tag) == name:
            return child
    return None


def problems(path):
    """What is wrong with the package at `path`; empty when it is as the spec says."""
    try:
        package = zipfile.ZipFile(path)
    except (OSError, zipfile.BadZipFile) as error:
        return [f"cannot open {path}: {error}"]
    found = []
    with package:
        names = set(package.namelist())
        for wanted in FILES:
            if wanted not in names:
                found.append(f"missing file: {wanted}")
        nuspecs = [n for n in names if n.endswith(".nuspec") and "/" not in n]
        if len(nuspecs) != 1:
            return found + [f"expected one .nuspec at the root, found {len(nuspecs)}"]
        metadata = _child(ET.fromstring(package.read(nuspecs[0])), "metadata")
    if metadata is None:
        return found + ["the .nuspec has no metadata"]

    license_el = _child(metadata, "license")
    if license_el is None or license_el.get("type") != "expression" or (license_el.text or "").strip() != "MIT":
        found.append("the license is not the MIT expression")
    description = _child(metadata, "description")
    if description is None or NOTICE not in (description.text or ""):
        found.append(f"the description does not say: {NOTICE}")

    groups = {}
    dependencies = _child(metadata, "dependencies")
    for group in (dependencies if dependencies is not None else []):
        if _local(group.tag) == "group":
            groups[group.get("targetFramework")] = {d.get("id"): d.get("version") for d in group}
    if set(groups) != set(DEPENDENCIES):
        found.append(f"dependency groups are {sorted(groups)}, expected {sorted(DEPENDENCIES)}")
    for framework, wanted in DEPENDENCIES.items():
        if framework in groups and groups[framework] != wanted:
            found.append(f"{framework} depends on {groups[framework]}, expected {wanted}")
    return found


def main(argv):
    if len(argv) != 2:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    found = problems(argv[1])
    for line in found:
        print(line)
    if found:
        return 1
    print("package ok")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
