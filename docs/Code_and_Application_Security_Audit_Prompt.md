# Code and application security audit prompt

Use this for full audits and release reviews. Record results in `docs/SECURITY_AUDIT.md` and the tracker.

Also use installed skills: `vibe-security`, `vibe-security-audit`.

## Ten checks

1. **Secrets** — No keys, tokens, passwords, or private certificates in code, history, or the built app.
2. **Dependencies** — No known vulnerable, outdated, or invented packages.
3. **Static flaws** — No injection, unsafe loading, or weak protection of stored data.
4. **Sign-in and permission** — A person can only see and change what they may (admin elevation, confirm gates, denylists).
5. **Input and output** — Inputs checked; screen text cannot run as code.
6. **Live app** — N/A for this Windows desktop product (no public website). Note N/A with reason.
7. **Hosting** — N/A for local Windows app; note release asset hosting (GitHub Releases) if relevant.
8. **Code quality** — Dead code, duplication, swallowed errors, missing handlers.
9. **Limits and cost** — No unlimited retries or runaway remote calls.
10. **Logs and backups** — Problems recorded without private data; backups/restores where the change affects them.

## Report format

- Seat and model (or same-session provisional)
- Verdict: Pass / Fail / Pass with follow-ups
- Scope (paths)
- Middleware / protective-layer verdict
- Per-check notes
- Follow-ups with owners
