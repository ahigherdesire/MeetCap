export type Platform = 'google-meet' | 'zoom' | 'unknown'

export type RecordingState = 'idle' | 'requesting' | 'recording' | 'paused' | 'stopping'

export interface DetectedMeeting {
  id: string
  platform: Platform
  title: string
  /** Where the detection came from — useful for transparency. */
  source: 'tab-url' | 'window-title' | 'capture-surface' | 'manual'
  url?: string
  detectedAt: number
}

export interface AudioSourceConfig {
  /** Capture audio from the shared tab/window/screen (system/tab audio). */
  systemAudio: boolean
  /** Mix in the user's microphone. */
  microphone: boolean
}

export interface RecordingMeta {
  id: string
  name: string
  platform: Platform
  /** Best-effort meeting title at the time of capture. */
  meetingTitle?: string
  createdAt: number
  durationMs: number
  /** Size in bytes. */
  size: number
  mimeType: string
  /** Human-friendly description of where the file lives. */
  saveLocation: string
  audio: AudioSourceConfig
}

export interface StoredRecording extends RecordingMeta {
  blob: Blob
}

export type ToastKind = 'success' | 'error' | 'info' | 'warning'

export interface Toast {
  id: string
  kind: ToastKind
  title: string
  description?: string
  /** ms; 0 means sticky until dismissed. */
  duration?: number
}

export type AppError =
  | 'permission-denied'
  | 'no-audio-source'
  | 'recording-interrupted'
  | 'storage-full'
  | 'unsupported'
  | 'meeting-ended'
  | 'unknown'

export interface ErrorInfo {
  code: AppError
  message: string
  detail?: string
}

export interface Settings {
  theme: 'light' | 'dark' | 'system'
  defaultAudio: AudioSourceConfig
  /** Show the "you may need participant consent" reminder before each recording. */
  consentReminder: boolean
  /** Automatically prompt when a meeting surface is detected. */
  autoDetect: boolean
  /** Future-ready: optional cloud upload after saving locally. */
  cloudUpload: boolean
  storageLocationLabel: string
}
