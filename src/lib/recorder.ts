import type { AudioSourceConfig, AppError } from './types'

export class RecorderError extends Error {
  code: AppError
  constructor(code: AppError, message: string) {
    super(message)
    this.code = code
    this.name = 'RecorderError'
  }
}

function pickMimeType(): string {
  const candidates = [
    'video/webm;codecs=vp9,opus',
    'video/webm;codecs=vp8,opus',
    'video/webm;codecs=h264,opus',
    'video/webm',
    'video/mp4',
  ]
  for (const c of candidates) {
    if (typeof MediaRecorder !== 'undefined' && MediaRecorder.isTypeSupported(c)) return c
  }
  return 'video/webm'
}

export function isSupported(): boolean {
  return (
    typeof navigator !== 'undefined' &&
    !!navigator.mediaDevices?.getDisplayMedia &&
    typeof MediaRecorder !== 'undefined' &&
    typeof window.indexedDB !== 'undefined'
  )
}

export interface StartResult {
  /** The display-capture video track — inspect `.label` for meeting detection. */
  displayTrack: MediaStreamTrack
  mimeType: string
  /** True if any audio track ended up in the recording. */
  hasAudio: boolean
}

interface RecorderCallbacks {
  /** Fired when capture stops unexpectedly (user hit the browser "Stop sharing"). */
  onInterrupted?: () => void
  /** Periodic data flush; useful for crash-resilient buffering. */
  onChunk?: (chunk: Blob) => void
}

/**
 * Wraps getDisplayMedia + optional getUserMedia(mic) into a single MediaRecorder.
 * Audio sources are mixed through a WebAudio graph so tab/system audio and the
 * microphone land in one track.
 */
export class MeetingRecorder {
  private displayStream: MediaStream | null = null
  private micStream: MediaStream | null = null
  private audioCtx: AudioContext | null = null
  private recorder: MediaRecorder | null = null
  private chunks: Blob[] = []
  private mimeType = 'video/webm'
  private cb: RecorderCallbacks

  constructor(callbacks: RecorderCallbacks = {}) {
    this.cb = callbacks
  }

  async start(audio: AudioSourceConfig): Promise<StartResult> {
    if (!isSupported()) {
      throw new RecorderError('unsupported', 'Your browser does not support screen recording.')
    }

    // 1) Ask for the screen/window/tab. The browser shows its own picker —
    //    we never bypass this consent step.
    try {
      this.displayStream = await navigator.mediaDevices.getDisplayMedia({
        video: { frameRate: 30 },
        audio: audio.systemAudio,
      })
    } catch (err) {
      throw mapGetMediaError(err, 'display')
    }

    const displayTrack = this.displayStream.getVideoTracks()[0]
    if (!displayTrack) {
      this.cleanup()
      throw new RecorderError('permission-denied', 'No screen source was shared.')
    }

    // If the user stops sharing from the browser chrome, treat as interruption.
    displayTrack.addEventListener('ended', () => this.cb.onInterrupted?.())

    // 2) Build a mixed audio track if any audio source is requested.
    const audioTracks: MediaStreamTrack[] = []
    const systemAudioTracks = this.displayStream.getAudioTracks()

    if (audio.microphone) {
      try {
        this.micStream = await navigator.mediaDevices.getUserMedia({
          audio: { echoCancellation: true, noiseSuppression: true },
        })
      } catch (err) {
        // Mic failure should not kill a screen+system-audio recording.
        if (!audio.systemAudio || systemAudioTracks.length === 0) {
          this.cleanup()
          throw mapGetMediaError(err, 'mic')
        }
      }
    }

    const hasSystem = audio.systemAudio && systemAudioTracks.length > 0
    const hasMic = !!this.micStream?.getAudioTracks().length

    if (hasSystem && hasMic) {
      // Mix both into one track via WebAudio.
      this.audioCtx = new AudioContext()
      const dest = this.audioCtx.createMediaStreamDestination()
      this.audioCtx.createMediaStreamSource(new MediaStream(systemAudioTracks)).connect(dest)
      this.audioCtx.createMediaStreamSource(this.micStream!).connect(dest)
      audioTracks.push(...dest.stream.getAudioTracks())
    } else if (hasSystem) {
      audioTracks.push(...systemAudioTracks)
    } else if (hasMic) {
      audioTracks.push(...this.micStream!.getAudioTracks())
    }

    if ((audio.systemAudio || audio.microphone) && audioTracks.length === 0) {
      this.cleanup()
      throw new RecorderError(
        'no-audio-source',
        'No audio source was captured. Pick a tab/window that shares audio, or enable your microphone.',
      )
    }

    // 3) Compose final stream and start the recorder.
    const composed = new MediaStream([displayTrack, ...audioTracks])
    this.mimeType = pickMimeType()

    try {
      this.recorder = new MediaRecorder(composed, {
        mimeType: this.mimeType,
        videoBitsPerSecond: 4_000_000,
      })
    } catch (err) {
      this.cleanup()
      throw new RecorderError('unsupported', `Could not start the recorder: ${(err as Error).message}`)
    }

    this.chunks = []
    this.recorder.ondataavailable = (e) => {
      if (e.data && e.data.size > 0) {
        this.chunks.push(e.data)
        this.cb.onChunk?.(e.data)
      }
    }
    // Flush a chunk every second for resilience.
    this.recorder.start(1000)

    return { displayTrack, mimeType: this.mimeType, hasAudio: audioTracks.length > 0 }
  }

  pause() {
    if (this.recorder?.state === 'recording') this.recorder.pause()
  }

  resume() {
    if (this.recorder?.state === 'paused') this.recorder.resume()
  }

  get state(): RecordingStateInternal {
    return (this.recorder?.state as RecordingStateInternal) ?? 'inactive'
  }

  /** Stops everything and resolves with the finished recording blob. */
  stop(): Promise<{ blob: Blob; mimeType: string }> {
    return new Promise((resolve, reject) => {
      if (!this.recorder) {
        reject(new RecorderError('unknown', 'No active recording.'))
        return
      }
      const rec = this.recorder
      rec.onstop = () => {
        const blob = new Blob(this.chunks, { type: this.mimeType })
        this.cleanup()
        resolve({ blob, mimeType: this.mimeType })
      }
      try {
        if (rec.state !== 'inactive') rec.stop()
        else rec.onstop?.(new Event('stop'))
      } catch (err) {
        reject(new RecorderError('unknown', (err as Error).message))
      }
    })
  }

  private cleanup() {
    this.displayStream?.getTracks().forEach((t) => t.stop())
    this.micStream?.getTracks().forEach((t) => t.stop())
    this.audioCtx?.close().catch(() => {})
    this.displayStream = null
    this.micStream = null
    this.audioCtx = null
    this.recorder = null
  }
}

type RecordingStateInternal = 'inactive' | 'recording' | 'paused'

function mapGetMediaError(err: unknown, kind: 'display' | 'mic'): RecorderError {
  const e = err as DOMException
  if (e?.name === 'NotAllowedError' || e?.name === 'SecurityError') {
    return new RecorderError(
      'permission-denied',
      kind === 'mic'
        ? 'Microphone permission was denied.'
        : 'Screen-share permission was denied or cancelled.',
    )
  }
  if (e?.name === 'NotFoundError' || e?.name === 'NotReadableError') {
    return new RecorderError('no-audio-source', 'The requested capture device could not be found or read.')
  }
  return new RecorderError('unknown', e?.message || 'Could not start capture.')
}
