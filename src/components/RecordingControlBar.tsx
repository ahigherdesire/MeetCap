import { AnimatePresence, motion } from 'framer-motion'
import { Pause, Play, Square, Mic, MicOff, Volume2 } from 'lucide-react'
import { useStore } from '../store/useStore'
import { formatDuration } from '../lib/format'
import { PlatformBadge } from './PlatformBadge'

export function RecordingControlBar() {
  const state = useStore((s) => s.recordingState)
  const elapsed = useStore((s) => s.elapsedMs)
  const meeting = useStore((s) => s.activeMeeting)
  const audio = useStore((s) => s.settings.defaultAudio)
  const pause = useStore((s) => s.pauseRecording)
  const resume = useStore((s) => s.resumeRecording)
  const stop = useStore((s) => s.stopRecording)

  const active = state === 'recording' || state === 'paused' || state === 'stopping'
  const paused = state === 'paused'

  return (
    <AnimatePresence>
      {active && (
        <motion.div
          initial={{ opacity: 0, y: -24 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -24 }}
          transition={{ type: 'spring', stiffness: 320, damping: 30 }}
          className="fixed inset-x-0 top-0 z-50 flex justify-center px-3 pt-3"
          role="region"
          aria-label="Recording controls"
        >
          <div className="card flex w-full max-w-3xl items-center gap-3 px-4 py-2.5 shadow-glow sm:gap-4">
            {/* Status dot */}
            <span className="relative flex h-3 w-3 shrink-0" aria-hidden>
              {!paused && (
                <span className="absolute inline-flex h-full w-full rounded-full bg-red-500/60 animate-pulse-ring" />
              )}
              <span className={`relative inline-flex h-3 w-3 rounded-full ${paused ? 'bg-amber-500' : 'bg-red-500'}`} />
            </span>

            <div className="flex min-w-0 flex-1 items-center gap-3">
              <span className="font-mono text-lg font-semibold tabular-nums" aria-live="off">
                {formatDuration(elapsed)}
              </span>
              <span
                className={`hidden text-xs font-medium uppercase tracking-wide sm:inline ${
                  paused ? 'text-amber-500' : 'text-red-500'
                }`}
              >
                {state === 'stopping' ? 'Saving…' : paused ? 'Paused' : 'Recording'}
              </span>
              {meeting && (
                <span className="hidden sm:block">
                  <PlatformBadge platform={meeting.platform} />
                </span>
              )}
              <span className="ml-auto hidden items-center gap-2 text-slate-400 md:flex">
                {audio.systemAudio && <Volume2 className="h-4 w-4" aria-label="System audio on" />}
                {audio.microphone ? (
                  <Mic className="h-4 w-4" aria-label="Microphone on" />
                ) : (
                  <MicOff className="h-4 w-4" aria-label="Microphone off" />
                )}
              </span>
            </div>

            <div className="flex items-center gap-2">
              {paused ? (
                <button onClick={resume} className="btn-secondary px-3" aria-label="Resume recording">
                  <Play className="h-4 w-4" />
                  <span className="hidden sm:inline">Resume</span>
                </button>
              ) : (
                <button
                  onClick={pause}
                  className="btn-secondary px-3"
                  aria-label="Pause recording"
                  disabled={state === 'stopping'}
                >
                  <Pause className="h-4 w-4" />
                  <span className="hidden sm:inline">Pause</span>
                </button>
              )}
              <button
                onClick={stop}
                className="btn-danger px-3"
                aria-label="Stop and save recording"
                disabled={state === 'stopping'}
              >
                <Square className="h-4 w-4 fill-current" />
                <span className="hidden sm:inline">Stop</span>
              </button>
            </div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
