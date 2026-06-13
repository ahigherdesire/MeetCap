import { Video } from 'lucide-react'
import type { Platform } from '../lib/types'

const labels: Record<Platform, string> = {
  'google-meet': 'Google Meet',
  zoom: 'Zoom',
  unknown: 'Screen',
}

const styles: Record<Platform, string> = {
  'google-meet':
    'bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-400/20',
  zoom: 'bg-blue-50 text-blue-700 ring-blue-600/20 dark:bg-blue-500/10 dark:text-blue-300 dark:ring-blue-400/20',
  unknown:
    'bg-slate-100 text-slate-600 ring-slate-500/20 dark:bg-slate-700/40 dark:text-slate-300 dark:ring-slate-400/20',
}

export function PlatformBadge({ platform }: { platform: Platform }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset ${styles[platform]}`}
    >
      <Video className="h-3.5 w-3.5" aria-hidden />
      {labels[platform]}
    </span>
  )
}
