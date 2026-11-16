# How this team works

The person in the chat is the **Manager**. Feature work follows this order every time. Shared standing rules also live in the user-level Cursor rule `team-workflow`.

## Roles this session can create

| Role | How created | Owns |
|---|---|---|
| Manager | Main chat (this session) | Outcome, plan approval, verify, tracker, owner handoff |
| Design Architect | Task subagent `design-architect` (strong reasoning when available) | Plan, mockups/flows, design validation |
| Security & Code Architect | Task subagent `security-architect` (strong reasoning when available) | Boundaries, ten audit checks, traceable findings |
| Worker bee 1 | Task subagent `worker-bee` (fast model) | One small owned-file task |
| Worker bee 2 | Second `worker-bee` in parallel | Separate small task; flag overlap |

If Other Models / seat limits block architects, the Manager fills the seat the same turn, labels **same-session provisional**, and leaves independent architect review pending on the tracker. Never idle the queue.

## Cycle

1. Design Architect first — picture, screen flow, protective-layer need, mockups, short plan, testable outcomes.
2. Manager approves — write into `tracker.html` and `docs/DECISIONS.md` before build.
3. Two Worker bees — one small step each; no design choices.
4. Manager verifies — match the plan; return or scoped fix.
5. Security Architect — `docs/SECURITY_AUDIT.md` + `docs/Code_and_Application_Security_Audit_Prompt.md` + vibe-security skills.
6. Design Architect validates finished work against plan/mockups/flows.
7. Manager updates `tracker.html` (offline board in this folder). Reload in the browser.

## Product-specific rules (DeskVerve)

- Product is the **Windows** app. A real test is a new `QuantumUpdater.exe` (full onedir / zip) opened on this PC. There is no website test copy.
- Offline tracker: `tracker.html` in the project root. Update it in the same turn as any plan or status change.
- Official releases: `merchanthasan2/AudoChocolatey` tags `v*` → `QuantumUpdater-Windows.zip`.
- Do not wait on owner decisions; queue Owner later and keep building the recommended provisional path (`development-tracker` rule).
- Park D live theme stays locked until owner approve; do not change live CSS for Park without that lock.
- Active stack today: **C# + WinUI 3** (ADR 043). The Python app is reference only in `archive/python-app`.

## Session start checklist

1. Read this file, `tracker.html`, `docs/DECISIONS.md`, `docs/ARCHITECTURE.md`, `docs/SECURITY_AUDIT.md`.
2. Confirm branch (`channel/windows` for Windows work). Preserve dirty work.
3. Open `tracker.html` in the browser; confirm the task table is visible.
4. Note winget PR / CLA / version stamp risks on the resume card.
