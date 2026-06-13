import { AnimatePresence, motion } from 'framer-motion'
import { Radio, X, ShieldCheck } from 'lucide-react'
import { useStore } from '../store/useStore'
import { PlatformBadge } from './PlatformBadge'

export function MeetingDetectedPopup() {
  const detected = useStore((s) => s.detected)
  const dismiss = useStore((s) => s.dismissDetected)
  const start = useStore((s) => s.startRecording)
  const consentReminder = useStore((s) => s.settings.consentReminder)
  const recordingState = useStore((s) => s.recordingState)

  // Never surface the prompt while a recording is in progress.
  const open = !!detected && recordingState === 'idle'

  return (
    <AnimatePresence>
      {open && detected && (
        <motion.div
          initial={{ opacity: 0, y: 24, scale: 0.96 }}
          animate={{ opacity: 1, y: 0, scale: 1 }}
          exit={{ opacity: 0, y: 24, scale: 0.96 }}
          transition={{ type: 'spring', stiffness: 360, damping: 28 }}
          className="card fixed bottom-4 left-4 z-50 w-[calc(100%-2rem)] max-w-sm overflow-hidden p-0 sm:bottom-6 sm:left-6"
          role="dialog"
          aria-label="Meeting detected"
        >
          <div className="flex items-start gap-3 p-4">
            <span className="relative mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
              <span className="absolute inset-0 rounded-xl bg-brand-400/40 animate-pulse-ring" />
              <Radio className="h-5 w-5" aria-hidden />
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-semibold">Meeting detected</p>
              <p className="mt-0.5 text-sm text-slate-500 dark:text-slate-400">
                Would you like to start recording?
              </p>
              <div className="mt-2 flex items-center gap-2">
                <PlatformBadge platform={detected.platform} />
                <span className="truncate text-xs text-slate-400">{detected.title}</span>
              </div>
            </div>
            <button
              onClick={dismiss}
              className="rounded-lg p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-600 dark:hover:bg-slate-800"
              aria-label="Dismiss"
            >
              <X className="h-4 w-4" />
            </button>
          </div>

          {consentReminder && (
            <div className="flex items-start gap-2 border-t border-slate-100 bg-slate-50 px-4 py-2.5 text-xs text-slate-500 dark:border-slate-800 dark:bg-slate-800/50 dark:text-slate-400">
              <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden />
              <span>You may need participants’ consent depending on local laws and workplace policy.</span>
            </div>
          )}

          <div className="flex gap-2 p-4 pt-3">
            <button onClick={dismiss} className="btn-secondary flex-1">
              Not now
            </button>
            <button onClick={() => start(detected)} className="btn-primary flex-1" autoFocus>
              Start Recording
            </button>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
