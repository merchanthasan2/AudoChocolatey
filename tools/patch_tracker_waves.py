from pathlib import Path
import re

p = Path(__file__).resolve().parents[1] / "tracker.html"
text = p.read_text(encoding="utf-8")
done_parts = '{ plan: "done", build: "done", verify: "done", sec: "done", valid: "todo", pc: "todo", acc: "todo" }'
ids = [
    "32.1", "32.2", "32.3", "32.4", "32.5", "32.6", "32.7", "32.8", "32.9", "32.10",
    "32.11", "32.12", "32.13", "32.14", "32.15",
    "33.4", "33.5", "33.6", "33.8", "33.10", "33.11", "33.12", "33.13", "33.14", "33.16", "33.17",
    "34.1", "34.4", "34.5", "34.9", "34.12", "34.13",
    "31.3", "31.4", "31.7", "31.9", "31.11", "42.5",
]
for iid in ids:
    pat = rf'(\{{ id: "{re.escape(iid)}", text: "[^"]+", status: )"waiting"(, owner: "[^"]+")(\s*\}})'
    repl = rf'\1"done"\2, parts: {done_parts}\3'
    text, n = re.subn(pat, repl, text)
    if not n:
        # upgrade existing done rows that still say waiting parts? skip
        pass

text = text.replace(
    "Next waiting Wave 2 rows are <code>31.3</code> and <code>31.4</code>.",
    "Wave 2 is built. Waves 3–5 core tools are on Settings. Advanced leftover rows stay waiting.",
)
text = re.sub(
    r"<h2>Wave 2 — catalog export and app repair shipped</h2>",
    "<h2>Waves 2–5 shipping — Settings tools live</h2>",
    text,
    count=1,
)
text = text.replace(
    "Team: <code>31.3</code> scheduled cleanup or <code>31.4</code> duplicate finder.",
    "Team continues remaining waiting rows (context menu, BSOD, Wi-Fi survey, optional features, …).",
)
p.write_text(text, encoding="utf-8")
for phase in ("31.", "32.", "33.", "34."):
    waiting = len(re.findall(rf'id: "{phase}\d+", text: "[^"]+", status: "waiting"', text))
    done = len(re.findall(rf'id: "{phase}\d+", text: "[^"]+", status: "done"', text))
    print(phase, "done", done, "waiting", waiting)
