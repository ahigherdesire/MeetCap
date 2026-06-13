import { useEffect, useMemo, useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import {
  Search,
  Play,
  Download,
  Trash2,
  Pencil,
  Check,
  X,
  Film,
  Clock,
  HardDrive,
} from 'lucide-react'
import { useStore } from '../store/useStore'
import { getRecording } from '../lib/db'
import { formatBytes, formatDuration, formatDateTime } from '../lib/format'
import { PlatformBadge } from '../components/PlatformBadge'
import { Modal } from '../components/Modal'
import type { RecordingMeta } from '../lib/types'

export function Library() {
  const recordings = useStore((s) => s.recordings)
  const load = useStore((s) => s.loadRecordings)
  const remove = useStore((s) => s.deleteRecording)
  const rename = useStore((s) => s.renameRecording)
  const toast = useStore((s) => s.toast)

  const [query, setQuery] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editName, setEditName] = useState('')
  const [preview, setPreview] = useState<{ url: string; meta: RecordingMeta } | null>(null)
  const [confirmDelete, setConfirmDelete] = useState<RecordingMeta | null>(null)

  useEffect(() => {
    void load()
  }, [load])

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return recordings
    return recordings.filter(
      (r) => r.name.toLowerCase().includes(q) || r.meetingTitle?.toLowerCase().includes(q),
    )
  }, [recordings, query])

  const openPreview = async (meta: RecordingMeta) => {
    const rec = await getRecording(meta.id)
    if (!rec) return toast('error', 'Not found', 'This recording could not be loaded.')
    setPreview({ url: URL.createObjectURL(rec.blob), meta })
  }

  const closePreview = () => {
    if (preview) URL.revokeObjectURL(preview.url)
    setPreview(null)
  }

  const download = async (meta: RecordingMeta) => {
    const rec = await getRecording(meta.id)
    if (!rec) return toast('error', 'Not found', 'This recording could not be loaded.')
    const url = URL.createObjectURL(rec.blob)
    const a = document.createElement('a')
    a.href = url
    const ext = meta.mimeType.includes('mp4') ? 'mp4' : 'webm'
    a.download = `${meta.name.replace(/[^\w.-]+/g, '_')}.${ext}`
    a.click()
    URL.revokeObjectURL(url)
    toast('success', 'Download started', meta.name)
  }

  const commitRename = async (id: string) => {
    const name = editName.trim()
    if (name) await rename(id, name)
    setEditingId(null)
  }

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      {/* Search */}
      <div className="relative">
        <Search className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search recordings…"
          aria-label="Search recordings"
          className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm focus-visible:ring-2 focus-visible:ring-brand-500 dark:border-slate-700 dark:bg-slate-900"
        />
      </div>

      {filtered.length === 0 ? (
        <div className="card flex flex-col items-center justify-center p-12 text-center">
          <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-slate-100 text-slate-400 dark:bg-slate-800">
            <Film className="h-7 w-7" />
          </div>
          <h3 className="mt-4 font-semibold">{query ? 'No matches' : 'No recordings yet'}</h3>
          <p className="mt-1 max-w-sm text-sm text-slate-500 dark:text-slate-400">
            {query
              ? 'Try a different search term.'
              : 'Start a recording from the dashboard and it’ll show up here, stored locally on your device.'}
          </p>
        </div>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <AnimatePresence initial={false}>
            {filtered.map((r) => (
              <motion.li
                key={r.id}
                layout
                initial={{ opacity: 0, scale: 0.97 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.95 }}
                className="card flex flex-col p-4"
              >
                <div className="flex items-start justify-between gap-2">
                  <PlatformBadge platform={r.platform} />
                  <span className="text-xs text-slate-400">{formatDateTime(r.createdAt)}</span>
                </div>

                {editingId === r.id ? (
                  <div className="mt-3 flex items-center gap-2">
                    <input
                      autoFocus
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') void commitRename(r.id)
                        if (e.key === 'Escape') setEditingId(null)
                      }}
                      className="w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-sm dark:border-slate-700 dark:bg-slate-800"
                      aria-label="Recording name"
                    />
                    <button onClick={() => commitRename(r.id)} className="btn-primary p-1.5" aria-label="Save">
                      <Check className="h-4 w-4" />
                    </button>
                    <button onClick={() => setEditingId(null)} className="btn-secondary p-1.5" aria-label="Cancel">
                      <X className="h-4 w-4" />
                    </button>
                  </div>
                ) : (
                  <h3 className="mt-3 truncate text-base font-semibold" title={r.name}>
                    {r.name}
                  </h3>
                )}

                <div className="mt-2 flex items-center gap-4 text-xs text-slate-400">
                  <span className="inline-flex items-center gap-1">
                    <Clock className="h-3.5 w-3.5" /> {formatDuration(r.durationMs)}
                  </span>
                  <span className="inline-flex items-center gap-1">
                    <HardDrive className="h-3.5 w-3.5" /> {formatBytes(r.size)}
                  </span>
                </div>

                <div className="mt-4 flex items-center gap-1.5 border-t border-slate-100 pt-3 dark:border-slate-800">
                  <button onClick={() => openPreview(r)} className="btn-primary flex-1 px-3 py-2 text-xs">
                    <Play className="h-4 w-4" /> Play
                  </button>
                  <button onClick={() => download(r)} className="btn-secondary px-2.5 py-2" aria-label="Download">
                    <Download className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => {
                      setEditingId(r.id)
                      setEditName(r.name)
                    }}
                    className="btn-secondary px-2.5 py-2"
                    aria-label="Rename"
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => setConfirmDelete(r)}
                    className="btn-secondary px-2.5 py-2 text-red-600 hover:bg-red-50 dark:hover:bg-red-500/10"
                    aria-label="Delete"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </motion.li>
            ))}
          </AnimatePresence>
        </ul>
      )}

      {/* Player */}
      <Modal open={!!preview} onClose={closePreview} title={preview?.meta.name}>
        {preview && (
          <div className="p-4">
            <video src={preview.url} controls autoPlay className="w-full rounded-xl bg-black" />
            <div className="mt-3 flex items-center justify-between text-xs text-slate-400">
              <span>{formatDuration(preview.meta.durationMs)}</span>
              <span>{formatBytes(preview.meta.size)}</span>
            </div>
          </div>
        )}
      </Modal>

      {/* Delete confirmation */}
      <Modal open={!!confirmDelete} onClose={() => setConfirmDelete(null)} title="Delete recording?">
        <div className="p-6">
          <p className="text-sm text-slate-500 dark:text-slate-400">
            “{confirmDelete?.name}” will be permanently removed from this device. This can’t be undone.
          </p>
          <div className="mt-6 flex gap-2">
            <button onClick={() => setConfirmDelete(null)} className="btn-secondary flex-1">
              Cancel
            </button>
            <button
              onClick={async () => {
                if (confirmDelete) await remove(confirmDelete.id)
                setConfirmDelete(null)
              }}
              className="btn-danger flex-1"
            >
              Delete
            </button>
          </div>
        </div>
      </Modal>
    </div>
  )
}
