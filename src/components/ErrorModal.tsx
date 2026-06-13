import {
  ShieldAlert,
  MicOff,
  Unplug,
  Database,
  MonitorX,
  PhoneOff,
  AlertOctagon,
  type LucideIcon,
} from 'lucide-react'
import { Modal } from './Modal'
import { useStore } from '../store/useStore'
import type { AppError } from '../lib/types'

const config: Record<AppError, { icon: LucideIcon; title: string; hint: string; recover: string }> = {
  'permission-denied': {
    icon: ShieldAlert,
    title: 'Permission denied',
    hint: 'MeetCap needs your permission to capture the screen and audio. Nothing is recorded without it.',
    recover: 'Open Permissions to review what’s needed and try again.',
  },
  'no-audio-source': {
    icon: MicOff,
    title: 'No audio source found',
    hint: 'We couldn’t capture any audio. When sharing a tab, tick “Share tab audio”, or enable your microphone.',
    recover: 'Check your audio settings and start a new recording.',
  },
  'recording-interrupted': {
    icon: Unplug,
    title: 'Recording interrupted',
    hint: 'The capture stopped unexpectedly. We saved whatever was captured up to that point.',
    recover: 'You can start a fresh recording whenever you’re ready.',
  },
  'storage-full': {
    icon: Database,
    title: 'Storage full',
    hint: 'There isn’t enough local space to save this recording.',
    recover: 'Delete older recordings or free up disk space, then try again.',
  },
  unsupported: {
    icon: MonitorX,
    title: 'Unsupported browser or device',
    hint: 'Screen recording isn’t available here. Use a recent desktop Chrome, Edge, or Firefox.',
    recover: 'Switch to a supported browser to record meetings.',
  },
  'meeting-ended': {
    icon: PhoneOff,
    title: 'Meeting ended',
    hint: 'The meeting appears to have ended. We stopped and saved your recording.',
    recover: 'Find your recording in the Library.',
  },
  unknown: {
    icon: AlertOctagon,
    title: 'Something went wrong',
    hint: 'An unexpected error occurred.',
    recover: 'Please try again.',
  },
}

export function ErrorModal() {
  const error = useStore((s) => s.error)
  const clear = useStore((s) => s.clearError)
  const setRoute = useStore((s) => s.setRoute)
  if (!error) return null

  const c = config[error.code] ?? config.unknown
  const Icon = c.icon

  return (
    <Modal open={!!error} onClose={clear} title="">
      <div className="px-6 pb-6 pt-2">
        <div className="flex flex-col items-center text-center">
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-red-50 text-red-500 dark:bg-red-500/15">
            <Icon className="h-8 w-8" aria-hidden />
          </div>
          <h2 className="mt-4 text-lg font-semibold">{c.title}</h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{error.message || c.hint}</p>
        </div>

        <div className="mt-5 rounded-xl bg-slate-50 p-4 text-sm text-slate-600 dark:bg-slate-800/50 dark:text-slate-300">
          <p className="font-medium text-slate-700 dark:text-slate-200">How to recover</p>
          <p className="mt-1">{c.recover}</p>
          {error.detail && (
            <p className="mt-2 break-words font-mono text-xs text-slate-400">{error.detail}</p>
          )}
        </div>

        <div className="mt-6 flex gap-2">
          <button onClick={clear} className="btn-secondary flex-1">
            Dismiss
          </button>
          {error.code === 'permission-denied' ? (
            <button
              onClick={() => {
                clear()
                setRoute('permissions')
              }}
              className="btn-primary flex-1"
            >
              Review permissions
            </button>
          ) : error.code === 'storage-full' ? (
            <button
              onClick={() => {
                clear()
                setRoute('library')
              }}
              className="btn-primary flex-1"
            >
              Manage recordings
            </button>
          ) : (
            <button onClick={clear} className="btn-primary flex-1">
              Got it
            </button>
          )}
        </div>
      </div>
    </Modal>
  )
}
