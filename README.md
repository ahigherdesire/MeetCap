# MeetCapppp - Consent-first meeting recorder

A modern, privacy-first web/desktop app for recording **your own** Google Meet and
Zoom sessions. Recording only ever starts after you explicitly click **Start
Recording** never secretly, never automatically.

> ⚠️ **Consent matters.** Depending on local laws and workplace policy, you may need
> participants' consent before recording. MeetCap always shows a clear indicator
> while recording and reminds you about consent — it never records in stealth and
> never bypasses browser, OS, Meet, or Zoom permissions.

## Features

- **Consent-first capture** explicit start, always-visible recording indicator.
- **Meeting detection**  recognizes Google Meet / Zoom from the shared surface, with
  pluggable adapters for a companion browser extension or desktop shell.
- **Non-intrusive detection popup** — "Meeting detected. Would you like to start recording?"
- **Full recording controls**  pause, resume, stop, save.
- **Screen + audio capture** tab/system audio and microphone, mixed via WebAudio.
- **Clean summary screen**  title, date/time, duration, file size, save location, rename.
- **Recordings library**  search, play, download, rename, delete.
- **Privacy-first settings**  explains exactly what is stored and where.
- **Permissions screen**  review and test screen, mic, and storage access.
- **Robust error & recovery**  permission denied, no audio source, interruption,
  storage full, unsupported browser, meeting ended.
- **Polished UX**  modern SaaS UI, responsive, dark/light/system themes, smooth
  animations (respects `prefers-reduced-motion`), keyboard-accessible, toast feedback.

## Tech stack

- **React 18 + TypeScript + Vite**
- **Tailwind CSS** (dark mode via class strategy)
- **Framer Motion** for animations
- **Zustand** for state
- **IndexedDB** (via `idb`) for local recording storage
- **MediaRecorder + getDisplayMedia/getUserMedia** for capture

## Getting started

```bash
cd meetcap
npm install
npm run dev      # http://localhost:5173
```

Build for production:

```bash
npm run build
npm run preview
```

Use a recent **desktop** Chrome, Edge, or Firefox — screen capture (`getDisplayMedia`)
is required. To capture call audio, tick **"Share tab audio"** in the browser's share
picker.

## Desktop app (background detection) — recommended

The web build is sandboxed: it can only detect the surface you choose to share. For
**true system-wide, background detection** — a popup that appears automatically at the
start of *any* Zoom or Google Meet call, no matter which browser tab or app it's in —
run the **Electron desktop app**:

```bash
npm run electron:dev     # dev: Vite + Electron with hot reload
npm run dist             # build installers into release/ (Windows .exe / macOS .dmg / Linux AppImage)
```

What the desktop shell adds over the web app:

- **Runs in the system tray** in the background — no window needed.
- **System-wide meeting detection** — the main process polls OS window titles every
  few seconds (`electron/detection.cjs`) to spot a live "Zoom Meeting" window or a
  browser tab titled "Meet - …", across the native Zoom client and any browser.
- **Native always-on-top popup** (`electron/popup.html`) — *"Meeting detected. Would
  you like to start recording?"* — shown automatically when a call starts. Recording
  still only begins when you click **Start Recording**.
- **System (loopback) audio capture** on Windows/Linux via Electron's
  `setDisplayMediaRequestHandler` — something a browser tab cannot do.

This is **read-only observation of window titles** — it never injects into, scrapes,
or bypasses Zoom, Meet, the browser, or the OS. Detection can be toggled from the tray
menu or **Settings → Auto-detect meetings**.

| Desktop file                 | Role                                              |
| ---------------------------- | ------------------------------------------------- |
| `electron/main.cjs`          | App lifecycle, tray, windows, IPC, capture grants |
| `electron/detection.cjs`     | Background window-title meeting detector          |
| `electron/popup.html`        | Native detection popup UI                         |
| `electron/preload.cjs`       | Secure bridge to the React renderer               |
| `electron/popup-preload.cjs` | Secure bridge for the popup                        |
| `electron/make-icon.cjs`     | Generates the app/tray icon PNG (no native deps)  |

## Architecture notes

| Concern            | Where                          |
| ------------------ | ------------------------------ |
| Capture engine     | `src/lib/recorder.ts`          |
| Meeting detection  | `src/lib/detection.ts`         |
| Local storage      | `src/lib/db.ts` (IndexedDB)    |
| App state/actions  | `src/store/useStore.ts`        |
| UI pages           | `src/pages/*`                  |
| Overlays/controls  | `src/components/*`             |

### Detection beyond a single tab

A sandboxed web page cannot read other browser tabs or OS window titles. MeetCap
exposes two adapter stubs in `detection.ts` so this can be wired up in production:

- **`browserExtensionAdapter`** — a companion extension with the `tabs` permission
  posts active Meet/Zoom URLs via `window.postMessage` (stub provided).
- **`desktopAdapter`** — **implemented** in the Electron build (see above): the main
  process inspects OS window titles and drives the native popup, then triggers
  recording in the renderer through `window.meetcapDesktop`.

When neither is present, MeetCap detects the meeting from the surface you *choose* to
share (reading the capture track label), which never bypasses any permission.

### Future-ready cloud upload

`Settings → Cloud upload` is a stubbed, opt-in module. Recordings are always saved
locally first; a cloud target can be added without touching the capture pipeline.
