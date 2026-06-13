import type { DetectedMeeting, Platform } from './types'
import { uid } from './format'

/**
 * Meeting detection.
 *
 * A pure web app is sandboxed: it cannot read other browser tabs or OS window
 * titles. Reliable background detection therefore requires one of two adapters:
 *
 *   - BrowserExtensionAdapter — a companion extension with the `tabs` permission
 *     reports active Meet/Zoom tab URLs to the app.
 *   - DesktopAdapter — an Electron/Tauri shell inspects window titles.
 *
 * Both are stubbed below so the production wiring is obvious. The default,
 * always-available path is `detectFromCaptureSurface`, which inspects the
 * label of a display-capture track the user has *already* chosen to share —
 * this never bypasses any permission.
 */

const MEET_HOSTS = ['meet.google.com']
const ZOOM_HINTS = ['zoom.us', 'zoom.com', 'zoom meeting', 'zoom workplace']

export function classifyLabel(label: string): Platform {
  const l = label.toLowerCase()
  if (MEET_HOSTS.some((h) => l.includes(h)) || l.includes('google meet')) return 'google-meet'
  if (ZOOM_HINTS.some((h) => l.includes(h))) return 'zoom'
  return 'unknown'
}

function titleFor(platform: Platform, label: string): string {
  if (platform === 'google-meet') return label.includes('-') ? label : 'Google Meet call'
  if (platform === 'zoom') return 'Zoom meeting'
  return label || 'Shared window'
}

/** Inspect an already-granted display surface for a known meeting. */
export function detectFromCaptureSurface(track: MediaStreamTrack): DetectedMeeting | null {
  const label = track.label || ''
  const platform = classifyLabel(label)
  if (platform === 'unknown') return null
  return {
    id: uid(),
    platform,
    title: titleFor(platform, label),
    source: 'capture-surface',
    detectedAt: Date.now(),
  }
}

/** Manual entry path used by the "I'm in a meeting" button. */
export function manualMeeting(platform: Platform, title: string): DetectedMeeting {
  return {
    id: uid(),
    platform,
    title: title || (platform === 'zoom' ? 'Zoom meeting' : 'Google Meet call'),
    source: 'manual',
    detectedAt: Date.now(),
  }
}

export interface DetectionAdapter {
  readonly name: string
  readonly available: boolean
  start(onDetect: (m: DetectedMeeting) => void): void
  stop(): void
}

/** Stub: a companion browser extension would post tab URLs here. */
export const browserExtensionAdapter: DetectionAdapter = {
  name: 'Browser extension',
  available: typeof window !== 'undefined' && '__MEETCAP_EXTENSION__' in window,
  start(onDetect) {
    const handler = (e: MessageEvent) => {
      if (e.data?.type !== 'meetcap:meeting') return
      const platform = classifyLabel(e.data.url ?? '')
      if (platform === 'unknown') return
      onDetect({
        id: uid(),
        platform,
        title: e.data.title ?? titleFor(platform, e.data.url ?? ''),
        source: 'tab-url',
        url: e.data.url,
        detectedAt: Date.now(),
      })
    }
    window.addEventListener('message', handler)
    ;(this as unknown as { _h?: typeof handler })._h = handler
  },
  stop() {
    const h = (this as unknown as { _h?: EventListener })._h
    if (h) window.removeEventListener('message', h)
  },
}

/**
 * Desktop shell adapter. When running inside the Electron build, the main
 * process polls OS window titles and pushes matches through the preload bridge
 * (`window.meetcapDesktop`). The native always-on-top popup handles the prompt
 * UI, so this adapter only mirrors detections into the renderer's state.
 */
export const desktopAdapter: DetectionAdapter = {
  name: 'Desktop shell',
  available: typeof window !== 'undefined' && 'meetcapDesktop' in window,
  // The native always-on-top popup is the prompt UI in the desktop build, and
  // the "Start Recording" click arrives via `meetcapDesktop.onStartRecording`
  // (wired in App.tsx). So this adapter intentionally doesn't open the in-app
  // popup — it exists for capability reporting.
  start() {},
  stop() {},
}

export interface MeetcapDesktopBridge {
  isDesktop: boolean
  platform: string
  onStartRecording(cb: (m: { platform: Platform; title: string }) => void): () => void
  onMeetingDetected(cb: (m: { platform: string; title?: string; detectedAt?: number }) => void): () => void
  setRecordingState(state: string): void
  setDetectionEnabled(enabled: boolean): void
}

export function activeAdapters(): DetectionAdapter[] {
  return [browserExtensionAdapter, desktopAdapter].filter((a) => a.available)
}
