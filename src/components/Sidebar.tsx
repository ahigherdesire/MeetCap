import { LayoutDashboard, Library, Settings as SettingsIcon, ShieldCheck, type LucideIcon } from 'lucide-react'
import { useStore, type Route } from '../store/useStore'
import { Logo } from './Logo'

const nav: { route: Route; label: string; icon: LucideIcon }[] = [
  { route: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
  { route: 'library', label: 'Recordings', icon: Library },
  { route: 'permissions', label: 'Permissions', icon: ShieldCheck },
  { route: 'settings', label: 'Settings', icon: SettingsIcon },
]

export function Sidebar({ onNavigate }: { onNavigate?: () => void }) {
  const route = useStore((s) => s.route)
  const setRoute = useStore((s) => s.setRoute)
  const count = useStore((s) => s.recordings.length)

  return (
    <nav className="flex h-full flex-col gap-1 p-4" aria-label="Primary">
      <div className="mb-4 flex items-center gap-2.5 px-2">
        <Logo className="h-9 w-9" />
        <div className="leading-tight">
          <p className="text-sm font-bold">MeetCap</p>
          <p className="text-[11px] text-slate-400">Consent-first recorder</p>
        </div>
      </div>

      {nav.map(({ route: r, label, icon: Icon }) => {
        const activeRoute = route === r
        return (
          <button
            key={r}
            onClick={() => {
              setRoute(r)
              onNavigate?.()
            }}
            aria-current={activeRoute ? 'page' : undefined}
            className={`group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition ${
              activeRoute
                ? 'bg-brand-50 text-brand-700 dark:bg-brand-500/15 dark:text-brand-200'
                : 'text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800'
            }`}
          >
            <Icon className="h-[18px] w-[18px]" aria-hidden />
            <span className="flex-1 text-left">{label}</span>
            {r === 'library' && count > 0 && (
              <span className="rounded-full bg-slate-200 px-2 py-0.5 text-[11px] font-semibold text-slate-600 dark:bg-slate-700 dark:text-slate-200">
                {count}
              </span>
            )}
          </button>
        )
      })}

      <div className="mt-auto rounded-xl bg-slate-50 p-3 text-xs text-slate-500 dark:bg-slate-800/50 dark:text-slate-400">
        <div className="flex items-center gap-1.5 font-medium text-slate-600 dark:text-slate-300">
          <ShieldCheck className="h-3.5 w-3.5" /> Privacy-first
        </div>
        <p className="mt-1 leading-relaxed">
          Recordings stay on this device. We never record without your explicit consent.
        </p>
      </div>
    </nav>
  )
}
