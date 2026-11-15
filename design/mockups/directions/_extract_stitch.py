import json
from pathlib import Path

paths = [
    Path(r"C:\Users\merch\.cursor\projects\d-Vibe-Code-Auto-Chocolatey\agent-tools\de04eddb-4946-4a0c-82f8-14f9a534d3ee.txt"),
]
out = Path(__file__).with_name("stitch-refs.json")
refs = []
for p in paths:
    if not p.exists():
        continue
    d = json.loads(p.read_text(encoding="utf-8"))
    for c in d.get("outputComponents", []):
        design = c.get("design") or {}
        for sc in design.get("screens", []):
            refs.append({
                "title": sc.get("title"),
                "id": sc.get("id"),
                "screenshot": (sc.get("screenshot") or {}).get("name"),
                "html": (sc.get("htmlCode") or {}).get("name"),
            })
        # also designSystem only responses
        if "designSystem" in c:
            refs.append({"designSystem": c["designSystem"].get("name")})
print(json.dumps(refs, indent=2))
out.write_text(json.dumps({
    "A_note": "first generate may be designSystem only",
    "B": "projects/4437627295113959804/files/c97e47c8594a4cff8bf8cdee27d87f1b",
    "C": "projects/4437627295113959804/files/56644fe37dc346a19d6256c2e0c7b269",
    "D": "projects/4437627295113959804/files/5e1580971b684e68803f60f58a659b08",
}, indent=2), encoding="utf-8")
