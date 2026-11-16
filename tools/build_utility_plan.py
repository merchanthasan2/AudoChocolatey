# One-shot generator for windows-utility-plan.html. Not part of the product.
import json
from pathlib import Path
import openpyxl

ROOT = Path(r"d:\Vibe Code\Auto Chocolatey")
XLSX = ROOT / "Windows_Utility_Feature_Matrix.xlsx"
OUT = ROOT / "windows-utility-plan.html"

# bucket, wave, overlap with current Quantum Updater work
# already = in product or in the current System tools track
# wave1..wave5 = this product, after current tools
# paid = after licensing / paid key (ADR)
# windows = open the Windows UI, do not rebuild
# later = companion product or a later year
# skip = too risky or off-brand for a $100 updater

OVERRIDE = {
    "Desktop app updates": ("already", "Current", "Updates page. Catalog upgrades are the core product."),
    "WinGet source refresh and reset": ("wave2", "WU2", "Catalog repair only, labelled Catalog sources. Reset is recovery, not everyday."),
    "Microsoft Store app updates": ("windows", "Open Windows", "Store updates stay in Microsoft Store. We can open it; we cannot replace it."),
    "Windows Update scan and install": ("wave3", "WU3", "Separate from app updates. Search, download, install through the Windows Update API, with policy respect."),
    "Restart-required status": ("wave1", "WU1", "Show which operation asked for a restart. Partial today."),
    "Windows Update component repair": ("wave3", "WU3", "On-demand, targeted, after Get Help. Never a blanket reset."),
    "Uninstall an eligible Windows update": ("wave3", "WU3", "Only eligible updates, with a warning that security updates can re-open holes."),
    "Installed app list export and reinstall": ("wave2", "WU2", "Export and import catalog IDs. Settings and user data stay out."),
    "Device driver updates": ("paid", "After licensing", "ADR: drivers after licensing, paid key only."),
    "Driver package export": ("paid", "After licensing", "Same ADR. Export signed packages the user chooses."),
    "Roll back a device driver": ("paid", "After licensing", "Same ADR. Device Manager rollback with a restart warning."),
    "Temporary file cleanup": ("already", "Current", "Cleanup page. Age floor and skip-in-use already planned."),
    "Empty Recycle Bin": ("wave1", "WU1", "Preview count and size, then empty the selected drive."),
    "Browser cache cleanup": ("already", "Current", "Cleanup. All Chromium profiles. Close the browser first."),
    "Browser history and cookies": ("wave1", "WU1", "Separate from cache. User picks profile. Warns about sign-out and sync."),
    "Explorer thumbnail cache cleanup": ("wave1", "WU1", "Per-user, regenerates itself."),
    "DirectX shader cache cleanup": ("wave1", "WU1", "Warn that games rebuild shaders on next launch."),
    "Crash dump cleanup": ("already", "Current", "Crash logs category. Age floor 7 days for Windows dumps."),
    "Windows Error Reporting cleanup": ("wave1", "WU1", "Queues and archives only. Leave Event Logs."),
    "Delivery Optimization cache cleanup": ("wave1", "WU1", "Windows cmdlet. Skip if servicing is active."),
    "Windows Update download cache cleanup": ("already", "Current", "ADR 011: pause update services, clear downloads, restart services."),
    "Component store cleanup (WinSxS)": ("already", "Current", "ADR 011: Windows DISM StartComponentCleanup. Our code never deletes WinSxS."),
    "Previous Windows installation cleanup": ("windows", "Open Windows", "Deleting Windows.old removes the go-back option. Open Storage recommendations."),
    "Scheduled cleanup": ("wave2", "WU2", "Task Scheduler, user-chosen categories and cadence, activity log."),
    "Storage treemap and large-file finder": ("already", "Current", "Large files tool. Treemap is a later visual on the same scan."),
    "Duplicate file finder": ("wave2", "WU2", "Report first. Recycle Bin default, permanent behind extra confirm (ADR 013)."),
    "Repair an installed Win32 app": ("wave2", "WU2", "Catalog repair where supported, else the app's own repair."),
    "Repair or reset a packaged app": ("wave2", "WU2", "Repair first. Reset warns it wipes that app's data."),
    "Reset Microsoft Store cache": ("wave2", "WU2", "wsreset in this user session only."),
    "Re-register an installed Store app": ("wave2", "WU2", "Only packages already present, for this account."),
    "Uninstall apps and review leftovers": ("already", "Current", "Uninstall plus leftover folders report. Removal of leftovers waits on D4."),
    "Startup app manager": ("already", "Current", "Being rebuilt. Disable via Windows flag, never delete."),
    "Explorer context-menu manager": ("wave4", "WU4", "Third-party classic handlers only. Reversible disable. System entries stay."),
    "Rebuild Explorer icon cache": ("wave2", "WU2", "On-demand for blank icons. Restarts Explorer after save warning."),
    "Clear stuck print jobs and restart spooler": ("wave2", "WU2", "Cancel selected jobs first. Machine-wide clear is a second confirm."),
    "Remove a broken or orphaned installation": ("wave2", "WU2", "Vendor uninstaller first, then leftover scan. No forced registry wipe."),
    "Rebuild the Windows Search index": ("windows", "Open Windows", "Open Advanced indexing options. Rebuild takes hours."),
    "Repair an incorrect default app association": ("windows", "Open Windows", "Windows must record the user's choice in Default apps."),
    "Protected system file scan and repair": ("wave3", "WU3", "SFC verify, then repair. Elevated. Show CBS log path."),
    "Windows component-store integrity repair": ("wave3", "WU3", "DISM ScanHealth then RestoreHealth. Follow with SFC."),
    "WMI repository verify and salvage": ("wave3", "WU3", "Verify first. Salvage only after that fails. No full reset in v1."),
    "Create a system restore point": ("wave3", "WU3", "Check System Protection, create, report result."),
    "Apply a system restore point": ("windows", "Open Windows", "Open System Restore. Show that it covers system state, not files."),
    "Reinstall current Windows version for repair": ("windows", "Open Windows", "Fix problems using Windows Update, when offered."),
    "Repair a PC that will not boot": ("windows", "Open Windows", "Guide into WinRE Startup Repair. BitLocker key reminder."),
    "Disk and SSD health": ("wave3", "WU3", "Read-only SMART/reliability counters on About and a Health card."),
    "Online file-system scan": ("wave3", "WU3", "chkdsk /scan, report only."),
    "Repair file-system errors": ("wave3", "WU3", "Spot-fix or schedule boot-time repair after confirm."),
    "HDD defragmentation": ("skip", "Not this product", "Windows already schedules this. Easy to harm an SSD if media detection is wrong."),
    "SSD ReTrim": ("wave3", "WU3", "Only when the volume reports TRIM. Leave Windows' schedule in place."),
    "View and export Windows events": ("wave3", "WU3", "Filter by channel and time. Export the minimum for a support case."),
    "Blue-screen dump triage": ("wave4", "WU4", "Stop code and suspected modules as leads, not a debugger."),
    "RAM error testing": ("windows", "Open Windows", "Schedule Windows Memory Diagnostic. Not a live trim."),
    "Battery health report": ("already", "Current", "About this computer battery card. Full report is the same facts plus design vs full charge."),
    "Hardware and OS inventory report": ("already", "Current", "About this computer v2. Copy all system info. Redact serial on share."),
    "CPU and GPU temperature monitoring": ("later", "Later companion", "Needs a sensor driver. Do not ship a kernel driver in this app."),
    "Disk speed benchmark": ("later", "Later companion", "Optional after Health. Scratch file only, never user files."),
    "Boot performance analysis": ("later", "Later companion", "Read-only boot traces. Heavy for v1."),
    "ETW performance trace capture": ("later", "Later companion", "Traces can hold paths and process names. Keep out of v1."),
    "Sleep and wake troubleshooting": ("wave4", "WU4", "powercfg last-wake and requests. Laptop-relevant on this TUF."),
    "Select a power plan": ("wave4", "WU4", "Show active plan, switch, revert."),
    "App hang and crash dump capture": ("later", "Later companion", "Dumps hold private memory. Keep local and opt-in later."),
    "Bulk file rename": ("later", "Later companion", "PowerToys territory. Not a maintenance tool."),
    "File and folder comparison": ("later", "Later companion", "WinMerge-class. Out of this app."),
    "Resumable file copy and verify": ("later", "Later companion", "Useful, but a copy manager is a different product."),
    "Folder synchronization": ("later", "Later companion", "Mirror mode can delete. Too easy to get wrong in v1."),
    "Create and extract archives": ("wave5", "WU5", "ZIP via Windows APIs. Other formats later."),
    "Encrypted archives": ("later", "Later companion", "Password handling and format support is a product of its own."),
    "Transparent folder compression": ("wave5", "WU5", "NTFS compact on a chosen folder, with measured savings."),
    "Mount and eject ISO or VHD images": ("wave5", "WU5", "Windows Mount-DiskImage, read-only default."),
    "Merge, split, and rotate PDFs": ("later", "Later companion", "PDF editor is not this product."),
    "File checksum calculation and verification": ("wave5", "WU5", "SHA-256 of a chosen file vs a pasted digest."),
    "Find empty folders": ("wave5", "WU5", "Report, then Recycle Bin. Skip system folders."),
    "Find and repair broken shortcuts": ("wave5", "WU5", "Startup and desktop shortcuts. Retarget or remove after review."),
    "Fast local file search": ("later", "Later companion", "Everything-class indexer. Windows Search is enough for v1."),
    "Find which app is locking a file": ("wave5", "WU5", "Restart Manager. Show PID. Ending the app is a separate confirm."),
    "File and folder templates": ("later", "Later companion", "PowerToys New+."),
    "Strip document and image metadata": ("later", "Later companion", "Privacy tool. New copies only, later."),
    "Versioned file backup and restore": ("paid", "After licensing", "File History wrapper or a paid backup engine. Not v1."),
    "Full system image backup": ("paid", "After licensing", "Imaging is a different product and a different support load."),
    "Recover deleted files": ("windows", "Open Windows", "Launch Windows File Recovery. Never write to the source drive."),
    "Recover a lost partition": ("skip", "Not this product", "Partition writes can lose the disk. Out of scope."),
    "Resize a disk partition": ("skip", "Not this product", "One bad size and the PC will not boot."),
    "Format a removable volume": ("wave5", "WU5", "Removable only, identity check, erase warning. Never C:."),
    "Create bootable Windows installation media": ("windows", "Open Windows", "Open Media Creation Tool. Writing a USB erases it."),
    "Create a Windows recovery USB": ("windows", "Open Windows", "Open Recovery Drive. Not a file backup."),
    "Flush DNS resolver cache": ("wave4", "WU4", "Safe, instant network repair."),
    "Reset Winsock catalog": ("wave4", "WU4", "Warn about VPN and a possible restart."),
    "Reset TCP/IP settings": ("wave4", "WU4", "Backup adapter settings first. Expect disconnect."),
    "Renew DHCP lease": ("wave4", "WU4", "Selected DHCP adapter only. Static IPs left alone."),
    "Restore firewall defaults": ("wave4", "WU4", "Export first. Managed policy stays. Custom local rules go."),
    "Restore the default Hosts file": ("wave4", "WU4", "Show what would be replaced, including local and work mappings."),
    "Connection and port inventory": ("wave4", "WU4", "Read-only. Listening vs outbound."),
    "Latency, packet loss, and route trace": ("wave4", "WU4", "Bounded test window."),
    "Test a specific TCP port": ("wave4", "WU4", "Host and port the user types. DNS and TCP reported separately."),
    "DNS record lookup": ("wave4", "WU4", "Chosen record type. Show which resolver answered."),
    "Wi-Fi signal and channel survey": ("wave4", "WU4", "Needs location permission. Fallback to adapter info."),
    "LAN device discovery": ("later", "Later companion", "Scanning other people's devices needs a clear authorised range. Not default."),
    "Internet speed test": ("later", "Later companion", "Metered links and third-party endpoints. Keep out of v1."),
    "Network bandwidth by app": ("later", "Later companion", "ETW, heavier. Later."),
    "Wake a computer on the LAN": ("later", "Later companion", "WoL is niche and firmware-dependent."),
    "Windows clock sync diagnosis": ("wave4", "WU4", "Show source and last sync. Domain time stays."),
    "Inspect and reset WinHTTP proxy": ("wave4", "WU4", "Show old proxy. Corporate proxies can be required."),
    "On-demand malware scan": ("wave3", "WU3", "Start Windows Security / Defender scan. Never replace the installed AV."),
    "Offline malware scan": ("windows", "Open Windows", "Defender Offline via Windows Security. Not on ARM."),
    "Verify publisher signature": ("wave5", "WU5", "Authenticode on a chosen file. Used by Startup and installers."),
    "BitLocker status audit": ("wave3", "WU3", "Read-only. Never print recovery keys."),
    "Windows privacy settings review": ("wave5", "WU5", "Read camera, mic, location access and open the Windows page."),
    "Firewall rule inspection and backup": ("wave4", "WU4", "Inspect and export. Reset is the separate Network repair item."),
    "Check file and folder permissions": ("wave5", "WU5", "Read-only ACL view. No global reset."),
    "Defender status and intelligence updates": ("wave3", "WU3", "Status and signature update when Defender is the active provider."),
    "Installed software vulnerability inventory": ("paid", "After licensing", "Needs a licensed advisory feed. Do not scrape random CVE sites."),
    "Selected file overwrite and delete": ("skip", "Not this product", "SSD wear-leveling means we cannot honestly promise a wipe."),
    "Overwrite free space on a selected volume": ("skip", "Not this product", "Same honesty problem, plus hours of disk wear."),
    "Live process and resource monitor": ("wave5", "WU5", "Read-only list of heavy apps. Close is a separate confirm (you asked for this on RAM)."),
    "Close or end an unresponsive app": ("wave5", "WU5", "Normal close first, forced end after confirm. Recheck PID."),
    "Set process priority and CPU affinity": ("later", "Later companion", "Easy to make a game or editor stutter. Out of v1."),
    "Service management": ("wave5", "WU5", "View, start, stop, restart with dependency preview. Security and update services refused."),
    "Scheduled task manager": ("already", "Current", "Sign-in tasks are in Startup. Full task list can reuse that engine later."),
    "Windows optional feature manager": ("wave5", "WU5", "DISM optional features with restart warning."),
    "Registry snapshot and diff": ("later", "Later companion", "Read-only diffs later. Never a registry cleaner."),
    "Preview .reg files": ("wave5", "WU5", "Parse and show. Import is a separate elevated step, off by default."),
    "Desktop automation and macros": ("later", "Later companion", "AutoHotkey-class. Different product."),
    "Windows 11 readiness check": ("skip", "Not this product", "This PC is already Windows 11. Low value in this app."),
    "Command palette and app launcher": ("later", "Later companion", "PowerToys."),
    "Pin a window above others": ("later", "Later companion", "PowerToys."),
    "Custom window snapping zones": ("later", "Later companion", "PowerToys FancyZones."),
    "Save and relaunch app workspaces": ("later", "Later companion", "PowerToys Workspaces."),
    "Quick file preview": ("later", "Later companion", "PowerToys Peek."),
    "Keyboard and shortcut remapping": ("later", "Later companion", "PowerToys."),
    "Clipboard history and pinned items": ("later", "Later companion", "Windows already has clipboard history."),
    "Paste as text, Markdown, or JSON": ("later", "Later companion", "PowerToys Advanced Paste."),
    "Extract text from the screen": ("later", "Later companion", "PowerToys / Snipping Tool."),
    "Screenshots and annotation": ("later", "Later companion", "Snipping Tool and ShareX exist."),
    "Screen recording": ("later", "Later companion", "Snipping Tool and OBS exist. OBS is on this PC."),
    "Screen pixel ruler": ("later", "Later companion", "PowerToys."),
    "Screen color picker": ("later", "Later companion", "PowerToys."),
    "Batch resize and convert images": ("later", "Later companion", "Image tool, not maintenance."),
    "Keep Windows awake temporarily": ("wave5", "WU5", "Small, useful on this laptop. Show expiry and a cancel."),
    "External monitor brightness": ("later", "Later companion", "DDC/CI is flaky on docks. Later."),
    "Per-app volume control": ("later", "Later companion", "EarTrumpet-class."),
    "Mouse pointer finder and highlight": ("later", "Later companion", "PowerToys."),
    "Crop a window to a floating region": ("later", "Later companion", "PowerToys."),
    "Schedule light and dark themes": ("later", "Later companion", "We already have a theme toggle. Scheduling is PowerToys."),
    "One mouse and keyboard across PCs": ("later", "Later companion", "Pairing and clipboard sharing is a security surface."),
    "Environment variable and PATH editor": ("later", "Later companion", "Developer pack, already scheduled after public release."),
    "JSON, YAML, and text formatting": ("later", "Later companion", "DevToys. Developer pack."),
    "Encoding and decoding helpers": ("later", "Later companion", "DevToys."),
    "Regular expression tester": ("later", "Later companion", "DevToys."),
    "Terminal and shell profiles": ("later", "Later companion", "Open Windows Terminal. Developer pack."),
    "Manage WSL installation and updates": ("later", "Later companion", "Developer pack. Unregister deletes distro data."),
    "Launch a configured Windows Sandbox": ("later", "Later companion", "Pro/Enterprise only. Later."),
}

