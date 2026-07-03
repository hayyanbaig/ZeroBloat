# ZeroBloat
ZeroBloat
A transparent, safety-first Windows debloating, privacy, and performance utility.
ZeroBloat strips out AI bloat, tracking, and background junk from Windows — without the black-box behavior most tools in this space rely on. Every change is previewed before it's applied, logged after it's applied, and reversible on its own, independent of System Restore.
Built for people who want control over their PC and want to see exactly what "optimizing" actually means, in raw registry values — not marketing language.


# Why ZeroBloat is different
Most debloaters in this space ask you to trust a description and click a button. ZeroBloat doesn't.
Dry-Run Diff Preview — every tweak shows the exact registry change before you apply it (HKLM\...\Key: 0 → 1), like a git diff for your OS.
Granular, DPAPI-secured undo — every change is individually reversible, even if your organization's Group Policy has disabled System Restore entirely.
Live capability badging — every toggle tells you upfront whether it's supported, untested, or unsupported on your specific Windows edition and build.
A watchdog that tells on itself — background drift detection logs everything it checks, including when it finds nothing wrong. Silence isn't proof of safety; a visible log is.
Zero-Data-Exfiltration Pledge — no telemetry, no analytics, no background network calls. The only network activity is a manual, user-triggered update check against this repository.
Fully open source — every registry key, every script, every strategy this app uses is in this repo. Nothing is hidden.


# Features
1) 
Anti-AI & Bloatware Removal
Disable Copilot entirely (taskbar + background service)
Disable Windows Recall / Click to Do
Strip AI features from Paint, Photos, Notepad
Start Menu purge (Bing web results, MSN widgets)
Classic right-click context menu
Hardware Copilot key remap
Block Edge browser AI sidebar/assistants
Permanently disable the Widgets process
OEM bloatware purge — with a hardware-safety exception list that protects fan control, RGB, and power utilities on gaming laptops

2) 
Deep Data Privacy
Full telemetry disable
Hosts-file network shield against tracking/ad domains
Disable Activity History, Timeline, and Advertising ID
Disable clipboard cloud sync
Purge existing diagnostic logs (.etl files)
Disable location tracking, Wi-Fi Sense, Bluetooth beaconing
Local account de-link (removes Microsoft account tie, build-aware fallback strategies)

3) 
Performance (safe, no trade-offs)
SysMain/Superfetch control
Targeted search indexing scope
Startup item debloater
Standby RAM purge
Safe / Smart presets

4) 
Gaming Tier (opt-in, isolated from everyday Performance)
Gated behind an explicit risk banner. These trade some OS-level protections for performance headroom — nothing here is applied silently.
Ultimate Performance power plan
Hardware-Accelerated GPU Scheduling (HAGS)
Nagle's Algorithm disable (network latency)
VBS / Core Isolation disable — requires a separate confirmation modal, logged distinctly every time it's used

5) 
Deployment & Update Control
Winget-based bulk app installer (150+ apps, Win32 source only — never Microsoft Store)
Scoped driver update blacklist (security patches always pass through)
Feature/quality update deferrals
Configuration export/import

6) 
Safety & Trust Framework
Dry-run diff preview before every change
Batched restore point per session (avoids the 24-hour Windows throttle)
Granular per-tweak undo, encrypted locally via DPAPI
Honest split-undo — registry tweaks are fully reversible; app removals export a manifest first, since they can't always be restored
Drift/Audit Dashboard with full history, filters, and export
Multi-trigger watchdog (Windows Update success, logon, weekly sweep) with a 6-hour cooldown to prevent execution spam
Portable (zero-footprint) mode alongside the installed mode
Zero-residue uninstaller

# How the safety model works & Why this matters
Before you click Apply, you see the exact diff — not a description.
When you click Apply, a session restore point is created once (not per-toggle, to respect Windows' 24-hour limit), and the pre-change value of every tweak is recorded locally, encrypted with DPAPI.
After you click Apply, the app verifies the change actually took effect by reading the value back — it doesn't just assume success.
In the background, the watchdog periodically re-checks that nothing has silently drifted (e.g., a Windows Update quietly re-enabling Recall) and logs every check — successful, uneventful, or corrective.
If you want to undo, you can revert a single tweak instantly, independent of System Restore, or roll back everything from a restore point.
Nothing here is a black box. If you don't believe a claim in this README, the code for it is in this repository.

Windows updates periodically change or re-enable the exact things this app disables. ZeroBloat doesn't pretend this won't happen — the watchdog exists specifically because it does. Tweak logic (registry paths, values, per-build compatibility) is decoupled from the app binary into a signed, checksum-verified manifest (tweaks.json), so fixes for Windows-side changes can ship fast without waiting on a full app release.
Installation

# Requirements

Minimum requirements: Windows 10 (22H2+) or Windows 11, any edition. Some tweaks are Pro/Enterprise-only and will show as unsupported on Home — always verified live via the capability badge, never assumed.
Building from source

Requires .NET (see global.json for the exact SDK version) and Windows, since this project relies on Win32/WMI/registry APIs that don't run cross-platform.
Project structure

Every tweak follows the same interface: Apply(), Verify(), Revert(). This is deliberate — it's what makes the diff preview, the drift watchdog, and the granular undo all work off the same underlying pattern instead of three separate systems.



# Reporting a broken tweak
Windows updates will eventually break something here — that's expected, not a failure state. If a tweak stops working:
Open the Activity Log tab and use Export to grab the relevant entry
Open a GitHub issue using the "Broken Tweak" template — it auto-fills your Windows build, edition, and the affected tweak
Known build-specific issues are tracked in the manifest itself and reflected live in the app's capability badges
Roadmap
[ ] Core tweak engine with diff preview and granular undo
[ ] Multi-trigger watchdog with manifest-driven drift correction
[ ] Push-based gaming profile auto-switching (WMI process event triggers)
[ ] Expanded winget catalog with categorized bulk install
[ ] Community-contributed OEM exception entries
[ ] Localization


# Disclaimer
ZeroBloat modifies system-level Windows settings, including the registry, scheduled tasks, and services. While every change is designed to be reversible and is preceded by a restore point, you are using this software at your own risk. Some tweaks (clearly marked) reduce OS-level security protections in exchange for performance — read the warning dialogs.
This project is not affiliated with or endorsed by Microsoft.
License
GNU General Public License v3.0 — see LICENSE for details.
Contributing
Pull requests welcome, especially for:
New verified working strategies for build-specific tweaks (particularly Local Account De-Link and Recall disable)
OEM exception list additions
Translation/localization
Please open an issue before submitting large feature PRs so the approach can be discussed first.
