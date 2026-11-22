# SDD ledger — plan: docs/superpowers/plans/2026-10-13-p6.md
Ruling: start_at_login off until the owner says yes to a one-time prompt (architect rec.) — avoids silently creating an elevated logon task — cost if wrong: one extra click
P6a T1: complete (commit db5833e, gate green, cargo 252)
P6a T2+T3: complete (commit e368eb7, cargo 257; live: close hides, 2nd launch re-shows, 1 process)
P6a T4+T5: complete (commit d6d3c00, cargo 265). Ruling: notifications only while the window is hidden — an open window already shows the list — cost if wrong: a minimized window gets no toast
