import { useEffect, useState } from 'react'
import { Volume2, Mic, Bell, Radio, Cloud, Database, Lock, Trash2, ShieldCheck } from 'lucide-react'
import { useStore } from '../store/useStore'
import { Switch } from './Dashboard'
import { ThemeToggle } from '../components/ThemeToggle'
import { estimateStorage } from '../lib/db'
import { formatBytes } from '../lib/format'

export function Settings() {
  const settings = useStore((s) => s.settings)
  const update = useStore((s) => s.updateSettings)
  const recordings = useStore((s) => s.recordings)
  const remove = useStore((s) => s.deleteRecording)
  const toast = useStore((s) => s.toast)
  const [storage, setStorage] = useState<{ usage: number; quota: number } | null>(null)

  useEffect(() => {
    void estimateStorage().then(setStorage)
  }, [recordings.length])

  const setAudio = (patch: Partial<typeof settings.defaultAudio>) =>
    update({ defaultAudio: { ...settings.defaultAudio, ...patch } })

  const clearAll = async () => {
    for (const r of recordings) await remove(r.id)
    toast('info', 'All recordings deleted', 'Your local library is now empty.')
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      {/* Appearance */}
      <Section title="Appearance" desc="Choose how MeetCap looks.">
        <Row title="Theme" desc="Light, dark, or follow your system.">
          <ThemeToggle />
        </Row>
      </Section>

      {/* Default capture */}
      <Section title="Default capture" desc="Pre-selected audio sources for new recordings.">
        <Row icon={Volume2} title="Tab / system audio" desc="Capture other participants’ audio.">
          <Switch
            label="System audio"
            checked={settings.defaultAudio.systemAudio}
            onChange={(v) => setAudio({ systemAudio: v })}
          />
        </Row>
        <Row icon={Mic} title="Microphone" desc="Capture your own voice.">
          <Switch
            label="Microphone"
            checked={settings.defaultAudio.microphone}
            onChange={(v) => setAudio({ microphone: v })}
          />
        </Row>
      </Section>

      {/* Detection & consent */}
      <Section title="Detection & consent" desc="How MeetCap finds meetings and reminds you about consent.">
        <Row icon={Radio} title="Auto-detect meetings" desc="Offer to record when a Meet/Zoom surface is found.">
          <Switch
            label="Auto-detect"
            checked={settings.autoDetect}
            onChange={(v) => update({ autoDetect: v })}
          />
        </Row>
        <Row
          icon={Bell}
          title="Consent reminder"
          desc="Show a participant-consent note before recording starts."
        >
          <Switch
            label="Consent reminder"
            checked={settings.consentReminder}
            onChange={(v) => update({ consentReminder: v })}
          />
        </Row>
      </Section>

      {/* Storage */}
      <Section title="Storage" desc="Where your recordings live.">
        <Row icon={Database} title="Save location" desc={settings.storageLocationLabel}>
          <span className="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-medium text-emerald-600 dark:bg-emerald-500/15 dark:text-emerald-300">
            Local
          </span>
        </Row>
        <Row icon={Cloud} title="Cloud upload" desc="Optional, future-ready. Upload a copy after saving locally.">
          <div className="flex items-center gap-2">
            <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500 dark:bg-slate-700 dark:text-slate-300">
              Coming soon
            </span>
            <Switch
              label="Cloud upload"
              checked={settings.cloudUpload}
              onChange={(v) => {
                update({ cloudUpload: v })
                if (v) toast('info', 'Cloud upload is a preview', 'No data leaves your device yet.')
              }}
            />
          </div>
        </Row>
        {storage && (
          <div className="px-5 pb-5">
            <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100 dark:bg-slate-800">
              <div
                className="h-full rounded-full bg-brand-500"
                style={{
                  width: `${storage.quota ? Math.min(100, (storage.usage / storage.quota) * 100) : 0}%`,
                }}
              />
            </div>
            <p className="mt-2 text-xs text-slate-400">
              {formatBytes(storage.usage)} used · {recordings.length} recording
              {recordings.length === 1 ? '' : 's'}
            </p>
          </div>
        )}
      </Section>

      {/* Privacy */}
      <Section title="Privacy" desc="What we store, and where.">
        <div className="space-y-3 px-5 py-4 text-sm text-slate-600 dark:text-slate-300">
          <PrivacyPoint icon={Lock} title="Local-first by default">
            Recordings, names, and settings are stored only in this browser (IndexedDB +
            localStorage). Nothing is uploaded unless you explicitly enable cloud upload.
          </PrivacyPoint>
          <PrivacyPoint icon={ShieldCheck} title="Consent-first, never stealth">
            Recording only starts when you click “Start Recording”, and a clear indicator stays
            visible the whole time. MeetCap never records hidden or in the background.
          </PrivacyPoint>
          <PrivacyPoint icon={Database} title="No analytics, no trackers">
            MeetCap doesn’t phone home. There’s no telemetry and no third-party tracking.
          </PrivacyPoint>
        </div>
      </Section>

      {/* Danger zone */}
      <Section title="Danger zone" desc="Irreversible actions.">
        <Row icon={Trash2} title="Delete all recordings" desc="Permanently remove every recording on this device.">
          <button onClick={clearAll} disabled={recordings.length === 0} className="btn-danger text-sm">
            Delete all
          </button>
        </Row>
      </Section>

      <p className="px-1 pb-2 text-center text-xs text-slate-400">
        MeetCap · consent-first, local-first meeting recorder
      </p>
    </div>
  )
}

function Section({
  title,
  desc,
  children,
}: {
  title: string
  desc: string
  children: React.ReactNode
}) {
  return (
    <section className="card overflow-hidden">
      <div className="border-b border-slate-100 px-5 py-4 dark:border-slate-800">
        <h2 className="font-semibold">{title}</h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">{desc}</p>
      </div>
      <div className="divide-y divide-slate-100 dark:divide-slate-800">{children}</div>
    </section>
  )
}

function Row({
  icon: Icon,
  title,
  desc,
  children,
}: {
  icon?: typeof Volume2
  title: string
  desc: string
  children: React.ReactNode
}) {
  return (
    <div className="flex items-center gap-3 px-5 py-4">
      {Icon && (
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-500 dark:bg-slate-800">
          <Icon className="h-[18px] w-[18px]" />
        </span>
      )}
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">{title}</p>
        <p className="text-xs text-slate-400">{desc}</p>
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  )
}

function PrivacyPoint({
  icon: Icon,
  title,
  children,
}: {
  icon: typeof Lock
  title: string
  children: React.ReactNode
}) {
  return (
    <div className="flex items-start gap-3">
      <Icon className="mt-0.5 h-4 w-4 shrink-0 text-brand-500" />
      <p>
        <span className="font-medium text-slate-700 dark:text-slate-200">{title}.</span> {children}
      </p>
    </div>
  )
}
