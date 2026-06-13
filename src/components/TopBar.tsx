import { Menu, Circle } from 'lucide-react'
import { useStore, type Route } from '../store/useStore'
import { ThemeToggle } from './ThemeToggle'

const titles: Record<Route, string> = {
  dashboard: 'Dashboard',
  library: 'Recordings',
  permissions: 'Permissions',
  settings: 'Settings',
}

export function TopBar({ onMenu }: { onMenu: () => void }) {
  const route = useStore((s) => s.route)
  const state = useStore((s) => s.recordingState)
  const recording = state === 'recording' || state === 'paused'

  return (
    <header className="sticky top-0 z-30 flex items-center gap-3 border-b border-slate-200 bg-slate-50/80 px-4 py-3 backdrop-blur dark:border-slate-800 dark:bg-slate-950/80 sm:px-6">
      <button
        onClick={onMenu}
        className="btn-ghost -ml-2 p-2 lg:hidden"
        aria-label="Open navigation menu"
      >
        <Menu className="h-5 w-5" />
      </button>
      <h1 className="text-lg font-semibold">{titles[route]}</h1>

      {recording && (
        <span
          className={`ml-2 inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${
            state === 'paused'
              ? 'bg-amber-50 text-amber-600 dark:bg-amber-500/15 dark:text-amber-300'
              : 'bg-red-50 text-red-600 dark:bg-red-500/15 dark:text-red-300'
          }`}
          role="status"
        >
          <Circle className="h-2 w-2 fill-current" />
          {state === 'paused' ? 'Paused' : 'Recording'}
        </span>
      )}

      <div className="ml-auto">
        <ThemeToggle />
      </div>
    </header>
  )
}
