import { useEffect, useState } from 'react'
import { CheckCircle2, Calendar, Clock, HardDrive, FolderOpen, Pencil, Check } from 'lucide-react'
import { Modal } from './Modal'
import { PlatformBadge } from './PlatformBadge'
import { useStore } from '../store/useStore'
import { formatBytes, formatDateTime, formatDuration } from '../lib/format'

export function SummaryModal() {
  const summary = useStore((s) => s.summary)
  const clear = useStore((s) => s.clearSummary)
  const rename = useStore((s) => s.renameRecording)
  const setRoute = useStore((s) => s.setRoute)

  const [editing, setEditing] = useState(false)
  const [name, setName] = useState('')

  useEffect(() => {
    if (summary) {
      setName(summary.name)
      setEditing(false)
    }
  }, [summary])

  if (!summary) return null

  const commit = async () => {
    const trimmed = name.trim()
    if (trimmed && trimmed !== summary.name) await rename(summary.id, trimmed)
    setEditing(false)
  }

  const rows = [
    { icon: Calendar, label: 'Date & time', value: formatDateTime(summary.createdAt) },
    { icon: Clock, label: 'Duration', value: formatDuration(summary.durationMs) },
    { icon: HardDrive, label: 'File size', value: formatBytes(summary.size) },
    { icon: FolderOpen, label: 'Save location', value: summary.saveLocation },
  ]

  return (
    <Modal open={!!summary} onClose={clear} title="">
      <div className="px-6 pb-6 pt-2">
        <div className="flex flex-col items-center text-center">
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-50 text-emerald-500 dark:bg-emerald-500/15">
            <CheckCircle2 className="h-8 w-8" aria-hidden />
          </div>
          <h2 className="mt-4 text-lg font-semibold">Recording saved</h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Your recording is stored locally and ready to review.
          </p>
        </div>

        {/* Title / rename */}
        <div className="mt-6 rounded-xl border border-slate-200 p-4 dark:border-slate-800">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium uppercase tracking-wide text-slate-400">
              Meeting title
            </span>
            <PlatformBadge platform={summary.platform} />
          </div>
          {editing ? (
            <div className="mt-2 flex items-center gap-2">
              <input
                autoFocus
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && commit()}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-sm focus-visible:ring-2 focus-visible:ring-brand-500 dark:border-slate-700 dark:bg-slate-800"
                aria-label="Recording name"
              />
              <button onClick={commit} className="btn-primary px-2.5 py-1.5" aria-label="Save name">
                <Check className="h-4 w-4" />
              </button>
            </div>
          ) : (
            <div className="mt-1.5 flex items-center justify-between gap-2">
              <p className="truncate text-base font-semibold">{summary.name}</p>
              <button
                onClick={() => setEditing(true)}
                className="btn-ghost shrink-0 px-2.5 py-1.5 text-xs"
                aria-label="Rename recording"
              >
                <Pencil className="h-3.5 w-3.5" /> Rename
              </button>
            </div>
          )}
        </div>

        {/* Details */}
        <dl className="mt-4 grid grid-cols-1 gap-px overflow-hidden rounded-xl border border-slate-200 bg-slate-200 dark:border-slate-800 dark:bg-slate-800 sm:grid-cols-2">
          {rows.map(({ icon: Icon, label, value }) => (
            <div key={label} className="flex items-center gap-3 bg-white p-3.5 dark:bg-slate-900">
              <Icon className="h-4 w-4 shrink-0 text-slate-400" aria-hidden />
              <div className="min-w-0">
                <dt className="text-xs text-slate-400">{label}</dt>
                <dd className="truncate text-sm font-medium">{value}</dd>
              </div>
            </div>
          ))}
        </dl>

        <div className="mt-6 flex gap-2">
          <button onClick={clear} className="btn-secondary flex-1">
            Done
          </button>
          <button
            onClick={() => {
              clear()
              setRoute('library')
            }}
            className="btn-primary flex-1"
          >
            View in Library
          </button>
        </div>
      </div>
    </Modal>
  )
}
