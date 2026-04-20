# Jellyfin — Missing File Cleanup Plugin

A Jellyfin plugin that prunes phantom library entries. It registers a scheduled task that walks every movie, episode, audio track and video in your library, checks whether the file is still on disk, and removes the Jellyfin database entry if it is not.

- **Target:** Jellyfin 10.11.x (`targetAbi = 10.11.0.0`, .NET 8)
- **Files on disk are never touched.** The plugin only removes library entries (`DeleteOptions.DeleteFileLocation = false`).
- **Dry-run by default.** The task logs what it would delete until you flip a toggle on the plugin config page.
- **Safety threshold.** Aborts the run if more than *N*% of items are flagged in a single pass (protects against an offline mount wiping the library).

---

## Install via plugin repository

1. In Jellyfin: **Dashboard → Plugins → Repositories → +**.
2. Add this URL:
   ```
   https://raw.githubusercontent.com/Webflue/jellyfin/main/manifest.json
   ```
3. **Dashboard → Plugins → Catalog** — find **Missing File Cleanup** and install.
4. Restart Jellyfin.
5. Configure at **Dashboard → Plugins → Missing File Cleanup**.

> The repo/owner in the URL above must match wherever this repository actually lives; change it if you fork.

## Manual install

1. Download the `missing-file-cleanup_<version>.zip` from the [Releases](../../releases) page.
2. Extract into `<jellyfin-data-dir>/plugins/MissingFileCleanup_<version>/` so the folder contains `Jellyfin.Plugin.MissingFileCleanup.dll` and `meta.json`.
3. Restart Jellyfin.

Default data dirs:

| OS | Path |
|---|---|
| Linux (systemd) | `/var/lib/jellyfin` |
| Docker | the mounted `/config` volume |
| Windows | `%ProgramData%\Jellyfin\Server` |

---

## Configuration

**Dashboard → Plugins → Missing File Cleanup**

| Option | Default | Notes |
|---|---|---|
| Allow deletion | off | Master kill-switch. While off, the task only logs what *would* be deleted. |
| Safety threshold (%) | 25 | Abort if more than this % of scanned items are flagged missing. `0` disables. |
| Scan movies / episodes / audio / other videos | on | Scope toggles. |
| Last run | — | Read-only summary from the most recent run. |

## Running the task

**Dashboard → Scheduled Tasks → Remove Missing Files → Run now** — or use the *Run now* button on the plugin config page.

Default schedule is weekly at 03:00 on Sunday. Change it in Dashboard → Scheduled Tasks.

## Verifying

1. First run with **Allow deletion** off.
2. Open **Dashboard → Logs** — look for lines beginning `Dry-run:` and sampled paths.
3. Spot-check that a file you know is deleted appears in the list, and a file you know still exists does **not**.
4. Flip **Allow deletion** on and trigger the task again.
5. Confirm library counts drop by the expected amount and that on-disk files are untouched.

---

## Building from source

Requires the .NET 8 SDK.

```bash
cd Jellyfin.Plugin.MissingFileCleanup
dotnet publish -c Release -o publish
# output: publish/Jellyfin.Plugin.MissingFileCleanup.dll
```

The GitHub Actions workflow at `.github/workflows/release.yml` builds the DLL, zips it with `meta.json`, publishes a GitHub release on tag push, and updates `manifest.json` at the repo root with a new version entry + MD5 checksum.

### Cutting a release

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow does the rest.

---

## License

GPL-3.0-only — Jellyfin's `Jellyfin.Controller` / `Jellyfin.Model` packages are GPL-3.0, so any plugin linking against them must be too.
