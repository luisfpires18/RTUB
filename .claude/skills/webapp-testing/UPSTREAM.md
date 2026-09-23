# Upstream provenance — webapp-testing

Vendored verbatim from Anthropic's official skills repository.

| Field | Value |
| --- | --- |
| Upstream repo | https://github.com/anthropics/skills |
| Upstream path | `skills/webapp-testing/` |
| Source commit | `41bbe19d1a1a7eaab5e7bb9050a417e5c6cffc8f` |
| Vendored on | 2026-09-07 |
| License | Apache-2.0 (`LICENSE.txt`, kept unmodified) |

## Vendored files
- `SKILL.md`
- `scripts/with_server.py`
- `examples/console_logging.py`
- `examples/element_discovery.py`
- `examples/static_html_automation.py`
- `LICENSE.txt`

Only this file (`UPSTREAM.md`) is RTUB-added. Upstream files are unmodified.

**Note:** Upstream `SKILL.md` line 85 contains a trailing space (per source). This is preserved for byte-for-byte provenance.

## Updating
```bash
git clone --depth 1 https://github.com/anthropics/skills.git /tmp/skills
# copy /tmp/skills/skills/webapp-testing/ over this directory, then
# update the source commit above with: git -C /tmp/skills rev-parse HEAD
```

## Security review (performed before first use)
`scripts/with_server.py` was read in full. It only: parses argv, opens TCP
connections to `localhost:<port>` to poll readiness, launches the caller-supplied
server command via `subprocess.Popen(shell=True)`, runs the caller-supplied
command, and terminates/kills the spawned processes on exit. No network egress
beyond localhost, no filesystem writes, no credential or environment
exfiltration, no downloads. It executes only commands the operator passes in,
so treat its arguments with the same care as a shell command.

Permissions for this skill are NOT broadened globally — grant per-invocation.
