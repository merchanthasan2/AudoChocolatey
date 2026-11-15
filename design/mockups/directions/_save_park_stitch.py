import json, base64, pathlib, re
src = pathlib.Path(r"C:\Users\merch\.cursor\projects\d-Vibe-Code-Auto-Chocolatey\agent-tools\6853981c-4ca4-4c53-aee0-37a73bc76e8c.txt")
text = src.read_text(encoding="utf-8")
data = json.loads(text)
shot_name = None
for c in data.get("outputComponents", []):
    design = c.get("design") or {}
    for sc in design.get("screens", []):
        shot = sc.get("screenshot") or {}
        print("title", sc.get("title"))
        print("shot", shot.get("name"))
        shot_name = shot.get("name")
print("SHOT_NAME", shot_name)
pathlib.Path(__file__).with_name("park-stitch-name.txt").write_text(shot_name or "", encoding="utf-8")
