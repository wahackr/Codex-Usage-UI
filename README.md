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

## Privacy and behavior

The app launches `codex app-server --stdio` and calls the read-only `account/rateLimits/read` method. Credentials remain managed by Codex. No conversation is started, and the app never reads or stores your authentication tokens.

If the live read is unavailable, the widget displays the newest rate-limit snapshot from local Codex session events and clearly labels it as potentially stale.
