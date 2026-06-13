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

- **`browserExtensionAdapter`**  a companion extension with the `tabs` permission
  posts active Meet/Zoom URLs via `window.postMessage`.
- **`desktopAdapter`** — an Electron/Tauri shell inspects window titles and pushes
  matches through a preload bridge.

When neither is present, MeetCap detects the meeting from the surface you *choose* to
share (reading the capture track label), which never bypasses any permission.

### Future-ready cloud upload

`Settings → Cloud upload` is a stubbed, opt-in module. Recordings are always saved
locally first; a cloud target can be added without touching the capture pipeline.
