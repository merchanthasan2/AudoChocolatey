        { id: "29.2", text: "Owner on-screen checks: scan list, Marketplace Installed, uninstall, About this computer, readable buttons, real icons, emblem", status: "waiting", owner: "owner" }
      ]
    },
    {
      id: "30",
      title: "Wave 1 — Finish Cleanup (after current System tools)",
      items: [
        { id: "30.1", text: "Restart-required status after updates and cleans", status: "waiting", owner: "product" },
        { id: "30.2", text: "Empty Recycle Bin with count and size preview", status: "waiting", owner: "engine" },
        { id: "30.3", text: "Browser history and cookies, separate opt-in from cache, per profile", status: "waiting", owner: "engine" },
        { id: "30.4", text: "Explorer thumbnail cache cleanup", status: "waiting", owner: "engine" },
        { id: "30.5", text: "DirectX shader cache cleanup, with rebuild warning", status: "waiting", owner: "engine" },
        { id: "30.6", text: "Windows Error Reporting queues and archives (leave Event Logs)", status: "waiting", owner: "engine" },
        { id: "30.7", text: "Delivery Optimization cache, skip if servicing is active", status: "waiting", owner: "engine" }
      ]
    },
    {
      id: "31",
      title: "Wave 2 — App and shell repair",
      items: [
        { id: "31.1", text: "Catalog source refresh and recovery reset", status: "waiting", owner: "engine" },
        { id: "31.2", text: "Installed app list export and reinstall (IDs only)", status: "waiting", owner: "engine" },
        { id: "31.3", text: "Scheduled cleanup, user-chosen categories and cadence", status: "waiting", owner: "engine" },
        { id: "31.4", text: "Duplicate file finder, Recycle Bin default", status: "waiting", owner: "engine" },
        { id: "31.5", text: "Repair an installed desktop app where supported", status: "waiting", owner: "engine" },
        { id: "31.6", text: "Repair or reset a packaged app (reset wipes that app data)", status: "waiting", owner: "engine" },
        { id: "31.7", text: "Reset Microsoft Store cache for this user", status: "waiting", owner: "engine" },
        { id: "31.8", text: "Re-register an installed Store app for this account", status: "waiting", owner: "engine" },
        { id: "31.9", text: "Rebuild Explorer icon cache", status: "waiting", owner: "engine" },
        { id: "31.10", text: "Clear stuck print jobs and restart spooler", status: "waiting", owner: "engine" },
        { id: "31.11", text: "Remove a broken or orphaned installation, then leftover scan", status: "waiting", owner: "engine" }
      ]
    },
    {
      id: "32",
      title: "Wave 3 — Windows health",
      items: [
        { id: "32.1", text: "Windows Update scan and install, separate from app updates", status: "waiting", owner: "engine" },
        { id: "32.2", text: "Windows Update component repair, targeted", status: "waiting", owner: "engine" },
        { id: "32.3", text: "Uninstall an eligible Windows update, with security warning", status: "waiting", owner: "engine" },
        { id: "32.4", text: "Protected system file scan and repair (SFC)", status: "waiting", owner: "engine" },
        { id: "32.5", text: "Component-store integrity repair (DISM RestoreHealth)", status: "waiting", owner: "engine" },
        { id: "32.6", text: "WMI repository verify, then salvage only if needed", status: "waiting", owner: "engine" },
        { id: "32.7", text: "Create a system restore point", status: "waiting", owner: "engine" },
        { id: "32.8", text: "Disk and SSD health, read-only", status: "waiting", owner: "engine" },
        { id: "32.9", text: "Online file-system scan", status: "waiting", owner: "engine" },
        { id: "32.10", text: "Repair file-system errors after confirm", status: "waiting", owner: "engine" },
        { id: "32.11", text: "SSD ReTrim when the volume supports it", status: "waiting", owner: "engine" },
        { id: "32.12", text: "View and export Windows events", status: "waiting", owner: "engine" },
        { id: "32.13", text: "On-demand malware scan via Windows Security", status: "waiting", owner: "engine" },
        { id: "32.14", text: "BitLocker status audit, never print recovery keys", status: "waiting", owner: "engine" },
        { id: "32.15", text: "Defender status and intelligence updates", status: "waiting", owner: "engine" }
      ]
    },
    {
      id: "33",
      title: "Wave 4 — Network and power",
      items: [
        { id: "33.1", text: "Explorer context-menu manager, third-party reversible disable", status: "waiting", owner: "engine" },
        { id: "33.2", text: "Blue-screen dump triage, stop code as a lead", status: "waiting", owner: "engine" },
        { id: "33.3", text: "Sleep and wake troubleshooting", status: "waiting", owner: "engine" },
        { id: "33.4", text: "Select a power plan with revert", status: "waiting", owner: "engine" },
        { id: "33.5", text: "Flush DNS resolver cache", status: "waiting", owner: "engine" },
        { id: "33.6", text: "Reset Winsock catalog, VPN and restart warning", status: "waiting", owner: "engine" },
        { id: "33.7", text: "Reset TCP/IP settings after backing up adapters", status: "waiting", owner: "engine" },
        { id: "33.8", text: "Renew DHCP lease on the selected adapter", status: "waiting", owner: "engine" },
        { id: "33.9", text: "Restore firewall defaults after export", status: "waiting", owner: "engine" },
        { id: "33.10", text: "Restore the default Hosts file after showing replacements", status: "waiting", owner: "engine" },
        { id: "33.11", text: "Connection and port inventory, read-only", status: "waiting", owner: "engine" },
        { id: "33.12", text: "Latency, packet loss, and route trace", status: "waiting", owner: "engine" },
        { id: "33.13", text: "Test a specific TCP port", status: "waiting", owner: "engine" },
        { id: "33.14", text: "DNS record lookup", status: "waiting", owner: "engine" },
        { id: "33.15", text: "Wi-Fi signal and channel survey", status: "waiting", owner: "engine" },
        { id: "33.16", text: "Windows clock sync diagnosis", status: "waiting", owner: "engine" },
        { id: "33.17", text: "Inspect and reset WinHTTP proxy", status: "waiting", owner: "engine" },
        { id: "33.18", text: "Firewall rule inspection and backup", status: "waiting", owner: "engine" }
      ]
    },
    {
      id: "34",
      title: "Wave 5 — Files and admin",
      items: [
        { id: "34.1", text: "Create and extract ZIP archives", status: "waiting", owner: "engine" },
        { id: "34.2", text: "Transparent folder compression with measured savings", status: "waiting", owner: "engine" },
        { id: "34.3", text: "Mount and eject ISO or VHD images, read-only default", status: "waiting", owner: "engine" },
        { id: "34.4", text: "File checksum calculation and verification", status: "waiting", owner: "engine" },
        { id: "34.5", text: "Find empty folders", status: "waiting", owner: "engine" },
        { id: "34.6", text: "Find and repair broken shortcuts", status: "waiting", owner: "engine" },
        { id: "34.7", text: "Find which app is locking a file", status: "waiting", owner: "engine" },
        { id: "34.8", text: "Format a removable volume only, never the system drive", status: "waiting", owner: "engine" },
        { id: "34.9", text: "Verify publisher signature of a chosen file", status: "waiting", owner: "engine" },
        { id: "34.10", text: "Windows privacy settings review, then open Windows", status: "waiting", owner: "product" },
        { id: "34.11", text: "Check file and folder permissions, read-only", status: "waiting", owner: "engine" },
        { id: "34.12", text: "Live process and resource monitor, read-only", status: "waiting", owner: "engine" },
        { id: "34.13", text: "Close or end an unresponsive app after confirm", status: "waiting", owner: "engine" },
        { id: "34.14", text: "Service management with denylist for security and update services", status: "waiting", owner: "engine" },
        { id: "34.15", text: "Windows optional feature manager", status: "waiting", owner: "engine" },
        { id: "34.16", text: "Preview .reg files; import off by default", status: "waiting", owner: "engine" },
        { id: "34.17", text: "Keep Windows awake temporarily, with cancel", status: "waiting", owner: "engine" }
      ]
    }
  ]
};
