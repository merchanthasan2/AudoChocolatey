from pathlib import Path

p = Path(r"d:\Vibe Code\Auto Chocolatey\tracker.html")
t = p.read_text(encoding="utf-8")

old = '''    "Large files: Recycle Bin by default, Delete permanently behind an extra confirmation"
  ],
  later: "Later, not started: licensing and payment, code signing, public catalog listing, drivers.",'''

new = '''    "Large files: Recycle Bin by default, Delete permanently behind an extra confirmation",
    "Windows Utility matrix: 80 in this product (12 already + Waves 1 to 5). Drivers/backup/vuln after licensing. 12 open Windows. 48 later companion. 6 skipped."
  ],
  later: "Held out of this product (not in the % bar): after licensing 6 (drivers, backup, vulnerability feed); open Windows 12; later companion 48 (PowerToys/DevToys-class); skipped 6 (partition recovery, disk resize, HDD defrag, dishonest wipes, Windows 11 readiness). Full names live in windows-utility-plan.html.",
  heldOut: [
    { title: "After licensing (6)", items: ["Device driver updates", "Driver package export", "Roll back a device driver", "Versioned file backup and restore", "Full system image backup", "Installed software vulnerability inventory"] },
    { title: "Open Windows (12)", items: ["Microsoft Store app updates", "Previous Windows installation cleanup", "Rebuild the Windows Search index", "Repair an incorrect default app association", "Apply a system restore point", "Reinstall current Windows version for repair", "Repair a PC that will not boot", "RAM error testing", "Recover deleted files", "Create bootable Windows installation media", "Create a Windows recovery USB", "Offline malware scan"] },
    { title: "Not this product (6)", items: ["HDD defragmentation", "Recover a lost partition", "Resize a disk partition", "Selected file overwrite and delete", "Overwrite free space on a selected volume", "Windows 11 readiness check"] }
  ],'''

if old not in t:
    raise SystemExit("anchor1 missing")
t = t.replace(old, new, 1)

old2 = '''        { id: "29.2", text: "Owner on-screen checks: scan list, Marketplace Installed, uninstall, About this computer, readable buttons, real icons, emblem", status: "waiting", owner: "owner" }
      ]
    }
  ]
};'''

new2 = Path(__file__).with_name("tracker_wu_phases.js").read_text(encoding="utf-8")
if old2 not in t:
    raise SystemExit("anchor2 missing")
t = t.replace(old2, new2, 1)
t = t.replace('updated: "8 Nov 2026, 18:45"', 'updated: "8 Nov 2026, 19:05"', 1)

# render held-out
old_boot = '''  document.getElementById("later").innerHTML = "<strong>Later</strong>" + TRACKER.later;'''
new_boot = '''  document.getElementById("later").innerHTML = "<strong>Held out</strong> " + TRACKER.later + renderHeldOut();'''
if old_boot not in t:
    raise SystemExit("boot anchor missing")
t = t.replace(old_boot, new_boot, 1)

fn = '''
function renderHeldOut() {
  if (!TRACKER.heldOut) return "";
  return TRACKER.heldOut.map(function (g) {
    return "<div class=\\"held-group\\"><b>" + g.title + "</b><ul>" +
      g.items.map(function (n) { return "<li>" + n + "</li>"; }).join("") +
      "</ul></div>";
  }).join("");
}

'''
t = t.replace("function boot() {", fn + "function boot() {", 1)

css = '''
  .held-group { margin-top: 12px; }
  .held-group b { display: block; color: var(--secondary); margin-bottom: 4px; }
  .held-group ul { margin: 0; padding-left: 18px; color: var(--muted); font-size: 13px; }
'''
t = t.replace(
    "  .note { font-size: 13px; color: var(--muted); margin-top: 8px; }",
    "  .note { font-size: 13px; color: var(--muted); margin-top: 8px; }" + css,
    1,
)

p.write_text(t, encoding="utf-8")
print("ok waves", t.count("Wave 1"), "heldOut", "heldOut:" in t)