BUCKET_META = {
    "already": ("Already in this app", "done", "In the current Quantum Updater, or in the System tools track already building."),
    "wave1": ("Wave 1 — Finish Cleanup", "waiting", "Complete the Cleanup page so it matches a trusted cleaner, without cookies unless you opt in."),
    "wave2": ("Wave 2 — App and shell repair", "waiting", "Repair, leftovers, printer, icon cache, catalog export."),
    "wave3": ("Wave 3 — Windows health", "waiting", "SFC, DISM, restore point, disk health, Defender, Windows Update as its own workflow."),
    "wave4": ("Wave 4 — Network and power", "waiting", "DNS, Winsock, TCP/IP, ports, Wi-Fi, sleep/wake, power plan."),
    "wave5": ("Wave 5 — Files and admin", "waiting", "Checksums, empty folders, file lock, services, awake, Recycle Bin, removable format."),
    "paid": ("After licensing", "waiting", "Drivers, backup, vulnerability feed. Paid key, later phase as already decided."),
    "windows": ("Open Windows", "waiting", "Guide into the Windows screen. Rebuilding it would fight Windows and policies."),
    "later": ("Later companion", "waiting", "PowerToys, DevToys, copy managers, sensors. A second product, not this $100 updater."),
    "skip": ("Not this product", "blocked", "Too easy to destroy a disk, lie about a wipe, or duplicate Windows badly."),
}


