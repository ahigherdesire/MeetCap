import { Moon, Sun, Monitor } from 'lucide-react'
import { useStore } from '../store/useStore'
import type { Settings } from '../lib/types'

const options: { value: Settings['theme']; label: string; icon: typeof Sun }[] = [
  { value: 'light', label: 'Light', icon: Sun },
  { value: 'dark', label: 'Dark', icon: Moon },
  { value: 'system', label: 'System', icon: Monitor },
]

export function ThemeToggle() {
  const theme = useStore((s) => s.settings.theme)
  const setTheme = useStore((s) => s.setTheme)

  return (
    <div
      role="radiogroup"
      aria-label="Color theme"
      className="inline-flex items-center gap-0.5 rounded-xl border border-slate-200 bg-white p-0.5 dark:border-slate-700 dark:bg-slate-800"
    >
      {options.map(({ value, label, icon: Icon }) => (
        <button
          key={value}
          role="radio"
          aria-checked={theme === value}
          aria-label={label}
          title={label}
          onClick={() => setTheme(value)}
          className={`rounded-lg p-1.5 transition ${
            theme === value
              ? 'bg-brand-50 text-brand-600 dark:bg-brand-500/20 dark:text-brand-300'
              : 'text-slate-400 hover:text-slate-600 dark:hover:text-slate-200'
          }`}
        >
          <Icon className="h-4 w-4" />
        </button>
      ))}
    </div>
  )
}
