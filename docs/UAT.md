# Owner acceptance tests (UAT)

Things the owner tests by hand on their PC. Add new items as each phase lands; tick them when the owner confirms. Live tests must use apps that are **not already installed**.

## P6a Background (build `30acb02d`, installer 2026-10-17 16:28)

- [x] Install: UAC prompt, version 3.0.0-alpha.1, Build `30acb02d` in Settings
- [x] First-run card "Keep apps updated in the background?" → yes creates `\QuantumStep\Quantum Updater <SID>` (logon trigger for this user, 15 s delay, highest privileges, interactive, `--background`, no battery stop, no time limit, IgnoreNew, priority 4)
- [x] Close to tray keeps it running; tray menu shows Open, Check now, Pause, Update all, Quit
- [x] Start menu item `Quantum Updater` (All apps, top level) and desktop shortcut created by the installer
- [x] Second launch from Start menu focuses the open window, no second process
- [ ] Sign out and in: app starts hidden in the tray about 15 s after sign-in (task last run result 0)
- [ ] Toast about 2 min after start when updates are waiting; clicking it opens Updates
- [ ] Uninstall removes the Scheduled Task

## P6d Lists

- [ ] Links from the elevated app open the browser unelevated
- [ ] FileZilla / WinSCP "Installed directly" link works (test in Windows Sandbox)
- [ ] Reinstall works end to end (use an app that is not already installed first)
- [ ] Uninstall on an app that was installed directly opens that app's own uninstaller. Nothing is removed until that window finishes. On 2026-10-23 FileZilla's command was checked and not run.

## P6e Polish

- [ ] With an app open that has an update, Update asks "X is open"; Update later skips it, Close and update closes it politely (it may ask to save) and updates
- [ ] Automatic updates leave an open app for the next round
- [ ] Startup lists the apps that open at sign-in. Turning one off and back on leaves it in place. A Windows or security row has no switch. Quantum Updater's own row points at Settings. On 2026-10-23 a disposable sign-in entry on this PC was turned off and back on and stayed in place, and a security-named entry was refused. The on-screen page is still unchecked.
