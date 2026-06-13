import { useEffect, useState } from 'react'
import {
  Monitor,
  Mic,
  Volume2,
  HardDrive,
  CheckCircle2,
  XCircle,
  HelpCircle,
  ShieldCheck,
  type LucideIcon,
} from 'lucide-react'
import { useStore } from '../store/useStore'
import { isSupported } from '../lib/recorder'
import { estimateStorage } from '../lib/db'
import { formatBytes } from '../lib/format'

type Status = 'granted' | 'denied' | 'prompt' | 'unknown'

const statusMeta: Record<Status, { label: string; icon: LucideIcon; cls: string }> = {
  granted: { label: 'Granted', icon: CheckCircle2, cls: 'text-emerald-500' },
  denied: { label: 'Blocked', icon: XCircle, cls: 'text-red-500' },
  prompt: { label: 'Will ask', icon: HelpCircle, cls: 'text-amber-500' },
  unknown: { label: 'Unknown', icon: HelpCircle, cls: 'text-slate-400' },
}

export function Permissions() {
  const toast = useStore((s) => s.toast)
  const supported = useStore((s) => s.supported)
  const [mic, setMic] = useState<Status>('unknown')
  const [storage, setStorage] = useState<{ usage: number; quota: number } | null>(null)

  useEffect(() => {
    void refresh()
  }, [])

  const refresh = async () => {
    try {
      const perm = await navigator.permissions?.query({ name: 'microphone' as PermissionName })
      if (perm) {
        setMic(perm.state as Status)
        perm.onchange = () => setMic(perm.state as Status)
      }
    } catch {
      setMic('unknown')
    }
    setStorage(await estimateStorage())
  }

  const requestMic = async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      stream.getTracks().forEach((t) => t.stop())
      setMic('granted')
      toast('success', 'Microphone allowed', 'Your mic can be mixed into recordings.')
    } catch {
      setMic('denied')
      toast('error', 'Microphone blocked', 'Enable it in your browser’s site settings.')
    }
    await refresh()
  }

  const testScreen = async () => {
    try {
      const stream = await navigator.mediaDevices.getDisplayMedia({ video: true })
      stream.getTracks().forEach((t) => t.stop())
      toast('success', 'Screen capture works', 'You can share a tab, window, or screen.')
    } catch {
      toast('error', 'Screen capture cancelled', 'No screen was shared.')
    }
  }

  const storagePct = storage && storage.quota ? Math.min(100, (storage.usage / storage.quota) * 100) : 0

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div className="card p-6">
        <div className="flex items-start gap-3">
          <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-brand-50 text-brand-600 dark:bg-brand-500/15 dark:text-brand-300">
            <ShieldCheck className="h-6 w-6" />
          </span>
          <div>
            <h2 className="text-lg font-semibold">Permissions setup</h2>
            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
              MeetCap uses standard browser permissions. You’ll always see the browser’s own prompt —
              we never bypass it. Grant only what you need.
            </p>
          </div>
        </div>
      </div>

      <PermissionRow
        icon={Monitor}
        title="Screen / window / tab capture"
        desc="Choose exactly what to share each time you record. Required."
        status={supported ? 'prompt' : 'denied'}
        action={
          <button onClick={testScreen} disabled={!supported} className="btn-secondary text-sm">
            Test capture
          </button>
        }
      />

      <PermissionRow
        icon={Volume2}
        title="Tab / system audio"
        desc="Captured alongside the screen when you tick “Share tab audio” in the browser picker."
        status="prompt"
      />

      <PermissionRow
        icon={Mic}
        title="Microphone"
        desc="Optional — mixes your own voice into the recording."
        status={mic}
        action={
          <button onClick={requestMic} className="btn-secondary text-sm">
            {mic === 'granted' ? 'Re-check' : 'Allow microphone'}
          </button>
        }
      />

      <div className="card p-5">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-100 text-slate-500 dark:bg-slate-800">
            <HardDrive className="h-5 w-5" />
          </span>
          <div className="flex-1">
            <h3 className="font-medium">Local storage</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Recordings are saved on this device.
            </p>
          </div>
        </div>
        {storage ? (
          <div className="mt-4">
            <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
              <div
                className={`h-full rounded-full ${storagePct > 90 ? 'bg-red-500' : 'bg-brand-500'}`}
                style={{ width: `${storagePct}%` }}
              />
            </div>
            <p className="mt-2 text-xs text-slate-400">
              {formatBytes(storage.usage)} used of {formatBytes(storage.quota)} available
            </p>
          </div>
        ) : (
          <p className="mt-3 text-xs text-slate-400">Storage estimate not available in this browser.</p>
        )}
      </div>

      {!isSupported() && (
        <div className="flex items-start gap-3 rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-300">
          <XCircle className="mt-0.5 h-5 w-5 shrink-0" />
          <p>
            This browser is missing the APIs MeetCap needs (screen capture, MediaRecorder, or
            IndexedDB). Use a recent desktop Chrome, Edge, or Firefox.
          </p>
        </div>
      )}
    </div>
  )
}

function PermissionRow({
  icon: Icon,
  title,
  desc,
  status,
  action,
}: {
  icon: LucideIcon
  title: string
  desc: string
  status: Status
  action?: React.ReactNode
}) {
  const meta = statusMeta[status]
  const StatusIcon = meta.icon
  return (
    <div className="card flex flex-col gap-3 p-5 sm:flex-row sm:items-center">
      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-100 text-slate-500 dark:bg-slate-800">
        <Icon className="h-5 w-5" />
      </span>
      <div className="flex-1">
        <h3 className="font-medium">{title}</h3>
        <p className="text-sm text-slate-500 dark:text-slate-400">{desc}</p>
      </div>
      <div className="flex items-center gap-3">
        <span className={`inline-flex items-center gap-1.5 text-sm font-medium ${meta.cls}`}>
          <StatusIcon className="h-4 w-4" /> {meta.label}
        </span>
        {action}
      </div>
    </div>
  )
}
