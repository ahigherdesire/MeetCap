import { create } from 'zustand'
import { MeetingRecorder, RecorderError, isSupported } from '../lib/recorder'
import { detectFromCaptureSurface } from '../lib/detection'
import {
  saveRecording,
  listRecordings,
  renameRecording as dbRename,
  deleteRecording as dbDelete,
} from '../lib/db'
import { uid } from '../lib/format'
import type {
  Settings,
  Toast,
  ToastKind,
  DetectedMeeting,
  RecordingState,
  RecordingMeta,
  StoredRecording,
  ErrorInfo,
  AudioSourceConfig,
} from '../lib/types'

export type Route = 'dashboard' | 'library' | 'settings' | 'permissions'

const SETTINGS_KEY = 'meetcap:settings'

const defaultSettings: Settings = {
  theme: 'system',
  defaultAudio: { systemAudio: true, microphone: true },
  consentReminder: true,
  autoDetect: true,
  cloudUpload: false,
  storageLocationLabel: 'This browser (local, IndexedDB)',
}

function loadSettings(): Settings {
  try {
    const raw = localStorage.getItem(SETTINGS_KEY)
    if (raw) return { ...defaultSettings, ...JSON.parse(raw) }
  } catch {
    /* ignore */
  }
  return defaultSettings
}

// The recorder instance is non-serializable, so it lives outside the store.
let recorder: MeetingRecorder | null = null
let tickHandle: number | null = null

interface AppState {
  supported: boolean
  route: Route
  settings: Settings
  toasts: Toast[]

  detected: DetectedMeeting | null
  recordingState: RecordingState
  /** Active recording elapsed time, ms (excludes paused spans). */
  elapsedMs: number
  activeMeeting: DetectedMeeting | null

  recordings: RecordingMeta[]
  summary: RecordingMeta | null
  error: ErrorInfo | null

  // actions
  setRoute: (r: Route) => void
  setTheme: (t: Settings['theme']) => void
  updateSettings: (patch: Partial<Settings>) => void

  toast: (kind: ToastKind, title: string, description?: string, duration?: number) => void
  dismissToast: (id: string) => void

  setDetected: (m: DetectedMeeting | null) => void
  dismissDetected: () => void

  startRecording: (meeting: DetectedMeeting | null, audio?: AudioSourceConfig) => Promise<void>
  pauseRecording: () => void
  resumeRecording: () => void
  stopRecording: () => Promise<void>

  loadRecordings: () => Promise<void>
  renameRecording: (id: string, name: string) => Promise<void>
  deleteRecording: (id: string) => Promise<void>
  clearSummary: () => void
  clearError: () => void
}

