# Changelog

## 2.0.0 (9 November 2026)

A complete rewrite of the 2018 tool.

### Fixed

- The 32-bit program folder was never removed: the path was written `Program Files(x86)` without a space.
- Registry settings were only removed from one registry view; the 32-bit view (`WOW6432Node`) and your user settings (`HKEY_CURRENT_USER`) were missed.
- Local application data, ProgramData, temporary files, shortcuts, startup entries, the Windows service and running TeamViewer programs were not handled.
- The tool assumed Windows was on drive C:.
- Errors were hidden, so it could report success when nothing was removed.

### New

- Scans first and shows everything it finds; you choose what to remove.
- Plain-language summary: installed, running, starts with Windows, or leftovers only.
- Runs TeamViewer's own uninstaller when it is still installed.
- Safety checks: only items whose own name is "TeamViewer" can ever be deleted.
- Command-line mode for scripts and IT administrators: `--scan`, `--clean --yes`, `--uninstall`, `--log` (requested in issue #1).
- Help for people targeted by remote-access scams.
- Save a report of what was found and removed.
- Scanning works without administrator rights; the tool asks to restart as administrator only when needed.
- EXE installer, MSI installer and portable EXE, with SHA-256 checksums and build provenance.
- Licence changed from Apache-2.0 to MIT.

## 1.0 (13 November 2018)

First release.
