# Codex Usage Widget

A compact desktop widget for the Codex allowance included with a ChatGPT plan. It runs on Windows 11, Linux, and macOS.

The widget asks your locally installed, authenticated Codex CLI for a fresh account-wide usage snapshot. This includes Codex use from other computers. It does not use an API key, scrape browser cookies, or submit an inference prompt.

## Requirements

- Node.js 20 or newer
- Codex CLI installed and signed in with ChatGPT (`codex login`)

## Run locally

```bash
npm install
npm start
```

The app refreshes every minute and can live in the system tray. Set `CODEX_BIN` to the full Codex executable path if `codex` is not on the GUI application's `PATH`.

Hover over the tray icon to see the remaining 5-hour and weekly allowances. The tooltip updates on automatic and manual refreshes, even while the widget is hidden. Offline snapshots are marked as potentially stale, and missing usage is shown as unavailable.

The tray menu includes Always on top, Light theme, Compact mode, and Open Codex Settings, followed by the usage summary. On Linux, click the icon to open this menu. Ubuntu's GNOME AppIndicator extension does not display hover tooltips; other Linux desktops may support them.

### Mini mode

A compact, frameless strip about the height of a Chrome tab bar (46px) showing both window percentages, the next reset countdown, and a live/stale indicator. Toggle it with the ▤ button, the tray menu, or the ▢ button in the strip; the strip is draggable. The choice is remembered across restarts.

## Light and dark mode

The widget follows your OS theme by default. Use the Auto / Light / Dark control in the footer to override it; the choice is remembered between launches. Overriding the theme also affects native window chrome, and the palette swaps instantly with no reload.

### Linux sandbox setup

Ubuntu may block Chromium's user-namespace sandbox. For a development or unpacked build, configure Electron's sandbox helper once:

```bash
npm run fix:linux-sandbox
```

The guarded script changes only the two known `chrome-sandbox` files inside this project to owner `root:root` and mode `4755`. It requires `sudo`. Do not work around this with `--no-sandbox`.

## Build installers

Build on each target operating system for the most reliable native package:

```bash
npm run build:win
npm run build:mac
npm run build:linux
```

Artifacts are written to `dist/`.

## Continuous integration and releases

Two GitHub Actions workflows drive the pipeline:

- `.github/workflows/ci.yml` runs the unit tests on `ubuntu-latest` and `windows-latest` for every push to `master` and every pull request.
- `.github/workflows/release.yml` builds Linux (`AppImage` + `deb`) and Windows (`NSIS setup` + `portable`) on their native runners, then publishes a GitHub Release with the installers attached.

### Version handling

The git tag decides the release version: pushing `v0.2.0` publishes `0.2.0`, and the AppImage, the `.deb`, and both Windows installers (including their file properties) are labeled `0.2.0`. `package.json` does not have to be bumped first - the workflow rewrites the version in its own checkout before building and emits a warning so you can commit the bump afterwards. Versions must look like `X.Y.Z` or `X.Y.Z-prerelease`; a version containing `-` is published as a GitHub prerelease.

Ship a release by bumping and tagging in one commit:

```bash
npm version 0.2.0
git commit -am "Release 0.2.0"
git tag v0.2.0
git push origin master --tags
```

Re-running an existing tag stops instead of overwriting a published release. To repair one, run the Release workflow manually with `force` checked. A manual run also takes an explicit `version` input and creates that tag at the commit it runs on.

Installers are unsigned: no code-signing identity or macOS notarization is configured, so SmartScreen warns on first run. macOS packages are not published; add a matrix entry with `macos-latest` and `build:mac` if you want them.

## Privacy and behavior

The app launches `codex app-server --stdio` and calls the read-only `account/rateLimits/read` method. Credentials remain managed by Codex. No conversation is started, and the app never reads or stores your authentication tokens.

If the live read is unavailable, the widget displays the newest rate-limit snapshot from local Codex session events and clearly labels it as potentially stale.