def load_rows():
    wb = openpyxl.load_workbook(XLSX, data_only=True)
    ws = wb.active
    rows = []
    for i, row in enumerate(ws.iter_rows(min_row=7, max_row=158, max_col=7, values_only=True), start=1):
        area, feature, impl, mode, github, competitor, notes = row
        if not feature:
            continue
        rows.append({
            "n": i,
            "area": (area or "").strip(),
            "feature": feature.strip(),
            "impl": (impl or "").strip(),
            "mode": (mode or "").strip(),
            "notes": (notes or "").strip(),
        })
    return rows


def classify(feature, area):
    if feature in OVERRIDE:
        return OVERRIDE[feature]
    # conservative fallback by area
    fallback = {
        "Updates and drivers": ("paid", "After licensing", "Default: treat leftover update/driver items as paid/later."),
        "Cleanup": ("wave1", "WU1", "Default: extra cleanup category with preview."),
        "App and shell repair": ("wave2", "WU2", "Default: repair with confirm."),
        "System repair and recovery": ("wave3", "WU3", "Default: guided repair."),
        "Diagnostics and performance": ("wave3", "WU3", "Default: read-only health."),
        "Hardware and performance": ("later", "Later companion", "Default: sensors and traces later."),
        "Files and storage": ("later", "Later companion", "Default: file-manager tools later."),
        "Backup and disk management": ("paid", "After licensing", "Default: backup later."),
        "Network repair": ("wave4", "WU4", "Default: network repair with confirm."),
        "Network tools": ("wave4", "WU4", "Default: read-only network tools."),
        "Security and privacy": ("wave3", "WU3", "Default: status and Windows Security."),
        "System administration": ("wave5", "WU5", "Default: admin with denylist."),
        "Desktop productivity": ("later", "Later companion", "Default: PowerToys-class, later companion."),
        "Developer utilities": ("later", "Later companion", "Default: developer pack later."),
    }
    return fallback.get(area, ("later", "Later companion", "Unclassified; held out of v1."))