export const useStore = create<AppState>((set, get) => ({
  supported: isSupported(),
  route: 'dashboard',
  settings: loadSettings(),
  toasts: [],

  detected: null,
  recordingState: 'idle',
  elapsedMs: 0,
  activeMeeting: null,

  recordings: [],
  summary: null,
  error: null,

  setRoute: (route) => set({ route }),

  setTheme: (theme) => {
    get().updateSettings({ theme })
  },

  updateSettings: (patch) => {
    const settings = { ...get().settings, ...patch }
    set({ settings })
    try {
      localStorage.setItem(SETTINGS_KEY, JSON.stringify(settings))
    } catch {
      /* ignore quota errors for settings */
    }
  },

  toast: (kind, title, description, duration = 4000) => {
    const t: Toast = { id: uid(), kind, title, description, duration }
    set((s) => ({ toasts: [...s.toasts, t] }))
    if (duration > 0) {
      window.setTimeout(() => get().dismissToast(t.id), duration)
    }
  },
  dismissToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),

  setDetected: (detected) => set({ detected }),
  dismissDetected: () => set({ detected: null }),

  startRecording: async (meeting, audio) => {
    const { settings, supported } = get()
    if (!supported) {
      set({
        error: {
          code: 'unsupported',
          message: 'This browser can’t record screens.',
          detail: 'Use a recent Chrome, Edge, or Firefox on desktop.',
        },
      })
      return
    }

    const audioConfig = audio ?? settings.defaultAudio
    set({ recordingState: 'requesting', detected: null, error: null })

    recorder = new MeetingRecorder({
      onInterrupted: () => {
        // Browser-level "Stop sharing" — finalize gracefully.
        void get().stopRecording()
        get().toast('warning', 'Sharing stopped', 'Screen sharing ended, so we saved what we had.')
      },
    })

    try {
      const result = await recorder.start(audioConfig)

      // Opportunistic detection from the surface the user just chose to share.
      let active = meeting
      if (!active) {
        const fromSurface = detectFromCaptureSurface(result.displayTrack)
        if (fromSurface) active = fromSurface
      }

      const startedAt = Date.now()
      set({ recordingState: 'recording', elapsedMs: 0, activeMeeting: active })

      // Elapsed timer that respects pauses.
      let accumulated = 0
      let lastResume = startedAt
      if (tickHandle) window.clearInterval(tickHandle)
      tickHandle = window.setInterval(() => {
        const state = get().recordingState
        if (state === 'recording') {
          set({ elapsedMs: accumulated + (Date.now() - lastResume) })
        } else if (state === 'paused') {
          accumulated = get().elapsedMs
          lastResume = Date.now()
        }
      }, 250)

      get().toast('success', 'Recording started', active ? active.title : 'Capturing your screen.')
    } catch (err) {
      set({ recordingState: 'idle', activeMeeting: null })
      const re = err instanceof RecorderError ? err : new RecorderError('unknown', String(err))
      const errorInfo: ErrorInfo = { code: re.code, message: re.message }
      // Cancelling the picker is a normal action, not a hard error screen.
      if (re.code === 'permission-denied') {
        get().toast('error', 'Recording not started', re.message)
      } else {
        set({ error: errorInfo })
        get().toast('error', 'Recording failed', re.message)
      }
    }
  },

  pauseRecording: () => {
    if (get().recordingState !== 'recording' || !recorder) return
    recorder.pause()
    set({ recordingState: 'paused' })
    get().toast('info', 'Recording paused')
  },

  resumeRecording: () => {
    if (get().recordingState !== 'paused' || !recorder) return
    recorder.resume()
    set({ recordingState: 'recording' })
    get().toast('info', 'Recording resumed')
  },

  stopRecording: async () => {
    if (!recorder || get().recordingState === 'idle' || get().recordingState === 'stopping') return
    const durationMs = get().elapsedMs
    const meeting = get().activeMeeting
    const audioConfig = get().settings.defaultAudio
    set({ recordingState: 'stopping' })
    if (tickHandle) {
      window.clearInterval(tickHandle)
      tickHandle = null
    }

    try {
      const { blob, mimeType } = await recorder.stop()
      recorder = null

      const meta: RecordingMeta = {
        id: uid(),
        name: defaultName(meeting),
        platform: meeting?.platform ?? 'unknown',
        meetingTitle: meeting?.title,
        createdAt: Date.now(),
        durationMs,
        size: blob.size,
        mimeType,
        saveLocation: get().settings.storageLocationLabel,
        audio: audioConfig,
      }

      const stored: StoredRecording = { ...meta, blob }
      try {
        await saveRecording(stored)
      } catch (err) {
        const isQuota = err instanceof DOMException && err.name === 'QuotaExceededError'
        set({
          recordingState: 'idle',
          activeMeeting: null,
          error: isQuota
            ? {
                code: 'storage-full',
                message: 'Storage is full.',
                detail: 'Free up space or delete old recordings, then try again.',
              }
            : { code: 'unknown', message: 'Could not save the recording.', detail: String(err) },
        })
        get().toast('error', 'Save failed', isQuota ? 'Storage is full.' : 'Could not save the recording.')
        return
      }

      set({ recordingState: 'idle', activeMeeting: null, summary: meta })
      await get().loadRecordings()
      get().toast('success', 'Recording saved', `${meta.name} • stored locally.`)
    } catch (err) {
      const re = err instanceof RecorderError ? err : new RecorderError('recording-interrupted', String(err))
      recorder = null
      set({
        recordingState: 'idle',
        activeMeeting: null,
        error: { code: re.code, message: re.message },
      })
      get().toast('error', 'Recording interrupted', re.message)
    }
  },

  loadRecordings: async () => {
    const recordings = await listRecordings()
    set({ recordings })
  },

  renameRecording: async (id, name) => {
    await dbRename(id, name)
    await get().loadRecordings()
    set((s) => (s.summary?.id === id ? { summary: { ...s.summary, name } } : {}))
    get().toast('success', 'Renamed', name)
  },

  deleteRecording: async (id) => {
    await dbDelete(id)
    await get().loadRecordings()
    get().toast('info', 'Recording deleted')
  },

  clearSummary: () => set({ summary: null }),
  clearError: () => set({ error: null }),
}))

function defaultName(meeting: DetectedMeeting | null): string {
  const stamp = new Date().toLocaleString(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
  const base =
    meeting?.platform === 'google-meet'
      ? 'Google Meet'
      : meeting?.platform === 'zoom'
        ? 'Zoom'
        : 'Recording'
  return `${base} — ${stamp}`
}
