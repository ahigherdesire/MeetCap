import { motion } from 'framer-motion'
import {
  Video,
  Volume2,
  Mic,
  ShieldCheck,
  MonitorPlay,
  Radio,
  CircleDot,
  ArrowRight,
} from 'lucide-react'
import { useStore } from '../store/useStore'
import { manualMeeting } from '../lib/detection'
import { formatRelative } from '../lib/format'
import { PlatformBadge } from '../components/PlatformBadge'
import type { Platform } from '../lib/types'

export function Dashboard() {
  const start = useStore((s) => s.startRecording)
  const state = useStore((s) => s.recordingState)
  const audio = useStore((s) => s.settings.defaultAudio)
  const updateSettings = useStore((s) => s.updateSettings)
  const setDetected = useStore((s) => s.setDetected)
  const supported = useStore((s) => s.supported)
  const recordings = useStore((s) => s.recordings)
  const setRoute = useStore((s) => s.setRoute)

  const busy = state !== 'idle'

  const setAudio = (patch: Partial<typeof audio>) =>
    updateSettings({ defaultAudio: { ...audio, ...patch } })

  const simulate = (platform: Platform) =>
    setDetected(manualMeeting(platform, platform === 'zoom' ? 'Zoom meeting' : 'Weekly sync'))

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      {/* Hero */}
      <motion.section
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
        className="card overflow-hidden"
      >
        <div className="relative p-6 sm:p-8">
          <div
            className="pointer-events-none absolute -right-16 -top-16 h-56 w-56 rounded-full bg-brand-500/10 blur-3xl"
            aria-hidden
          />
          <div className="relative">
            <span className="inline-flex items-center gap-1.5 rounded-full bg-brand-50 px-3 py-1 text-xs font-medium text-brand-700 dark:bg-brand-500/15 dark:text-brand-200">
              <CircleDot className="h-3.5 w-3.5" /> Ready to record
            </span>
            <h2 className="mt-4 text-2xl font-bold tracking-tight sm:text-3xl">
              Record your own meetings, with consent.
            </h2>
            <p className="mt-2 max-w-xl text-sm text-slate-500 dark:text-slate-400">
              MeetCap captures your Google Meet and Zoom sessions locally. Recording only ever begins
              after you explicitly choose to start — never secretly, never automatically.
            </p>

            <div className="mt-6 flex flex-wrap items-center gap-3">
              <button
                onClick={() => start(null)}
                disabled={busy || !supported}
                className="btn-primary px-5 py-3 text-base"
              >
                <Video className="h-5 w-5" />
                {state === 'requesting' ? 'Requesting…' : 'Start Recording'}
              </button>
              <button onClick={() => setRoute('permissions')} className="btn-secondary px-5 py-3 text-base">
                <ShieldCheck className="h-5 w-5" />
                Check permissions
              </button>
            </div>

            {!supported && (
              <p className="mt-4 inline-flex items-center gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-700 dark:bg-amber-500/10 dark:text-amber-300">
                <MonitorPlay className="h-4 w-4" />
                This browser doesn’t support screen recording. Try desktop Chrome, Edge, or Firefox.
              </p>
            )}
          </div>
        </div>

        {/* Audio source toggles */}
        <div className="grid grid-cols-1 gap-px border-t border-slate-200 bg-slate-200 dark:border-slate-800 dark:bg-slate-800 sm:grid-cols-2">
          <AudioToggle
            icon={Volume2}
            title="Tab / system audio"
            desc="Capture what others say in the call"
            checked={audio.systemAudio}
            onChange={(v) => setAudio({ systemAudio: v })}
          />
          <AudioToggle
            icon={Mic}
            title="Microphone"
            desc="Capture your own voice"
            checked={audio.microphone}
            onChange={(v) => setAudio({ microphone: v })}
          />
        </div>
      </motion.section>

      {/* Consent reminder */}
      <div className="flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-500/20 dark:bg-amber-500/10 dark:text-amber-200">
        <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0" aria-hidden />
        <p>
          <span className="font-semibold">Recording consent matters.</span> Depending on your local
          laws and workplace policy, you may need to inform participants and get their consent before
          recording. MeetCap always shows a clear indicator while recording is active.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Detection demo */}
        <section className="card p-6">
          <div className="flex items-center gap-2">
            <Radio className="h-5 w-5 text-brand-500" />
            <h3 className="font-semibold">Meeting detection</h3>
          </div>
          <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
            MeetCap watches for active Meet and Zoom surfaces and offers to record. Preview the
            detection prompt below.
          </p>
          <div className="mt-4 flex flex-wrap gap-2">
            <button onClick={() => simulate('google-meet')} className="btn-secondary">
              <PlatformBadge platform="google-meet" />
            </button>
            <button onClick={() => simulate('zoom')} className="btn-secondary">
              <PlatformBadge platform="zoom" />
            </button>
          </div>
          <p className="mt-3 text-xs text-slate-400">
            Automatic background detection across tabs/windows is provided by the optional companion
            extension or desktop app.
          </p>
        </section>

        {/* Recent */}
        <section className="card p-6">
          <div className="flex items-center justify-between">
            <h3 className="font-semibold">Recent recordings</h3>
            <button
              onClick={() => setRoute('library')}
              className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 hover:text-brand-700 dark:text-brand-300"
            >
              View all <ArrowRight className="h-4 w-4" />
            </button>
          </div>
          {recordings.length === 0 ? (
            <div className="mt-4 rounded-xl border border-dashed border-slate-300 p-6 text-center text-sm text-slate-400 dark:border-slate-700">
              No recordings yet. Your saved meetings will appear here.
            </div>
          ) : (
            <ul className="mt-4 space-y-2">
              {recordings.slice(0, 3).map((r) => (
                <li
                  key={r.id}
                  className="flex items-center gap-3 rounded-xl border border-slate-100 p-3 dark:border-slate-800"
                >
                  <PlatformBadge platform={r.platform} />
                  <span className="min-w-0 flex-1 truncate text-sm font-medium">{r.name}</span>
                  <span className="shrink-0 text-xs text-slate-400">{formatRelative(r.createdAt)}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}

function AudioToggle({
  icon: Icon,
  title,
  desc,
  checked,
  onChange,
}: {
  icon: typeof Volume2
  title: string
  desc: string
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer items-center gap-3 bg-white p-4 dark:bg-slate-900">
      <span
        className={`flex h-10 w-10 items-center justify-center rounded-xl ${
          checked
            ? 'bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300'
            : 'bg-slate-100 text-slate-400 dark:bg-slate-800'
        }`}
      >
        <Icon className="h-5 w-5" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-medium">{title}</span>
        <span className="block text-xs text-slate-400">{desc}</span>
      </span>
      <Switch checked={checked} onChange={onChange} label={title} />
    </label>
  )
}

export function Switch({
  checked,
  onChange,
  label,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  label: string
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition ${
        checked ? 'bg-brand-600' : 'bg-slate-300 dark:bg-slate-700'
      }`}
    >
      <span
        className={`inline-block h-5 w-5 transform rounded-full bg-white shadow transition ${
          checked ? 'translate-x-5' : 'translate-x-0.5'
        }`}
      />
    </button>
  )
}