def main():
    rows = load_rows()
    items = []
    for r in rows:
        bucket, wave, why = classify(r["feature"], r["area"])
        label, chip, _ = BUCKET_META[bucket]
        items.append({
            "id": f"F{r['n']:03d}",
            "area": r["area"],
            "feature": r["feature"],
            "mode": r["mode"],
            "bucket": bucket,
            "wave": wave,
            "why": why,
            "impl": r["impl"][:220],
        })

    html = TEMPLATE.replace("__ITEMS__", json.dumps(items, ensure_ascii=True))
    OUT.write_text(html, encoding="utf-8")
    counts = {}
    for it in items:
        counts[it["bucket"]] = counts.get(it["bucket"], 0) + 1
    in_product = sum(counts.get(k, 0) for k in ("already", "wave1", "wave2", "wave3", "wave4", "wave5"))
    print("wrote", OUT, "features", len(items), "in_product", in_product, counts)


TEMPLATE = r"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Quantum Updater — Windows Utility plan</title>
<style>
  :root {
    --bg-page: #04212A; --panel: #062B36; --card: #0A3442; --border: #164A5B;
    --text: #F2F8FA; --secondary: #BFD9E2; --muted: #8FB3C0;
    --mint: #06D7A0; --mint-label: #073A4B; --ocean: #108AB1; --link: #6CD0EE;
    --yellow: #FFD167; --yellow-label: #2B2000; --orange: #F78C6A; --orange-label: #2A1008;
    --pink: #F04770;
  }
  * { box-sizing: border-box; }
  body { margin: 0; background: var(--bg-page); color: var(--text); font: 15px/1.55 "Segoe UI", system-ui, sans-serif; }
  .header-band { background: var(--panel); border-bottom: 1px solid var(--border); padding: 28px 20px 24px; }
  .header-inner { max-width: 1180px; margin: 0 auto; display: flex; gap: 20px; align-items: flex-start; flex-wrap: wrap; }
  .emblem { width: 56px; height: 56px; border-radius: 14px; border: 1px solid var(--border); background: var(--bg-page); object-fit: contain; }
  h1 { margin: 0 0 6px; font-size: 26px; letter-spacing: -0.03em; }
  .sub { color: var(--secondary); max-width: 760px; margin: 0; font-size: 14px; }
  .stamp { margin-left: auto; font-size: 13px; color: var(--muted); align-self: center; }
  main { max-width: 1180px; margin: 0 auto; padding: 24px 20px 80px; }
  h2 { font-size: 17px; margin: 28px 0 12px; color: var(--secondary); font-weight: 600; }
  p { color: var(--secondary); }
  .overall-card, .card, .wave, .item {
    background: var(--card); border: 1px solid var(--border); border-radius: 12px;
  }
  .overall-card { padding: 22px 24px; margin-bottom: 16px; border-top: 3px solid var(--mint); }
  .overall-pct { font-size: 48px; font-weight: 800; font-variant-numeric: tabular-nums; line-height: 1; }
  .progress-bar { height: 12px; background: var(--bg-page); border-radius: 6px; overflow: hidden; border: 1px solid var(--border); margin-top: 12px; }
  .progress-fill { height: 100%; background: linear-gradient(90deg, var(--ocean), var(--mint)); }
  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(160px, 1fr)); gap: 10px; }
  .tile { background: var(--card); border: 1px solid var(--border); border-radius: 12px; padding: 12px 14px; border-top: 3px solid var(--border); }
  .tile .num { font-size: 26px; font-weight: 700; font-variant-numeric: tabular-nums; }
  .tile .lbl { font-size: 12px; color: var(--muted); }
  .tile.already { border-top-color: var(--mint); }
  .tile.wave1, .tile.wave2, .tile.wave3, .tile.wave4, .tile.wave5 { border-top-color: var(--ocean); }
  .tile.paid { border-top-color: var(--yellow); }
  .tile.windows { border-top-color: var(--link); }
  .tile.later { border-top-color: var(--muted); }
  .tile.skip { border-top-color: var(--pink); }
  .principles { display: grid; gap: 10px; }
  .principles .card { padding: 14px 16px; }
  .principles b { display: block; margin-bottom: 4px; }
  .filters { display: flex; flex-wrap: wrap; gap: 8px; margin: 8px 0 16px; }
  .filters button {
    font: inherit; color: var(--text); background: var(--panel); border: 1px solid var(--border);
    border-radius: 999px; padding: 6px 12px; cursor: pointer;
  }
  .filters button.on { background: var(--mint); color: var(--mint-label); border-color: var(--mint); }
  .wave { padding: 16px 18px 8px; margin-bottom: 14px; border-left: 4px solid var(--ocean); }
  .wave header { display: flex; justify-content: space-between; gap: 12px; align-items: baseline; flex-wrap: wrap; }
  .wave h3 { margin: 0 0 4px; font-size: 16px; }
  .wave .meta { color: var(--muted); font-size: 13px; margin: 0 0 12px; }
  .item { padding: 10px 12px; margin: 0 0 8px; display: grid; grid-template-columns: 64px 1fr auto; gap: 10px; align-items: start; }
  .item .id { font-variant-numeric: tabular-nums; color: var(--muted); font-size: 12px; padding-top: 3px; }
  .item .name { font-weight: 650; }
  .item .why { color: var(--secondary); font-size: 13px; margin: 2px 0 0; }
  .item .mode { color: var(--muted); font-size: 12px; margin-top: 4px; }
  .chip {
    font-size: 11px; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase;
    border-radius: 999px; padding: 4px 8px; white-space: nowrap;
  }
  .b-already { background: var(--mint); color: var(--mint-label); }
  .b-wave1, .b-wave2, .b-wave3, .b-wave4, .b-wave5 { background: #0A5F7A; color: var(--text); }
  .b-paid { background: var(--yellow); color: var(--yellow-label); }
  .b-windows { background: var(--link); color: var(--bg-page); }
  .b-later { background: transparent; color: var(--muted); border: 1px solid var(--border); }
  .b-skip { background: var(--pink); color: #2A1008; }
  .note { font-size: 13px; color: var(--muted); }
  .approve { padding: 18px; border-top: 3px solid var(--orange); }
</style>
</head>
<body>
  <header class="header-band">
    <div class="header-inner">
      <img class="emblem" src="brand/desk-verve-emblem.png" width="56" height="56" alt="">
      <div>
        <h1>Windows Utility plan</h1>
        <p class="sub">Draft for your approval. 152 features from the matrix, mapped onto Quantum Updater. Nothing from this list is added to the tracker or built until you approve a plan.</p>
      </div>
      <div class="stamp">Draft 8 Nov 2026, 18:55<br>Not in the live tracker yet</div>
    </div>
  </header>
<main>
  <section class="overall-card">
    <div style="display:flex;gap:16px;align-items:baseline;flex-wrap:wrap">
      <div class="overall-pct" id="pct">0%</div>
      <div>
        <div id="pct-label" style="color:var(--muted)"></div>
        <div class="note">In this product = already here, plus Waves 1 to 5. Licensing, Open Windows, later companion, and skip are held out of that percentage.</div>
      </div>
    </div>
    <div class="progress-bar"><div class="progress-fill" id="bar" style="width:0%"></div></div>
  </section>

  <div class="grid" id="tiles"></div>

  <h2>Recommended boundary</h2>
  <div class="principles">
    <div class="card"><b>This app stays an updater plus trusted system tools.</b> The matrix is a full Glary / CCleaner / PowerToys / DevToys / HWiNFO suite. Putting all 152 in this $100 product would stall the current System tools work and ship a kitchen sink.</div>
    <div class="card"><b>Finish what is already building, then Wave 1.</b> Cleanup safety, launch scans, About v2, Startup apps, Large files, leftover folders. Then complete Cleanup so Clean actually works with progress.</div>
    <div class="card"><b>Windows-owned work goes through Windows.</b> WinSxS, component cleanup, update cache, Defender, BitLocker keys, Store, WinRE. We orchestrate and confirm; we do not invent a second servicing stack.</div>
    <div class="card"><b>Drivers, backup, and a vulnerability feed wait for licensing.</b> That is already decided. They stay marked After licensing.</div>
    <div class="card"><b>PowerToys-class tools become a later companion, not this app.</b> Command palette, FancyZones, OCR, macros, DevToys, mouse-without-borders. Same company, different product, later.</div>
    <div class="card"><b>We will not ship a registry cleaner, a dishonest wipe, partition recovery, or HDD defrag.</b> Those either destroy disks or cannot keep the promise on an SSD.</div>
  </div>

  <h2>Waves if you approve this boundary</h2>
  <p>Each wave is one tracker phase after the current System tools track. Mockups still come before layout changes. One owner decision at a time when a wave needs one.</p>
  <div class="principles">
    <div class="card"><b>Now — current track (do not pause).</b> About v2, icons, cleanup safety, launch scans, Startup apps, Large files, leftover folders, real Optimize RAM.</div>
    <div class="card"><b>Wave 1 — Finish Cleanup.</b> Recycle Bin, thumbnails, shaders, WER, Delivery Optimization, history/cookies as a separate opt-in, restart-required banner.</div>
    <div class="card"><b>Wave 2 — App and shell repair.</b> Repair/reset apps, Store cache, printer queue, icon cache, catalog export/import, scheduled cleanup, duplicates.</div>
    <div class="card"><b>Wave 3 — Windows health.</b> SFC, DISM RestoreHealth, restore point, disk SMART, chkdsk scan, Defender scan/status, Windows Update as its own workflow, BitLocker status (no keys).</div>
    <div class="card"><b>Wave 4 — Network and power.</b> Flush DNS, Winsock, TCP/IP, DHCP, firewall export/reset, Hosts, ports, ping/trace, Wi-Fi survey, sleep/wake, power plan.</div>
    <div class="card"><b>Wave 5 — Files and admin.</b> Checksums, empty folders, broken shortcuts, file lock, ZIP, ISO mount, services, optional features, keep-awake, Recycle Bin empty, removable-only format.</div>
  </div>

  <h2>Feature by feature</h2>
  <p class="note">Filter does not change the plan. It only hides rows. Implementation notes keep the matrix's Windows APIs; on-screen labels will not name catalog engines.</p>
  <div class="filters" id="filters"></div>
  <div id="waves"></div>

  <h2>If you approve</h2>
  <div class="card approve">
    <b>Nothing is merged yet.</b>
    <p>When you approve a boundary, I will add those waves as phases in tracker.html so there is one pane for % and item status. Features marked Already stay as pointers to the current track, not duplicated work. Features marked Open Windows, Later companion, After licensing, or Not this product go in a held-out list, not in the live build queue.</p>
  </div>
</main>
<script>
const ITEMS = __ITEMS__;
const BUCKETS = [
  { id: "already", label: "Already in this app" },
  { id: "wave1", label: "Wave 1 Cleanup" },
  { id: "wave2", label: "Wave 2 Repair" },
  { id: "wave3", label: "Wave 3 Health" },
  { id: "wave4", label: "Wave 4 Network" },
  { id: "wave5", label: "Wave 5 Files" },
  { id: "paid", label: "After licensing" },
  { id: "windows", label: "Open Windows" },
  { id: "later", label: "Later companion" },
  { id: "skip", label: "Not this product" }
];
const WAVE_ORDER = [
  { key: "Current", bucket: "already", blurb: "Already shipping or in the System tools track. Do not rebuild." },
  { key: "WU1", bucket: "wave1", blurb: "Finish Cleanup so Clean is honest, with progress, and extra safe categories." },
  { key: "WU2", bucket: "wave2", blurb: "Repair apps and the shell. Catalog backup. Duplicates. Printer and icons." },
  { key: "WU3", bucket: "wave3", blurb: "SFC, DISM, restore point, disk health, Defender, Windows Update workflow." },
  { key: "WU4", bucket: "wave4", blurb: "Network repair and power. Read-only port and Wi-Fi views." },
  { key: "WU5", bucket: "wave5", blurb: "Everyday file and admin tools that fit a maintenance app." },
  { key: "After licensing", bucket: "paid", blurb: "Drivers, backup, vulnerability feed. Paid key, later." },
  { key: "Open Windows", bucket: "windows", blurb: "Open the Windows screen. Do not clone it." },
  { key: "Later companion", bucket: "later", blurb: "PowerToys, DevToys, copy, sensors. Different product later." },
  { key: "Not this product", bucket: "skip", blurb: "Will not ship. Disk-destroying or dishonest on SSD." }
];

let filter = "all";
const counts = {};
ITEMS.forEach((it) => { counts[it.bucket] = (counts[it.bucket] || 0) + 1; });
const inProduct = ITEMS.filter((it) => ["already","wave1","wave2","wave3","wave4","wave5"].includes(it.bucket)).length;
const pct = Math.round((inProduct / ITEMS.length) * 100);
document.getElementById("pct").textContent = pct + "%";
document.getElementById("pct-label").textContent = inProduct + " of " + ITEMS.length + " features recommended for this product (already + Waves 1 to 5)";
document.getElementById("bar").style.width = pct + "%";

const tiles = document.getElementById("tiles");
BUCKETS.forEach((b) => {
  const t = document.createElement("div");
  t.className = "tile " + b.id;
  t.innerHTML = "<div class='num'>" + (counts[b.id] || 0) + "</div><div class='lbl'>" + b.label + "</div>";
  tiles.appendChild(t);
});

const filters = document.getElementById("filters");
function drawFilters() {
  filters.innerHTML = "";
  [{id:"all", label:"All " + ITEMS.length}].concat(BUCKETS.map((b) => ({id: b.id, label: b.label + " " + (counts[b.id]||0)}))).forEach((b) => {
    const btn = document.createElement("button");
    btn.textContent = b.label;
    btn.className = filter === b.id ? "on" : "";
    btn.addEventListener("click", () => { filter = b.id; drawFilters(); drawWaves(); });
    filters.appendChild(btn);
  });
}

function drawWaves() {
  const root = document.getElementById("waves");
  root.innerHTML = "";
  WAVE_ORDER.forEach((w) => {
    const list = ITEMS.filter((it) => it.bucket === w.bucket && (filter === "all" || filter === it.bucket));
    if (!list.length) return;
    const sec = document.createElement("section");
    sec.className = "wave";
    sec.innerHTML = "<header><h3>" + w.key + " · " + list.length + "</h3></header><p class='meta'>" + w.blurb + "</p>";
    list.forEach((it) => {
      const el = document.createElement("article");
      el.className = "item";
      el.innerHTML = "<div class='id'>" + it.id + "</div><div><div class='name'>" + it.feature + "</div><p class='why'>" + it.why + "</p><div class='mode'>" + it.area + " · " + it.mode + "</div></div><span class='chip b-" + it.bucket + "'>" + it.wave + "</span>";
      sec.appendChild(el);
    });
    root.appendChild(sec);
  });
}
drawFilters();
drawWaves();
</script>
</body>
</html>
"""

if __name__ == "__main__":
    main()
