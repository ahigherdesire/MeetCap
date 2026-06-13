import { useEffect, useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { useStore } from './store/useStore'
import { Sidebar } from './components/Sidebar'
import { TopBar } from './components/TopBar'
import { Toasts } from './components/Toasts'
import { MeetingDetectedPopup } from './components/MeetingDetectedPopup'
import { RecordingControlBar } from './components/RecordingControlBar'
import { SummaryModal } from './components/SummaryModal'
import { ErrorModal } from './components/ErrorModal'
import { Dashboard } from './pages/Dashboard'
import { Library } from './pages/Library'
import { Settings } from './pages/Settings'
import { Permissions } from './pages/Permissions'
import { activeAdapters, manualMeeting, type MeetcapDesktopBridge } from './lib/detection'

function useTheme() {
  const theme = useStore((s) => s.settings.theme)
  useEffect(() => {
    const root = document.documentElement
    const mql = window.matchMedia('(prefers-color-scheme: dark)')
    const apply = () => {
      const dark = theme === 'dark' || (theme === 'system' && mql.matches)
      root.classList.toggle('dark', dark)
    }
    apply()
    if (theme === 'system') {
      mql.addEventListener('change', apply)
      return () => mql.removeEventListener('change', apply)
    }
  }, [theme])
}

const pages = {
  dashboard: Dashboard,
  library: Library,
  settings: Settings,
  permissions: Permissions,
}

export default function App() {
  useTheme()
  const route = useStore((s) => s.route)
  const loadRecordings = useStore((s) => s.loadRecordings)
  const autoDetect = useStore((s) => s.settings.autoDetect)
  const setDetected = useStore((s) => s.setDetected)
  const startRecording = useStore((s) => s.startRecording)
  const recordingState = useStore((s) => s.recordingState)
  const [drawer, setDrawer] = useState(false)

  // Initial load.
  useEffect(() => {
    void loadRecordings()
  }, [loadRecordings])

  // Wire any available detection adapters (browser extension / desktop shell).
  useEffect(() => {
    if (!autoDetect) return
    const adapters = activeAdapters()
    adapters.forEach((a) => a.start((m) => useStore.getState().recordingState === 'idle' && setDetected(m)))
    return () => adapters.forEach((a) => a.stop())
  }, [autoDetect, setDetected])

  // Desktop bridge: the native popup's "Start Recording" arrives here, and we
  // mirror recording + detection-enabled state back so the tray stays in sync.
  const desktop = (window as unknown as { meetcapDesktop?: MeetcapDesktopBridge }).meetcapDesktop
  useEffect(() => {
    if (!desktop) return
    return desktop.onStartRecording((m) => startRecording(manualMeeting(m.platform, m.title)))
  }, [desktop, startRecording])

  useEffect(() => {
    desktop?.setRecordingState(recordingState)
  }, [desktop, recordingState])

  useEffect(() => {
    desktop?.setDetectionEnabled(autoDetect)
  }, [desktop, autoDetect])

  // Warn before closing the tab mid-recording so footage isn't lost silently.
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (recordingState === 'recording' || recordingState === 'paused') {
        e.preventDefault()
        e.returnValue = ''
      }
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [recordingState])

  const Page = pages[route]

  return (
    <div className="flex h-full">
      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 border-r border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900 lg:block">
        <Sidebar />
      </aside>

      {/* Mobile drawer */}
      <AnimatePresence>
        {drawer && (
          <div className="fixed inset-0 z-50 lg:hidden">
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 bg-slate-900/50 backdrop-blur-sm"
              onClick={() => setDrawer(false)}
            />
            <motion.aside
              initial={{ x: -280 }}
              animate={{ x: 0 }}
              exit={{ x: -280 }}
              transition={{ type: 'spring', stiffness: 320, damping: 32 }}
              className="absolute inset-y-0 left-0 w-64 border-r border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900"
            >
              <Sidebar onNavigate={() => setDrawer(false)} />
            </motion.aside>
          </div>
        )}
      </AnimatePresence>

      {/* Main */}
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar onMenu={() => setDrawer(true)} />
        <main className="flex-1 overflow-y-auto px-4 py-6 sm:px-6 lg:px-8">
          <AnimatePresence mode="wait">
            <motion.div
              key={route}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -8 }}
              transition={{ duration: 0.18 }}
            >
              <Page />
            </motion.div>
          </AnimatePresence>
        </main>
      </div>

      {/* Global overlays */}
      <RecordingControlBar />
      <MeetingDetectedPopup />
      <SummaryModal />
      <ErrorModal />
      <Toasts />
    </div>
  )
}
