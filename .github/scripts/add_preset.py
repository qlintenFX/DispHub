"""Adds the preset from an approved "Share a preset" issue to presets/gallery.json.

Run by .github/workflows/preset.yml. The issue text is untrusted: every field is checked and only plain text
ends up in the JSON. Prints the preset name to GITHUB_OUTPUT for the pull request title.
"""
import json
import os
import re
import sys
from urllib.parse import parse_qsl, quote, urlencode

GALLERY = "presets/gallery.json"
LINK = re.compile(r"^disphub://preset\?[A-Za-z0-9%._~=&+\-]{1,400}$")
NAME = re.compile(r"^[\w .,'&()+\-]{1,40}$")


def fail(message: str) -> None:
    print(f"::error::{message}")
    sys.exit(1)


def fields(body: str) -> dict[str, str]:
    """Issue forms render as '### Label' followed by the answer."""
    result: dict[str, str] = {}
    label = None
    lines: list[str] = []
    for line in body.splitlines() + ["### "]:
        if line.startswith("### "):
            if label is not None:
                value = "\n".join(lines).strip()
                result[label] = "" if value == "_No response_" else value
            label, lines = line[4:].strip(), []
        else:
            lines.append(line)
    return result


def main() -> None:
    with open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8") as f:
        issue = json.load(f)["issue"]
    answers = fields(issue.get("body") or "")

    link = answers.get("Preset link", "").strip()
    name = " ".join(answers.get("Name", "").split())
    description = " ".join(answers.get("Description", "").split())
    tags = [t.strip() for t in answers.get("Tags", "").split(",") if t.strip()]
    author = issue["user"]["login"]

    if not LINK.match(link):
        fail("The preset link should start with disphub://preset? and come from DispHub's Share button.")
    if not NAME.match(name):
        fail("The name should be 1 to 40 letters, digits, spaces or simple punctuation.")
    if not 1 <= len(description) <= 200:
        fail("The description should be one sentence of at most 200 characters.")
    if len(tags) > 5 or any(len(t) > 24 for t in tags):
        fail("Use at most five tags of up to 24 characters.")

    # The link carries the name too; make it the one from the form.
    query = [(k, v) for k, v in parse_qsl(link.split("?", 1)[1], keep_blank_values=True) if k != "name"]
    link = "disphub://preset?" + urlencode([("name", name)] + query, quote_via=quote)

    with open(GALLERY, encoding="utf-8") as f:
        gallery = json.load(f)
    if any(p["name"].casefold() == name.casefold() for p in gallery["presets"]):
        fail(f"There is already a preset called {name}.")
    gallery["presets"].append({"name": name, "description": description, "tags": tags, "link": link, "author": author})
    with open(GALLERY, "w", encoding="utf-8", newline="\n") as f:
        json.dump(gallery, f, ensure_ascii=False, indent=2)
        f.write("\n")

    with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as f:
        f.write(f"name={name}\n")
    print(f"Added {name} by @{author}")


if __name__ == "__main__":
    main()
